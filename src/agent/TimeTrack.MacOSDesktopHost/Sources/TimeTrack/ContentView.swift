import Foundation
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
    @State private var devToolsEnabled = false
    @State private var webViewReloadKey = UUID()

    var body: some View {
        ZStack {
            // Invisible view that configures the NSWindow and sets up the mini bar
            WindowSetupView(ipcClient: ipcClient).frame(width: 0, height: 0)
            WebViewContainer(
                ipcClient: ipcClient,
                webView: $webView,
                devToolsEnabled: devToolsEnabled,
                onDevToolsAccessChanged: { enabled in
                    if devToolsEnabled != enabled {
                        devToolsEnabled = enabled
                        webViewReloadKey = UUID()
                    }
                }
            )
            .id(webViewReloadKey)
        }
        // Size to 85% of screen, capped at reasonable maximums
        .frame(
            width: min(NSScreen.main.map { $0.visibleFrame.width * 0.85 } ?? 1100, 1400),
            height: min(NSScreen.main.map { $0.visibleFrame.height * 0.85 } ?? 800, 1000)
        )
        .onAppear {
            menuBarController.setupMenuBar(ipcClient: ipcClient)
        }
        .onChange(of: ipcClient.isConnected) { connected in
            guard connected else { return }
            Task { @MainActor in
                await syncDevToolsFromAgentSettings()
            }
        }
    }

    @MainActor
    private func syncDevToolsFromAgentSettings() async {
        do {
            let resp = try await ipcClient.sendQuery("getSettings")
            guard resp.success else { return }

            // Agent returns `data` as a JSON string in AnyCodable.string.
            guard case .string(let jsonStr) = resp.data else { return }
            guard let data = jsonStr.data(using: .utf8) else { return }
            guard let obj = try JSONSerialization.jsonObject(with: data) as? [String: Any] else { return }

            let enabled = (obj["devToolsEnabled"] as? Bool) ?? false
            let untilRaw = obj["devToolsEnabledUntilUtc"] as? String
            let effectiveEnabled: Bool
            if let untilRaw = untilRaw, let untilDate = ISO8601DateFormatter().date(from: untilRaw) {
                effectiveEnabled = enabled && untilDate.timeIntervalSinceNow > 0
            } else {
                effectiveEnabled = enabled
            }

            if devToolsEnabled != effectiveEnabled {
                devToolsEnabled = effectiveEnabled
                webViewReloadKey = UUID()
            }
        } catch {
            // non-critical
        }
    }
}

struct WebViewContainer: NSViewRepresentable {
    @ObservedObject var ipcClient: IpcClient
    @Binding var webView: WKWebView?
    var devToolsEnabled: Bool
    var onDevToolsAccessChanged: ((Bool) -> Void)?
    static var schemeHandler: TimeTrackSchemeHandler?
    static var localServer: LocalHTTPServer?

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

