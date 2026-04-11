import Foundation
import WebKit

class TimeTrackSchemeHandler: NSObject, WKURLSchemeHandler {
    private let resourcePath: String

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
                "Cache-Control": "no-cache"
            ]
            let response = HTTPURLResponse(url: url, statusCode: 200, httpVersion: "HTTP/1.1", headerFields: headers)!
            task.didReceive(response)
            task.didReceive(data)
            task.didFinish()
        } else {
            let indexPath = URL(fileURLWithPath: resourcePath).appendingPathComponent("index.html").path
            if let data = FileManager.default.contents(atPath: indexPath) {
                let headers = ["Content-Type": "text/html; charset=utf-8", "Content-Length": "\(data.count)"]
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

    func webView(_ webView: WKWebView, stop task: WKURLSchemeTask) {}

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
