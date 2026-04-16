import SwiftUI
import WebKit

// Configures NSWindow when the view joins the window hierarchy
// and initializes the floating mini bar manager.
private final class _WindowSetupNSView: NSView {
    var ipcClient: IpcClient?
    private var miniBarManager: FloatingMiniBarManager?

    override func viewDidMoveToWindow() {
        super.viewDidMoveToWindow()
        guard let w = window else { return }
        let appBg = NSColor(red: 10.0/255, green: 12.0/255, blue: 18.0/255, alpha: 1)
        w.backgroundColor = appBg
        w.titlebarAppearsTransparent = true
        w.titleVisibility = .hidden
        w.isMovableByWindowBackground = true
        // Block resizing — remove the resizable bit from the style mask
        w.styleMask.remove(.resizable)
        // Maximise button should not zoom; disable it to avoid a confusing state
        if let zoomButton = w.standardWindowButton(.zoomButton) {
            zoomButton.isEnabled = false
        }

        // Center the window on screen on first launch
        w.center()

        // Initialize the floating mini bar manager (shows bar on minimize)
        if miniBarManager == nil, let client = ipcClient {
            miniBarManager = FloatingMiniBarManager(ipcClient: client, mainWindow: w)
        }
    }
}

private struct WindowSetupView: NSViewRepresentable {
    var ipcClient: IpcClient?

    func makeNSView(context: Context) -> _WindowSetupNSView {
        let view = _WindowSetupNSView()
        view.ipcClient = ipcClient
        return view
    }
    func updateNSView(_ nsView: _WindowSetupNSView, context: Context) {}
}

struct ContentView: View {
    @ObservedObject var ipcClient: IpcClient
    @State private var webView: WKWebView?
    @State private var menuBarController = MenuBarController()

    var body: some View {
        ZStack {
            // Invisible view that configures the NSWindow and sets up the mini bar
            WindowSetupView(ipcClient: ipcClient).frame(width: 0, height: 0)
            WebViewContainer(ipcClient: ipcClient, webView: $webView)
        }
        // Size to 85% of screen, capped at reasonable maximums
        .frame(
            width: min(NSScreen.main.map { $0.visibleFrame.width * 0.85 } ?? 1100, 1400),
            height: min(NSScreen.main.map { $0.visibleFrame.height * 0.85 } ?? 800, 1000)
        )
        .onAppear {
            menuBarController.setupMenuBar(ipcClient: ipcClient)
        }
    }
}

struct WebViewContainer: NSViewRepresentable {
    @ObservedObject var ipcClient: IpcClient
    @Binding var webView: WKWebView?
    static var schemeHandler: TimeTrackSchemeHandler?

    private static func resolveDistURL() -> URL? {
        let fm = FileManager.default

        // 1) Production app bundle: Contents/Resources/dist
        if let bundleDist = Bundle.main.resourceURL?.appendingPathComponent("dist"),
           fm.fileExists(atPath: bundleDist.path) {
            return bundleDist
        }

        // 2) Dev (SwiftPM): run from repo root or package dir
        let cwd = URL(fileURLWithPath: fm.currentDirectoryPath)
        let candidates: [URL] = [
            cwd.appendingPathComponent("dist"),
            cwd.appendingPathComponent("../../ui/timetrack-ui/dist"),
            cwd.appendingPathComponent("../../../ui/timetrack-ui/dist"),
            cwd.appendingPathComponent("../../../../src/ui/timetrack-ui/dist"),
        ].map { $0.standardizedFileURL }

        for url in candidates {
            if fm.fileExists(atPath: url.path) {
                return url
            }
        }

        return nil
    }

