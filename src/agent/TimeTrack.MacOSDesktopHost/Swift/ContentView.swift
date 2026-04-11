import SwiftUI
import WebKit

struct ContentView: View {
    @ObservedObject var ipcClient: IpcClient
    @State private var webView: WKWebView?
    @State private var menuBarController = MenuBarController()

    var body: some View {
        WebViewContainer(ipcClient: ipcClient, webView: $webView)
            .frame(minWidth: 1000, minHeight: 700)
            .onAppear {
                menuBarController.setupMenuBar(ipcClient: ipcClient)
            }
    }
}

struct WebViewContainer: NSViewRepresentable {
    @ObservedObject var ipcClient: IpcClient
    @Binding var webView: WKWebView?

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

        let webView = WKWebView(frame: .zero, configuration: config)
        self.webView = webView

        if let bundlePath = Bundle.main.path(forResource: "dist", ofType: nil) {
            let url = URL(fileURLWithPath: bundlePath).appendingPathComponent("index.html")
            webView.loadFileURL(url, allowingReadAccessTo: URL(fileURLWithPath: bundlePath))
        } else {
            let html = """
            <html><body style="display:flex;justify-content:center;align-items:center;height:100vh;font-family:system-ui;">
            <div style="text-align:center">
            <h2>TimeTrack</h2>
            <p>UI bundle not found. Place the React dist/ in the app bundle.</p>
            </div></body></html>
            """
            webView.loadHTMLString(html, baseURL: nil)
        }

        return webView
    }

    func updateNSView(_ nsView: WKWebView, context: Context) {}

    func makeCoordinator() -> Coordinator {
        Coordinator(ipcClient: ipcClient)
    }

    class Coordinator: NSObject, WKScriptMessageHandler {
        let ipcClient: IpcClient

        init(ipcClient: IpcClient) {
            self.ipcClient = ipcClient
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

                    let responseDict: [String: Any] = [
                        "success": response.success,
                        "data": response.data?.value ?? NSNull(),
                        "error": response.error ?? NSNull()
                    ]

                    let responseData = try? JSONSerialization.data(withJSONObject: responseDict)
                    let responseJson = responseData.flatMap { String(data: $0, encoding: .utf8) } ?? "{}"

                    let script = "window._timeTrackIpcResponse(\(requestId), \(responseJson))"
                    await message.webView?.evaluateJavaScript(script)
                } catch {
                    let errorJson = "{\"success\":false,\"error\":\"\(error.localizedDescription.replacingOccurrences(of: "\"", with: "\\\""))\"}"
                    let script = "window._timeTrackIpcResponse(\(requestId), \(errorJson))"
                    await message.webView?.evaluateJavaScript(script)
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
                if (typeof responseJson === 'string') {
                    try { p(JSON.parse(responseJson)); }
                    catch(e) { p(responseJson); }
                } else {
                    p(responseJson);
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
    })();
    """
}
