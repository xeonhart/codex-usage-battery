# Codex Usage Battery

A small Windows desktop widget that shows **remaining Codex usage** as a battery. It stays above other windows and refreshes every two minutes.

This is a standalone companion app. It is not an installed Codex or ChatGPT plugin, and it does not display general ChatGPT message limits.

## Use

1. Install and sign in to the Codex desktop app or CLI.
2. Run `CodexUsageBattery.exe`.
3. Drag the widget to move it, double-click to refresh, or right-click for settings and Exit.
4. Choose **Start with Windows** from the right-click menu to launch it when you sign in. This writes an entry for the current user under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; the same menu item removes it.

The percentage and battery show quota remaining. The reset time follows your Windows local time. If a refresh fails, the widget marks the previous reading as stale.

## How it works

The app launches `codex app-server --stdio` from the local Codex installation, completes the app-server handshake, and calls `account/rateLimits/read`. It uses the Codex account already signed in on this computer. It does not require an OpenAI API key and does not store account tokens.

The app was built for Windows 10/11 with .NET Framework 4.6 or later. Codex must be installed and its CLI executable available under the desktop installation or on `PATH`.

## Build from source

Run `build.ps1` in Windows PowerShell. It uses the .NET Framework C# compiler included with Windows and embeds `CodexUsageBattery.ico` into the resulting executable. No third-party runtime package is needed to build.

`make_icon.py` can recreate the icon with Pillow if you want to edit its design. The committed `.ico` is sufficient for a normal build.

## Files

- `CodexUsageBattery.cs` — widget and Codex app-server client source
- `CodexUsageBattery.ico` — battery icon
- `CodexUsageBattery.exe` — compiled Windows application
- `build.ps1` — build script
- `make_icon.py` — icon generator
- `README.txt` — original quick-start notes

## Source of usage data

OpenAI documents the Codex app-server rate-limit method at https://learn.chatgpt.com/docs/app-server. The meter reflects the limits returned by that method, which may vary by account and time.
