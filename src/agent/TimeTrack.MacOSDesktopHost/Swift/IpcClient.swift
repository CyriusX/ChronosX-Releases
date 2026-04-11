import Foundation

enum IpcError: Error {
    case notConnected
    case sendFailed(String)
    case responseTimeout
    case parseError(String)
}

struct IpcResponse: Codable {
    let success: Bool
    let data: AnyCodable?
    let error: String?
}

struct IpcEvent: Codable {
    let eventType: String
    let payload: AnyCodable?
}

enum AnyCodable: Codable {
    case null
    case bool(Bool)
    case int(Int)
    case double(Double)
    case string(String)
    case array([AnyCodable])
    case dictionary([String: AnyCodable])

    var value: Any? {
        switch self {
        case .null: return nil
        case .bool(let v): return v
        case .int(let v): return v
        case .double(let v): return v
        case .string(let v): return v
        case .array(let v): return v.map { $0.value }
        case .dictionary(let v): return v.mapValues { $0.value }
        }
    }

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if container.decodeNil() { self = .null }
        else if let v = try? container.decode(Bool.self) { self = .bool(v) }
        else if let v = try? container.decode(Int.self) { self = .int(v) }
        else if let v = try? container.decode(Double.self) { self = .double(v) }
        else if let v = try? container.decode(String.self) { self = .string(v) }
        else if let v = try? container.decode([AnyCodable].self) { self = .array(v) }
        else if let v = try? container.decode([String: AnyCodable].self) { self = .dictionary(v) }
        else { self = .null }
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case .null: try container.encodeNil()
        case .bool(let v): try container.encode(v)
        case .int(let v): try container.encode(v)
        case .double(let v): try container.encode(v)
        case .string(let v): try container.encode(v)
        case .array(let v): try container.encode(v)
        case .dictionary(let v): try container.encode(v)
        }
    }
}

@MainActor
class IpcClient: ObservableObject {
    private let socketPath: String
    private let jsonDecoder: JSONDecoder = {
        let d = JSONDecoder()
        d.keyDecodingStrategy = .convertFromSnakeCase
        return d
    }()

    private var socket: Socket?
    private var listenTask: Task<Void, Never>?
    private var requestId = 0
    private var pendingRequests: [Int: CheckedContinuation<IpcResponse, Error>] = [:]

    @Published var isConnected = false

    var onEvent: ((IpcEvent) -> Void)?
    var onConnectionStateChanged: ((Bool) -> Void)?

    init(socketPath: String = "/var/tmp/TimeTrack.Agent.IPC") {
        self.socketPath = socketPath
    }

    func connect() async throws {
        let sock = try Socket(path: socketPath)
        self.socket = sock
        self.isConnected = true
        self.onConnectionStateChanged?(true)
        startListening()
    }

    func disconnect() {
        listenTask?.cancel()
        listenTask = nil
        socket?.close()
        socket = nil
        isConnected = false
        onConnectionStateChanged?(false)

        for (_, cont) in pendingRequests {
            cont.resume(throwing: IpcError.notConnected)
        }
        pendingRequests.removeAll()
    }

    func sendCommand(_ command: String, payload: Any? = nil) async throws -> IpcResponse {
        return try await sendMessage(type: "command", name: command, payload: payload)
    }

    func sendQuery(_ query: String, payload: Any? = nil) async throws -> IpcResponse {
        return try await sendMessage(type: "query", name: query, payload: payload)
    }

    private func sendMessage(type: String, name: String, payload: Any?) async throws -> IpcResponse {
        guard let socket = socket else { throw IpcError.notConnected }

        let currentId = nextRequestId()
        let message: [String: Any] = [
            "requestId": currentId,
            "type": type,
            "name": name,
            "payload": payload as Any
        ].compactMapValues { $0 }

        let json = try JSONSerialization.data(withJSONObject: message)
        guard let jsonString = String(data: json, encoding: .utf8) else {
            throw IpcError.sendFailed("Failed to encode message")
        }

        return try await withCheckedThrowingContinuation { continuation in
            pendingRequests[currentId] = continuation
            do {
                try socket.send(jsonString + "\n")
            } catch {
                pendingRequests.removeValue(forKey: currentId)
                continuation.resume(throwing: error)
            }
        }
    }

