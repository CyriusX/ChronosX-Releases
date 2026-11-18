# ChronosX Releases

Public release assets for the ChronosX desktop app.

## Latest release

- Version: `v1.0.87`
- Installer: `ChronosX-Setup-1.0.87.exe` (Windows x64)
- Test backend: `https://chronosx-dev-timetrack-api.gpoda0.easypanel.host`
- SHA-256: `8d71bf3936aff82c2d8adabb84fff9fdb7fef709cd99102d4ded1fbb3e9e7790`
- Size: `100850058` bytes
- Download: [ChronosX 1.0.87](https://github.com/CyriusX/ChronosX-Releases/releases/download/v1.0.87/ChronosX-Setup-1.0.87.exe)
- Source commit: `f80ddd143f2031bc1f93b7be9513546c558cf423`
- Build: [Windows installer](https://github.com/CyriusX/TimeTracking/actions/runs/44372397714)

This version preserves pending screenshots/data in a shared 512 MiB local buffer
when cloud capacity is full, pauses new collection when the buffer fills, and
resumes uploads/tracking after paid capacity becomes available. It also includes
storage warnings and the one-time capacity purchase interface.

**Deploy the compatible backend and storage migrations before distributing this
agent.** Configure the Stripe prices and signed webhooks in the matching environment.
Installing this client does not deploy the backend or enable live payments.

See the [release notes](https://github.com/CyriusX/ChronosX-Releases/releases/tag/v1.0.87)
for limitations and validation. A full local buffer pauses new collection; it does
not guarantee recording a fixed number of days.