    func makeNSView(context: Context) -> WKWebView {
        let config = WKWebViewConfiguration()
        let userContentController = WKUserContentController()

        let bridgeScript = WKUserScript(
            source: Self.bridgeJavaScript,
            injectionTime: .atDocumentStart,
            forMainFrameOnly: true
        )
        userContentController.addUserScript(bridgeScript)

        userContentController.add(context.coordinator, name: "timeTrackBridge")

        config.userContentController = userContentController
        config.preferences.setValue(true, forKey: "developerExtrasEnabled")

        let distURL = Self.resolveDistURL()
        let distExists = distURL != nil

        NSLog("[WebView] resourceURL: %@", Bundle.main.resourceURL?.path ?? "nil")
        NSLog("[WebView] distPath: %@ exists=%d", distURL?.path ?? "nil", distExists ? 1 : 0)

        // Register scheme handler so timetrack://app/api/... proxies to backend
        if let distURL = distURL {
            let handler = TimeTrackSchemeHandler(resourcePath: distURL.path)
            Self.schemeHandler = handler
            config.setURLSchemeHandler(handler, forURLScheme: "timetrack")
            NSLog("[WebView] Scheme handler registered for dist: %@", distURL.path)
        } else {
            NSLog("[WebView] WARNING: dist not found, no scheme handler registered")
        }

        let webView = WKWebView(frame: .zero, configuration: config)
        let appBg = NSColor(red: 10.0/255, green: 12.0/255, blue: 18.0/255, alpha: 1)
        webView.underPageBackgroundColor = appBg
        webView.setValue(false, forKey: "drawsBackground")
        webView.navigationDelegate = context.coordinator
        context.coordinator.webView = webView
        self.webView = webView

        if distExists {
            let url = URL(string: "timetrack://app/")!
            NSLog("[WebView] Loading timetrack://app/")
            webView.load(URLRequest(url: url))
        } else {
            let html = """
            <html><body style="background:#0a0c12;display:flex;justify-content:center;align-items:center;height:100vh;font-family:system-ui;color:#f5f7fb;">
            <div style="text-align:center"><h2>ChronosX</h2><p>UI bundle not found in app resources.</p><p style="font-size:11px;opacity:0.5">\(distURL?.path ?? "no path")</p></div></body></html>
            """
            webView.loadHTMLString(html, baseURL: nil)
        }

        return webView
    }

    func updateNSView(_ nsView: WKWebView, context: Context) {}

    func makeCoordinator() -> Coordinator {
        Coordinator(ipcClient: ipcClient)
    }