    private func startListening() {
        listenTask = Task { [weak self] in
            guard let self = self, let socket = self.socket else { return }

            do {
                for try await line in socket.lines {
                    guard !Task.isCancelled else { break }
                    self.processIncomingMessage(line)
                }
            } catch {
                await MainActor.run {
                    self.isConnected = false
                    self.onConnectionStateChanged?(false)
                }
            }
        }
    }

    private func processIncomingMessage(_ line: String) {
        guard let data = line.data(using: .utf8),
              let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return
        }

        if let requestId = json["requestId"] as? Int {
            let success = json["success"] as? Bool ?? false
            let error = json["error"] as? String
            let responseData = json["data"]

            let response = IpcResponse(
                success: success,
                data: responseData != nil ? .string(String(describing: responseData)) : nil,
                error: error
            )

            if let continuation = pendingRequests.removeValue(forKey: requestId) {
                continuation.resume(returning: response)
            }
        } else if let eventType = json["eventType"] as? String {
            let payload = json["payload"]
            let event = IpcEvent(
                eventType: eventType,
                payload: payload != nil ? .string(String(describing: payload)) : nil
            )
            onEvent?(event)
        }
    }

    private func nextRequestId() -> Int {
        requestId += 1
        return requestId
    }
}

private class Socket {
    private var fileDescriptor: Int32
    private let bufferedReader: BufferedReader

    init(path: String) throws {
        let fd = Darwin.socket(AF_UNIX, SOCK_STREAM, 0)
        if fd < 0 {
            throw IpcError.sendFailed("Failed to create socket")
        }

        var addr = sockaddr_un()
        addr.sun_family = sa_family_t(AF_UNIX)
        path.withCString { pathPtr in
            let len = min(strlen(pathPtr) + 1, 104)
            memcpy(&addr.sun_path, pathPtr, len)
        }
        addr.sun_len = UInt8(MemoryLayout<sockaddr_un>.size)

        let connectResult = withUnsafePointer(to: addr) { ptr in
            ptr.withMemoryRebound(to: sockaddr.self, capacity: 1) { rebound in
                Darwin.connect(fd, rebound, socklen_t(MemoryLayout<sockaddr_un>.size))
            }
        }

        if connectResult < 0 {
            Darwin.close(fd)
            throw IpcError.sendFailed("Failed to connect to \(path)")
        }

        fileDescriptor = fd
        bufferedReader = BufferedReader(fd: fd)
    }

    func send(_ message: String) throws {
        guard let data = message.data(using: .utf8) else {
            throw IpcError.sendFailed("Failed to encode message")
        }
        let sent = data.withUnsafeBytes { ptr in
            Darwin.send(fileDescriptor, ptr.baseAddress, data.count, 0)
        }
        if sent < 0 {
            throw IpcError.sendFailed("send() failed")
        }
    }

    var lines: AsyncLineSequence {
        return AsyncLineSequence(reader: bufferedReader)
    }

    func close() {
        if fileDescriptor >= 0 {
            Darwin.close(fileDescriptor)
            fileDescriptor = -1
        }
    }

    deinit {
        close()
    }
}

private class BufferedReader {
    private let fd: Int32
    private var buffer = ""

    init(fd: Int32) {
        self.fd = fd
    }

    func readLine() async -> String? {
        if let newlineRange = buffer.range(of: "\n") {
            let line = String(buffer[..<newlineRange.lowerBound])
            buffer.removeSubrange(..<newlineRange.upperBound)
            return line
        }

        while true {
            var chunk = [UInt8](repeating: 0, count: 4096)
            let bytesRead = Darwin.read(fd, &chunk, 4096)
            if bytesRead <= 0 { return nil }

            let data = Data(chunk[..<bytesRead])
            guard let str = String(data: data, encoding: .utf8) else { return nil }
            buffer += str

            if let newlineRange = buffer.range(of: "\n") {
                let line = String(buffer[..<newlineRange.lowerBound])
                buffer.removeSubrange(..<newlineRange.upperBound)
                return line
            }
        }
    }
}

private struct AsyncLineSequence: AsyncSequence {
    typealias Element = String
    let reader: BufferedReader

    struct AsyncIterator: AsyncIteratorProtocol {
        let reader: BufferedReader
        mutating func next() async -> String? {
            return await reader.readLine()
        }
    }

    func makeAsyncIterator() -> AsyncIterator {
        return AsyncIterator(reader: reader)
    }
}
