# ChronosX Releases

## Latest release: v1.0.88

- [Download ChronosX 1.0.88 for Windows x64](https://github.com/CyriusX/ChronosX-Releases/releases/download/v1.0.88/ChronosX-Setup-1.0.88.exe)
- SHA-256: `127db372f297b35fd7394952113f9e5cca8e60b63d1ed383228c193326856c2e`
- Size: `100871909` bytes
- Source commit: `30552d31c766706ec1e8d9df16f6f8bfc96f2011`
- [Build and browser tests](https://github.com/CyriusX/TimeTracking/actions/runs/44519806999)
- [Installed application E2E on Windows](https://github.com/CyriusX/TimeTracking/actions/runs/44535868478)

Password setup, recovery and changes happen on the web. The desktop app opens the
web account page, refuses temporary-password sessions and preserves the mandatory
password-change flag when restoring a session.

Validation: 261 agent tests, 95 UI tests, 24 Chromium/WebKit scenarios and the
installed WebView2 application with real agent IPC. UI tests simulate API responses
and capture external browser opening. Password integration was tested separately
against real JWT, BCrypt and PostgreSQL.

Compatible API, frontend and credential-version migration are required. Public API
and frontend routes were verified on 20 November 2026. Installing this client does
not deploy the backend. The shared 512 MiB local buffer remains available; a full
buffer pauses new collection and does not guarantee a fixed number of days.

See the [release notes and validation evidence](https://github.com/CyriusX/ChronosX-Releases/releases/tag/v1.0.88).