    class Coordinator: NSObject, WKScriptMessageHandler, WKNavigationDelegate {
        func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
            NSLog("[WebView] didFinish: %@", webView.url?.absoluteString ?? "nil")
        }
        func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
            NSLog("[WebView] didFail: %@", error.localizedDescription)
        }
        func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
            NSLog("[WebView] didFailProvisionalNavigation: %@", error.localizedDescription)
        }
        func webView(_ webView: WKWebView, didStartProvisionalNavigation navigation: WKNavigation!) {
            NSLog("[WebView] didStart: %@", webView.url?.absoluteString ?? "nil")
        }
        let ipcClient: IpcClient
        weak var webView: WKWebView?

        init(ipcClient: IpcClient) {
            self.ipcClient = ipcClient
            super.init()

            ipcClient.onEvent = { [weak self] event in
                self?.forwardEventToWebView(event)
            }
        }

        func forwardEventToWebView(_ event: IpcEvent) {
            guard let wv = webView else { return }
            // payload is a valid JSON string stored in AnyCodable.string.
            // dispatchFromJson expects a JSON string argument, so embed as a JS string
            // literal by constructing it inside the script via JSON.stringify on the
            // parsed object — avoids needing to escape the raw JSON string.
            let payloadJs: String
            if let jsonStr = event.payload?.value as? String {
                payloadJs = jsonStr  // Already valid JSON — embed as JS value
            } else {
                payloadJs = "null"
            }
            let safeType = event.eventType.replacingOccurrences(of: "'", with: "\\'")
            // Embed payload as a JS literal, then stringify it so timeTrackHandleEvent
            // receives a JSON string (as dispatchFromJson expects).
            let script = """
            (function() {
                var _p = \(payloadJs);
                window.timeTrackHandleEvent('\(safeType)', _p === null ? null : JSON.stringify(_p));
            })();
            """
            Task { @MainActor in
                _ = try? await wv.evaluateJavaScript(script)
            }
        }

        func userContentController(
            _ userContentController: WKUserContentController,
            didReceive message: WKScriptMessage
        ) {
            guard let body = message.body as? [String: Any],
                  let type = body["type"] as? String,
                  let name = body["name"] as? String,
                  let requestId = body["requestId"] as? Int else {
                return
            }

            let payload = body["payload"]

            Task { @MainActor in
                do {
                    let response: IpcResponse
                    if type == "command" {
                        response = try await ipcClient.sendCommand(name, payload: payload)
                    } else {
                        response = try await ipcClient.sendQuery(name, payload: payload)
                    }

                    // response.data is a JSON string (or nil); parse it back to a Foundation object
                    // so JSONSerialization can embed it properly in the response dict.
                    var dataValue: Any = NSNull()
                    if let jsonStr = response.data?.value as? String,
                       let jsonBytes = jsonStr.data(using: .utf8),
                       let parsed = try? JSONSerialization.jsonObject(with: jsonBytes, options: .fragmentsAllowed) {
                        dataValue = parsed
                    }

                    let responseDict: [String: Any] = [
                        "success": response.success,
                        "data": dataValue,
                        "error": response.error ?? NSNull()
                    ]

                    let responseData = try? JSONSerialization.data(withJSONObject: responseDict)
                    let responseJson = responseData.flatMap { String(data: $0, encoding: .utf8) } ?? "{}"

                    let script = "window._timeTrackIpcResponse(\(requestId), \(responseJson))"
                    _ = try? await message.webView?.evaluateJavaScript(script)
                } catch {
                    let errorJson = "{\"success\":false,\"error\":\"\(error.localizedDescription.replacingOccurrences(of: "\"", with: "\\\""))\"}"
                    let script = "window._timeTrackIpcResponse(\(requestId), \(errorJson))"
                    _ = try? await message.webView?.evaluateJavaScript(script)
                }
            }
        }
    }

    static let bridgeJavaScript = """
    (function() {
        var _nextId = 1;
        var _pending = {};

        window._timeTrackIpcResponse = function(requestId, responseJson) {
            var p = _pending[requestId];
            if (p) {
                delete _pending[requestId];
                // React IpcService calls JSON.parse() on the result, so always
                // resolve with a JSON string, not a parsed object.
                if (typeof responseJson === 'string') {
                    p(responseJson);
                } else {
                    p(JSON.stringify(responseJson));
                }
            }
        };

        function ipcCall(type, name, payload) {
            return new Promise(function(resolve) {
                var id = _nextId++;
                _pending[id] = resolve;
                window.webkit.messageHandlers.timeTrackBridge.postMessage({
                    requestId: id,
                    type: type,
                    name: name,
                    payload: payload || null
                });
            });
        }

        window.timeTrackBridge = {
            isConnected: true,
            SendCommand: function(command, payloadJson) {
                var payload = null;
                if (payloadJson) {
                    try { payload = JSON.parse(payloadJson); } catch(e) { payload = payloadJson; }
                }
                return ipcCall('command', command, payload);
            },
            SendQuery: function(query, payloadJson) {
                var payload = null;
                if (payloadJson) {
                    try { payload = JSON.parse(payloadJson); } catch(e) { payload = payloadJson; }
                }
                return ipcCall('query', query, payload);
            }
        };

        window.timeTrackHandleEvent = function(eventType, payloadJson) {
            if (window._timeTrackEventHandlers && window._timeTrackEventHandlers[eventType]) {
                var payload = payloadJson;
                if (typeof payloadJson === 'string') {
                    try { payload = JSON.parse(payloadJson); } catch(e) { payload = payloadJson; }
                }
                window._timeTrackEventHandlers[eventType].forEach(function(cb) { cb(payload); });
            }
        };

        window._timeTrackEventHandlers = {};
        window.timeTrackBridge.subscribeToEvent = function(eventType, callback) {
            if (!window._timeTrackEventHandlers[eventType]) {
                window._timeTrackEventHandlers[eventType] = [];
            }
            window._timeTrackEventHandlers[eventType].push(callback);
            return function() {
                var handlers = window._timeTrackEventHandlers[eventType];
                if (handlers) {
                    var idx = handlers.indexOf(callback);
                    if (idx > -1) handlers.splice(idx, 1);
                }
            };
        };

        console.log('[MacOSDesktopHost] Bridge injected successfully');

        // Rewrite remote API URLs to same-origin timetrack:// scheme so WKURLSchemeHandler
        // proxies them natively — eliminates the CORS "Origin timetrack://app" rejection.
        var _origFetch = window.fetch;
        var _remoteBase = 'https://chronosx-timetrack-api.gpoda0.easypanel.host/api/v1';
        var _localBase = 'timetrack://app/api/v1';
        window.fetch = function(input, init) {
            var url = (typeof input === 'string') ? input : (input instanceof Request ? input.url : String(input));
            if (url.indexOf(_remoteBase) === 0) {
                var newUrl = _localBase + url.substring(_remoteBase.length);
                if (typeof input === 'string') {
                    input = newUrl;
                } else if (input instanceof Request) {
                    input = new Request(newUrl, input);
                }
            }
            return _origFetch.call(window, input, init);
        };
        console.log('[MacOSDesktopHost] API proxy active via timetrack:// scheme');
    })();
    """
}
