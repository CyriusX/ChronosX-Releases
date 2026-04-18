import Foundation

class LocalHTTPServer {
    private var socketFd: Int32 = -1
    private(set) var port: UInt16 = 0
    private(set) var baseURL: String = ""
    private var resourcePath: String = ""
    private let apiBaseURL = "https://chronosx-timetrack-api.gpoda0.easypanel.host"
    private static let preferredPortDefaultsKey = "TimeTrack.LocalHTTPServer.preferredPort"
    private static let basePort: UInt16 = 49621

    init?(resourcePath: String) {
        self.resourcePath = resourcePath

        socketFd = Darwin.socket(AF_INET, SOCK_STREAM, 0)
        guard socketFd >= 0 else { return nil }

        var reuse: Int32 = 1
        setsockopt(socketFd, SOL_SOCKET, SO_REUSEADDR, &reuse, socklen_t(MemoryLayout.size(ofValue: reuse)))

        // Use a stable port so WebKit's origin (and localStorage) remains stable between app restarts.
        // This prevents users from being forced to login again on every launch.
        let preferred = UInt16(UserDefaults.standard.integer(forKey: Self.preferredPortDefaultsKey))
        let startPort = preferred != 0 ? preferred : Self.basePort

        var bound = false
        for offset in 0..<10 {
            let candidate = startPort &+ UInt16(offset)
            if tryBind(port: candidate) {
                port = candidate
                bound = true
                UserDefaults.standard.set(Int(candidate), forKey: Self.preferredPortDefaultsKey)
                break
            }
        }

        guard bound else {
            Darwin.close(socketFd)
            return nil
        }
        baseURL = "http://localhost:\(port)"

        Darwin.listen(socketFd, 10)

        DispatchQueue.global(qos: .background).async { [weak self] in
            self?.acceptLoop()
        }
    }

