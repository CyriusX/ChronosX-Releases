import Foundation

class LocalHTTPServer {
    private var socketFd: Int32 = -1
    private(set) var port: UInt16 = 0
    private(set) var baseURL: String = ""
    private var resourcePath: String = ""
    private let apiBaseURL = "https://chronosx-timetrack-api.gpoda0.easypanel.host"

    init?(resourcePath: String) {
        self.resourcePath = resourcePath

        socketFd = Darwin.socket(AF_INET, SOCK_STREAM, 0)
        guard socketFd >= 0 else { return nil }

        var reuse: Int32 = 1
        setsockopt(socketFd, SOL_SOCKET, SO_REUSEADDR, &reuse, socklen_t(MemoryLayout.size(ofValue: reuse)))

        var addr = sockaddr_in()
        addr.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        addr.sin_family = sa_family_t(AF_INET)
        addr.sin_port = UInt16(0).bigEndian
        addr.sin_addr.s_addr = INADDR_ANY

        let bindResult = withUnsafePointer(to: &addr) { ptr in
            ptr.withMemoryRebound(to: sockaddr.self, capacity: 1) { rebound in
                Darwin.bind(socketFd, rebound, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }

        guard bindResult == 0 else {
            Darwin.close(socketFd)
            return nil
        }

        var addrLen = socklen_t(MemoryLayout<sockaddr_in>.size)
        var assignedAddr = sockaddr_in()
        withUnsafeMutablePointer(to: &assignedAddr) { ptr in
            ptr.withMemoryRebound(to: sockaddr.self, capacity: 1) { rebound in
                getsockname(socketFd, rebound, &addrLen)
            }
        }
        port = assignedAddr.sin_port.bigEndian
        baseURL = "http://localhost:\(port)"

        Darwin.listen(socketFd, 10)

        DispatchQueue.global(qos: .background).async { [weak self] in
            self?.acceptLoop()
        }
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

        var buffer = [UInt8](repeating: 0, count: 65536)
        let bytesRead = recv(fd, &buffer, 65536, 0)
        guard bytesRead > 0 else { return }

        let requestStr = String(bytes: buffer[..<bytesRead], encoding: .utf8) ?? ""
        let lines = requestStr.components(separatedBy: "\r\n")
        guard let firstLine = lines.first else { return }
        let parts = firstLine.components(separatedBy: " ")
        guard parts.count >= 2 else { return }

        let method = parts[0]
        var path = parts[1]
        if path.hasPrefix("/") { path = String(path.dropFirst()) }
        if path.isEmpty { path = "index.html" }

        if method == "OPTIONS" {
            sendResponse(fd, statusCode: 204, headers: corsHeaders(), body: Data())
            return
        }

        if path.hasPrefix("api/") {
            proxyToBackend(fd, method: method, path: path, request: requestStr)
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

    private func proxyToBackend(_ fd: Int32, method: String, path: String, request: String) {
        let urlString = "\(apiBaseURL)/\(path)"
        guard let url = URL(string: urlString) else {
            sendResponse(fd, statusCode: 502, headers: corsHeaders(), body: "Bad Gateway".data(using: .utf8) ?? Data())
            return
        }

        var urlRequest = URLRequest(url: url)
        urlRequest.httpMethod = method
        urlRequest.timeoutInterval = 30

        let lines = request.components(separatedBy: "\r\n")
        for line in lines.dropFirst() {
            if line.isEmpty { break }
            let headerParts = line.components(separatedBy: ": ")
            if headerParts.count == 2 {
                let key = headerParts[0]
                let value = headerParts[1]
                if key.lowercased() == "content-type" || key.lowercased() == "authorization" {
                    urlRequest.setValue(value, forHTTPHeaderField: key)
                }
            }
        }

        let bodyStart = request.components(separatedBy: "\r\n\r\n")
        if bodyStart.count > 1 && !bodyStart[1].isEmpty {
            urlRequest.httpBody = bodyStart[1].data(using: .utf8)
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
        case 204: statusText = "No Content"
        case 403: statusText = "Forbidden"
        case 404: statusText = "Not Found"
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