    private static func injectAppConfig(userContentController: WKUserContentController, overrides: [String: Any]) {
        guard let data = try? JSONSerialization.data(withJSONObject: overrides, options: []),
              let json = String(data: data, encoding: .utf8)
        else { return }

        let script = "window.__APP_CONFIG__ = Object.assign({}, window.__APP_CONFIG__ || {}, \(json));"
        userContentController.addUserScript(WKUserScript(
            source: script,
            injectionTime: .atDocumentStart,
            forMainFrameOnly: true
        ))
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
        config.preferences.setValue(devToolsEnabled, forKey: "developerExtrasEnabled")

        let distURL = Self.resolveDistURL()
        let distExists = distURL != nil

        NSLog("[WebView] resourceURL: %@", Bundle.main.resourceURL?.path ?? "nil")
        NSLog("[WebView] distPath: %@ exists=%d", distURL?.path ?? "nil", distExists ? 1 : 0)

        // Prefer a localhost origin for the UI bundle to avoid WebKit treating the origin as "null"
        // (custom URL schemes can break react-router's hash history with history.replaceState throttling).
        if let distURL = distURL {
            if let server = LocalHTTPServer(resourcePath: distURL.path) {
                Self.localServer = server
                NSLog("[WebView] LocalHTTPServer started at %@", server.baseURL)

                // Provide the UI with a same-origin API base that proxies to production.
                Self.injectAppConfig(userContentController: userContentController, overrides: ["VITE_API_URL": "/api/v1"])
            } else {
                NSLog("[WebView] WARNING: failed to start LocalHTTPServer, falling back to timetrack:// scheme handler")
                let handler = TimeTrackSchemeHandler(resourcePath: distURL.path)
                Self.schemeHandler = handler
                config.setURLSchemeHandler(handler, forURLScheme: "timetrack")
                NSLog("[WebView] Scheme handler registered for dist: %@", distURL.path)
            }
        } else {
            NSLog("[WebView] WARNING: dist not found, no local server or scheme handler registered")
        }

        let webView = WKWebView(frame: .zero, configuration: config)
        let appBg = NSColor(red: 10.0/255, green: 12.0/255, blue: 18.0/255, alpha: 1)
        webView.underPageBackgroundColor = appBg
        webView.setValue(false, forKey: "drawsBackground")
        webView.navigationDelegate = context.coordinator
        context.coordinator.webView = webView
        // The IPC client may connect before the WKWebView exists; ensure we sync
        // the current connection state into the page as soon as the WebView is attached.
        context.coordinator.forwardConnectionStateToWebView(ipcClient.isConnected)
        self.webView = webView

        if distExists {
            let url: URL
            if let server = Self.localServer, let u = URL(string: "\(server.baseURL)/index.html") {
                url = u
                NSLog("[WebView] Loading %@", url.absoluteString)
            } else {
                url = URL(string: "timetrack://app/")!
                NSLog("[WebView] Loading timetrack://app/")
            }

            // Delay to next runloop so the view is in a window before first navigation.
            DispatchQueue.main.async { webView.load(URLRequest(url: url)) }
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
        Coordinator(ipcClient: ipcClient, onDevToolsAccessChanged: onDevToolsAccessChanged)
    }

    class Coordinator: NSObject, WKScriptMessageHandler, WKNavigationDelegate {
        func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
            NSLog("[WebView] didFinish: %@", webView.url?.absoluteString ?? "nil")
            // Re-sync connection state after navigations/reloads so the SPA always
            // receives the latest IPC state even if the initial event was missed.
            forwardConnectionStateToWebView(ipcClient.isConnected)
            // Some SPA code may replace `window.timeTrackHandleEvent` shortly after
            // navigation finishes. Re-send once after a short delay to ensure the
            // React IPC layer receives the event and updates any subscribed state.
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.25) { [weak self] in
                guard let self else { return }
                self.forwardConnectionStateToWebView(self.ipcClient.isConnected)
            }
#if DEBUG
            let debugScript = """
            (function() {
              try {
                var hasBridge = !!window.timeTrackBridge;
                var proto = window.location && window.location.protocol ? window.location.protocol : "unknown";
                var apiBase = (window.__APP_CONFIG__ && window.__APP_CONFIG__.VITE_API_URL) ? window.__APP_CONFIG__.VITE_API_URL : null;
                var storageOk = false;
                try {
                  if (typeof localStorage !== 'undefined') {
                    localStorage.setItem('__tt_ping', '1');
                    localStorage.removeItem('__tt_ping');
                    storageOk = true;
                  }
                } catch(e) {}
                return JSON.stringify({ protocol: proto, hasBridge: hasBridge, localStorageOk: storageOk, apiBase: apiBase });
              } catch(e) {
                return "error:" + (e && e.message ? e.message : String(e));
              }
            })();
            """
            webView.evaluateJavaScript(debugScript) { result, error in
                if let error = error {
                    NSLog("[WebView] debug eval error: %@", error.localizedDescription)
                } else if let s = result as? String {
                    NSLog("[WebView] debug: %@", s)
                } else {
                    NSLog("[WebView] debug: (no string)")
                }
            }

	            DispatchQueue.main.asyncAfter(deadline: .now() + 2.0) {
	                let snapshotScript = """
	                (function() {
	                  try {
	                    var hash = window.location && window.location.hash ? window.location.hash : "";
	                    var title = document.title || "";
	                    var text = (document.body && document.body.innerText) ? document.body.innerText : "";
	                    text = text.replace(/\\s+/g,' ').trim().slice(0, 180);
	                    var root = document.getElementById('root');
	                    var rootChildCount = root ? root.childElementCount : -1;
	                    var rootHtmlLen = root && root.innerHTML ? root.innerHTML.length : 0;
	                    var inputCount = document.querySelectorAll('input').length;
	                    var errors = (window.__TT_ERRORS__ && Array.isArray(window.__TT_ERRORS__)) ? window.__TT_ERRORS__ : [];
	                    var lastError = errors.length ? errors[errors.length - 1] : null;
	                    var authRaw = null;
	                    var authLen = 0;
	                    var authParseOk = false;
	                    try {
	                      authRaw = (typeof localStorage !== 'undefined') ? localStorage.getItem('timetrack-auth') : null;
	                      authLen = authRaw ? authRaw.length : 0;
	                      if (authRaw) {
	                        JSON.parse(authRaw);
	                        authParseOk = true;
	                      }
	                    } catch(e) {}
	                    return JSON.stringify({
	                      hash: hash,
	                      title: title,
	                      bodyText: text,
	                      rootChildCount: rootChildCount,
	                      rootHtmlLen: rootHtmlLen,
	                      inputCount: inputCount,
	                      errorsCount: errors.length,
	                      lastError: lastError,
	                      authLen: authLen,
	                      authParseOk: authParseOk
	                    });
	                  } catch(e) {
	                    return "error:" + (e && e.message ? e.message : String(e));
	                  }
	                })();
	                """
                webView.evaluateJavaScript(snapshotScript) { result, error in
                    if let error = error {
                        NSLog("[WebView] snapshot eval error: %@", error.localizedDescription)
                    } else if let s = result as? String {
                        NSLog("[WebView] snapshot: %@", s)
                    } else {
                        NSLog("[WebView] snapshot: (no string)")
                    }
                }
            }
	#endif

	            // Ensure the webview can receive keyboard input immediately (login form typing).
	            DispatchQueue.main.async {
	                NSApp.activate(ignoringOtherApps: true)
	                webView.window?.makeKeyAndOrderFront(nil)
	                webView.window?.makeFirstResponder(webView)
	            }

	            // Best-effort focus first input (some webviews ignore HTML autofocus).
	            let focusScript = """
	            try {
	              setTimeout(function() {
	                var el = document.querySelector('input[autofocus], input, textarea, [contenteditable=\"true\"]');
	                if (el && el.focus) el.focus();
	              }, 50);
	            } catch (e) {}
	            """
	            webView.evaluateJavaScript(focusScript, completionHandler: nil)
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
        let onDevToolsAccessChanged: ((Bool) -> Void)?
        weak var webView: WKWebView?

        init(ipcClient: IpcClient, onDevToolsAccessChanged: ((Bool) -> Void)?) {
            self.ipcClient = ipcClient
            self.onDevToolsAccessChanged = onDevToolsAccessChanged
            super.init()

            ipcClient.onEvent = { [weak self] event in
                self?.forwardEventToWebView(event)
            }

            ipcClient.onConnectionStateChanged = { [weak self] connected in
                self?.forwardConnectionStateToWebView(connected)
            }
        }

        func forwardConnectionStateToWebView(_ connected: Bool) {
            guard let wv = webView else { return }
            let connectedJs = connected ? "true" : "false"
            let script = """
            (function() {
                try {
                    if (window.timeTrackBridge) { window.timeTrackBridge.isConnected = \(connectedJs); }
                    if (typeof window.timeTrackHandleEvent === 'function') {
                        window.timeTrackHandleEvent('connectionStateChanged', JSON.stringify({ isConnected: \(connectedJs) }));
                    }
                } catch (e) {}
            })();
            """
            Task { @MainActor in
                _ = try? await wv.evaluateJavaScript(script)
            }
        }

        func forwardEventToWebView(_ event: IpcEvent) {
            guard let wv = webView else { return }
            if event.eventType == "devToolsAccessChanged",
               let jsonStr = event.payload?.value as? String,
               let data = jsonStr.data(using: .utf8),
               let obj = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
               let enabled = obj["devToolsEnabled"] as? Bool
            {
                Task { @MainActor [weak self] in
                    self?.onDevToolsAccessChanged?(enabled)
                }
            }
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
        // Basic error capture so we can diagnose "black screen" boots without devtools.
        window.__TT_ERRORS__ = window.__TT_ERRORS__ || [];
        function _ttPush(kind, args) {
            try {
                var arr = window.__TT_ERRORS__;
                var msg = Array.from(args || []).map(function(a) {
                    try { return typeof a === 'object' ? JSON.stringify(a) : String(a); }
                    catch(e) { return String(a); }
                }).join(' ');
                arr.push({ kind: kind, message: msg, ts: Date.now() });
                if (arr.length > 50) arr.shift();
            } catch(e) {}
        }
        try {
            window.addEventListener('error', function(e) {
                _ttPush('window.error', [e.message, e.filename, e.lineno, e.colno, (e.error && e.error.stack) ? e.error.stack : '']);
            });
            window.addEventListener('unhandledrejection', function(e) {
                var r = e && e.reason;
                _ttPush('unhandledrejection', [r && r.stack ? r.stack : String(r)]);
            });
            var _origErr = console.error;
            console.error = function() { _ttPush('console.error', arguments); return _origErr.apply(console, arguments); };
        } catch(e) {}

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
            isConnected: false,
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

        // API base is provided by the host via window.__APP_CONFIG__.VITE_API_URL
        // and points at the embedded localhost proxy.
    })();
    """
}