    private func tryBind(port: UInt16) -> Bool {
        var addr = sockaddr_in()
        addr.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        addr.sin_family = sa_family_t(AF_INET)
        addr.sin_port = port.bigEndian
        addr.sin_addr.s_addr = inet_addr("127.0.0.1")

        let bindResult = withUnsafePointer(to: &addr) { ptr in
            ptr.withMemoryRebound(to: sockaddr.self, capacity: 1) { rebound in
                Darwin.bind(socketFd, rebound, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }

        return bindResult == 0
    }

    deinit {
        if socketFd >= 0 {
            Darwin.close(socketFd)
        }
    }

    private func acceptLoop() {
        while socketFd >= 0 {
            var clientAddr = sockaddr_in()
            var clientLen = socklen_t(MemoryLayout<sockaddr_in>.size)
            let clientFd = withUnsafeMutablePointer(to: &clientAddr) { ptr in
                ptr.withMemoryRebound(to: sockaddr.self, capacity: 1) { rebound in
                    Darwin.accept(socketFd, rebound, &clientLen)
                }
            }

            guard clientFd >= 0 else { break }

            let resources = self.resourcePath
            DispatchQueue.global(qos: .background).async {
                self.handleClient(clientFd, resourcePath: resources)
            }
        }
    }

    private func handleClient(_ fd: Int32, resourcePath: String) {
        defer { Darwin.close(fd) }

        guard let request = readHttpRequest(fd) else { return }

        let method = request.method
        var path = request.path
        if path.hasPrefix("/") { path = String(path.dropFirst()) }
        if path.isEmpty { path = "index.html" }

        if method == "OPTIONS" {
            sendResponse(fd, statusCode: 204, headers: corsHeaders(), body: Data())
            return
        }

        if path.hasPrefix("api/") {
            proxyToBackend(fd, method: method, path: path, headers: request.headers, body: request.body)
            return
        }

        let fullPath = URL(fileURLWithPath: resourcePath).appendingPathComponent(path).path
        let normalizedPath = NSString(string: fullPath).standardizingPath
        let normalizedRoot = NSString(string: resourcePath).standardizingPath

        guard normalizedPath.hasPrefix(normalizedRoot) else {
            sendResponse(fd, statusCode: 403, headers: corsHeaders(), body: "Forbidden".data(using: .utf8) ?? Data())
            return
        }

        if FileManager.default.fileExists(atPath: normalizedPath) {
            let data = FileManager.default.contents(atPath: normalizedPath) ?? Data()
            var headers = corsHeaders()
            headers["Content-Type"] = Self.mimeType(for: normalizedPath)
            sendResponse(fd, statusCode: 200, headers: headers, body: data)
        } else {
            let indexPath = URL(fileURLWithPath: resourcePath).appendingPathComponent("index.html").path
            if let data = FileManager.default.contents(atPath: indexPath) {
                var headers = corsHeaders()
                headers["Content-Type"] = "text/html; charset=utf-8"
                sendResponse(fd, statusCode: 200, headers: headers, body: data)
            } else {
                sendResponse(fd, statusCode: 404, headers: corsHeaders(), body: "Not Found".data(using: .utf8) ?? Data())
            }
        }
    }

    private func proxyToBackend(_ fd: Int32, method: String, path: String, headers: [String: String], body: Data) {
        let urlString = "\(apiBaseURL)/\(path)"
        guard let url = URL(string: urlString) else {
            sendResponse(fd, statusCode: 502, headers: corsHeaders(), body: "Bad Gateway".data(using: .utf8) ?? Data())
            return
        }

        var urlRequest = URLRequest(url: url)
        urlRequest.httpMethod = method
        urlRequest.timeoutInterval = 30

        for (key, value) in headers {
            let lower = key.lowercased()
            // Only forward the headers that matter to the API, and never forward Host/Origin.
            if lower == "content-type" || lower == "authorization" || lower == "accept" {
                urlRequest.setValue(value, forHTTPHeaderField: key)
            }
        }

        if !body.isEmpty {
            urlRequest.httpBody = body
        }

        let sem = DispatchSemaphore(value: 0)
        var responseHeaders: [String: String] = [:]
        var responseBody: Data = Data()
        var responseStatus: Int = 502

        let task = URLSession.shared.dataTask(with: urlRequest) { data, response, _ in
            if let httpResponse = response as? HTTPURLResponse {
                responseStatus = httpResponse.statusCode
                for (key, value) in httpResponse.allHeaderFields {
                    responseHeaders[key as? String ?? ""] = value as? String ?? ""
                }
            }
            responseBody = data ?? Data()
            sem.signal()
        }
        task.resume()
        _ = sem.wait(timeout: .now() + 30)

        var headers = corsHeaders()
        if let ct = responseHeaders["Content-Type"] { headers["Content-Type"] = ct }
        sendResponse(fd, statusCode: responseStatus, headers: headers, body: responseBody)
    }

    private struct HttpRequest {
        let method: String
        let path: String
        let headers: [String: String]
        let body: Data
    }

    private func readHttpRequest(_ fd: Int32) -> HttpRequest? {
        var data = Data()
        var buffer = [UInt8](repeating: 0, count: 16 * 1024)

        func findHeaderEnd(in d: Data) -> Int? {
            let needle = Data([13, 10, 13, 10]) // \r\n\r\n
            guard let range = d.range(of: needle) else { return nil }
            return range.upperBound
        }

        var headerEndIndex: Int? = nil
        for _ in 0..<128 {
            let n = recv(fd, &buffer, buffer.count, 0)
            if n <= 0 { return nil }
            data.append(buffer, count: n)

            if let end = findHeaderEnd(in: data) {
                headerEndIndex = end
                break
            }

            // Prevent unbounded header growth
            if data.count > 512 * 1024 { return nil }
        }

        guard let headerEnd = headerEndIndex else { return nil }
        let headerData = data.prefix(headerEnd)
        let headerStr = String(data: headerData, encoding: .utf8) ?? ""

        let lines = headerStr.components(separatedBy: "\r\n").filter { !$0.isEmpty }
        guard let firstLine = lines.first else { return nil }
        let parts = firstLine.components(separatedBy: " ")
        guard parts.count >= 2 else { return nil }

        let method = parts[0]
        let path = parts[1]

        var headers: [String: String] = [:]
        for line in lines.dropFirst() {
            guard let idx = line.firstIndex(of: ":") else { continue }
            let key = String(line[..<idx]).trimmingCharacters(in: .whitespacesAndNewlines)
            let value = String(line[line.index(after: idx)...]).trimmingCharacters(in: .whitespacesAndNewlines)
            if !key.isEmpty { headers[key] = value }
        }

        var bodyLength = 0
        if let raw = headers.first(where: { $0.key.lowercased() == "content-length" })?.value,
           let parsed = Int(raw.trimmingCharacters(in: .whitespacesAndNewlines)),
           parsed > 0 {
            bodyLength = parsed
        }

        let alreadyHave = data.count - headerEnd
        if bodyLength > alreadyHave {
            let remaining = bodyLength - alreadyHave
            var readSoFar = 0
            while readSoFar < remaining {
                let n = recv(fd, &buffer, min(buffer.count, remaining - readSoFar), 0)
                if n <= 0 { break }
                data.append(buffer, count: n)
                readSoFar += n
            }
        }

        let bodyStart = headerEnd
        let bodyEnd = min(data.count, bodyStart + bodyLength)
        let body = bodyLength > 0 && bodyEnd > bodyStart ? data.subdata(in: bodyStart..<bodyEnd) : Data()

        return HttpRequest(method: method, path: path, headers: headers, body: body)
    }

    private func corsHeaders() -> [String: String] {
        return [
            "Access-Control-Allow-Origin": "*",
            "Access-Control-Allow-Methods": "GET, POST, PUT, PATCH, DELETE, OPTIONS",
            "Access-Control-Allow-Headers": "Content-Type, Authorization, X-Requested-With",
            "Access-Control-Max-Age": "86400"
        ]
    }

    private func sendResponse(_ fd: Int32, statusCode: Int, headers: [String: String], body: Data) {
        let statusText: String
        switch statusCode {
        case 200: statusText = "OK"
        case 400: statusText = "Bad Request"
        case 401: statusText = "Unauthorized"
        case 204: statusText = "No Content"
        case 403: statusText = "Forbidden"
        case 404: statusText = "Not Found"
        case 500: statusText = "Internal Server Error"
        case 502: statusText = "Bad Gateway"
        default: statusText = "Unknown"
        }

        var headerStr = "HTTP/1.1 \(statusCode) \(statusText)\r\n"
        for (key, value) in headers {
            headerStr += "\(key): \(value)\r\n"
        }
        headerStr += "Content-Length: \(body.count)\r\n"
        headerStr += "Connection: close\r\n\r\n"

        var responseData = headerStr.data(using: .utf8) ?? Data()
        responseData.append(body)

        var sent = 0
        while sent < responseData.count {
            let written = send(fd, responseData.withUnsafeBytes { $0.baseAddress!.advanced(by: sent) }, responseData.count - sent, 0)
            if written <= 0 { break }
            sent += written
        }
    }

    static func mimeType(for path: String) -> String {
        let ext = (path as NSString).pathExtension.lowercased()
        switch ext {
        case "html", "htm": return "text/html; charset=utf-8"
        case "js": return "application/javascript; charset=utf-8"
        case "css": return "text/css; charset=utf-8"
        case "json": return "application/json; charset=utf-8"
        case "png": return "image/png"
        case "jpg", "jpeg": return "image/jpeg"
        case "gif": return "image/gif"
        case "svg": return "image/svg+xml"
        case "ico": return "image/x-icon"
        case "woff": return "font/woff"
        case "woff2": return "font/woff2"
        case "ttf": return "font/ttf"
        case "webp": return "image/webp"
        default: return "application/octet-stream"
        }
    }
}
