import Foundation
import WebKit

class TimeTrackSchemeHandler: NSObject, WKURLSchemeHandler {
    private let resourcePath: String
    private let apiBaseURL = "https://chronosx-timetrack-api.gpoda0.easypanel.host"
    private var activeTasks: [ObjectIdentifier: URLSessionDataTask] = [:]
    private let lock = NSLock()

    init(resourcePath: String) {
        self.resourcePath = resourcePath
    }

    func webView(_ webView: WKWebView, start task: WKURLSchemeTask) {
        guard let url = task.request.url else {
            task.didFailWithError(URLError(.badURL))
            return
        }

        var path = url.path
        if path.hasPrefix("/") { path = String(path.dropFirst()) }
        if path.isEmpty { path = "index.html" }

        NSLog("[Scheme] Request: %@ → path=%@", url.absoluteString, path)

        // Proxy API requests natively — same origin (timetrack://app/api/...) so no CORS
        if path.hasPrefix("api/") {
            proxyToBackend(task: task, url: url, path: path)
            return
        }

        serveStaticFile(task: task, url: url, path: path)
    }

    func webView(_ webView: WKWebView, stop task: WKURLSchemeTask) {
        let key = ObjectIdentifier(task)
        lock.lock()
        let sessionTask = activeTasks.removeValue(forKey: key)
        lock.unlock()
        sessionTask?.cancel()
    }

    // MARK: - API Proxy

    private func proxyToBackend(task: WKURLSchemeTask, url: URL, path: String) {
        var components = URLComponents(string: "\(apiBaseURL)/\(path)")
        if let query = url.query { components?.query = query }

        guard let backendURL = components?.url else {
            task.didFailWithError(URLError(.badURL))
            return
        }

        var request = URLRequest(url: backendURL)
        request.httpMethod = task.request.httpMethod ?? "GET"
        request.timeoutInterval = 30

        // Forward safe headers
        let forwardHeaders = ["content-type", "authorization", "accept", "x-requested-with"]
        for (key, value) in task.request.allHTTPHeaderFields ?? [:] {
            if forwardHeaders.contains(key.lowercased()) {
                request.setValue(value, forHTTPHeaderField: key)
            }
        }

        // httpBody may be nil in WKURLSchemeTask — fall back to httpBodyStream
        if let body = task.request.httpBody, !body.isEmpty {
            request.httpBody = body
        } else if let stream = task.request.httpBodyStream {
            request.httpBody = readStream(stream)
        }

        let sessionTask = URLSession.shared.dataTask(with: request) { [weak self, weak task] data, response, error in
            guard let task = task else { return }

            defer {
                if let self = self {
                    let key = ObjectIdentifier(task)
                    self.lock.lock()
                    self.activeTasks.removeValue(forKey: key)
                    self.lock.unlock()
                }
            }

            if let error = error {
                task.didFailWithError(error)
                return
            }

            guard let httpResponse = response as? HTTPURLResponse else {
                task.didFailWithError(URLError(.badServerResponse))
                return
            }

            var headers: [String: String] = [:]
            for (key, value) in httpResponse.allHeaderFields {
                if let k = key as? String, let v = value as? String {
                    headers[k] = v
                }
            }

            let proxiedResponse = HTTPURLResponse(
                url: task.request.url!,
                statusCode: httpResponse.statusCode,
                httpVersion: "HTTP/1.1",
                headerFields: headers
            )!

            task.didReceive(proxiedResponse)
            task.didReceive(data ?? Data())
            task.didFinish()
        }

        let key = ObjectIdentifier(task)
        lock.lock()
        activeTasks[key] = sessionTask
        lock.unlock()
        sessionTask.resume()
    }

    private func readStream(_ stream: InputStream) -> Data {
        var data = Data()
        let bufferSize = 4096
        let buffer = UnsafeMutablePointer<UInt8>.allocate(capacity: bufferSize)
        defer { buffer.deallocate() }
        stream.open()
        while stream.hasBytesAvailable {
            let read = stream.read(buffer, maxLength: bufferSize)
            if read > 0 { data.append(buffer, count: read) }
        }
        stream.close()
        return data
    }

    // MARK: - Static File Serving

    private func serveStaticFile(task: WKURLSchemeTask, url: URL, path: String) {
        let fullPath = URL(fileURLWithPath: resourcePath).appendingPathComponent(path).path
        let normalizedPath = NSString(string: fullPath).standardizingPath
        let normalizedRoot = NSString(string: resourcePath).standardizingPath

        guard normalizedPath.hasPrefix(normalizedRoot) else {
            let response = HTTPURLResponse(url: url, statusCode: 403, httpVersion: "HTTP/1.1", headerFields: nil)!
            task.didReceive(response)
            task.didFinish()
            return
        }

        if FileManager.default.fileExists(atPath: normalizedPath) {
            let data = FileManager.default.contents(atPath: normalizedPath) ?? Data()
            let contentType = Self.mimeType(for: normalizedPath)
            let headers = [
                "Content-Type": contentType,
                "Content-Length": "\(data.count)",
                "Cache-Control": "no-cache",
                // Required for WKWebView to load ES modules (type="module" crossorigin)
                // from a custom URL scheme without silently failing.
                "Access-Control-Allow-Origin": "*"
            ]
            let response = HTTPURLResponse(url: url, statusCode: 200, httpVersion: "HTTP/1.1", headerFields: headers)!
            task.didReceive(response)
            task.didReceive(data)
            task.didFinish()
        } else {
            // SPA fallback: serve index.html for unknown routes
            let indexPath = URL(fileURLWithPath: resourcePath).appendingPathComponent("index.html").path
            if let data = FileManager.default.contents(atPath: indexPath) {
                let headers = [
                    "Content-Type": "text/html; charset=utf-8",
                    "Content-Length": "\(data.count)",
                    "Access-Control-Allow-Origin": "*"
                ]
                let response = HTTPURLResponse(url: url, statusCode: 200, httpVersion: "HTTP/1.1", headerFields: headers)!
                task.didReceive(response)
                task.didReceive(data)
                task.didFinish()
            } else {
                let response = HTTPURLResponse(url: url, statusCode: 404, httpVersion: "HTTP/1.1", headerFields: nil)!
                task.didReceive(response)
                task.didFinish()
            }
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
