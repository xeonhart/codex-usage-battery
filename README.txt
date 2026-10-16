Codex Usage Battery for Windows
================================

Open CodexUsageBattery.exe. The widget stays above other windows and refreshes your Codex usage every two minutes. The battery and percentage show quota remaining; the line below shows the next reset in your Windows local time.

Double-click the widget to refresh. Drag it to move it. Right-click for Refresh now, Always on top, Start with Windows, and Exit.

Requirements: Windows 10/11, Codex desktop or CLI installed, and a signed-in Codex account. The widget reads usage through the Codex app-server on your own computer. It does not need an OpenAI API key and does not store account tokens.

The executable is a standalone companion widget, not an installed Codex/ChatGPT plugin. To remove it, exit the widget, turn off Start with Windows if enabled, then delete the executable.

Source: CodexUsageBattery.cs. Build with the .NET Framework 4 C# compiler:
csc.exe /target:winexe /platform:anycpu /optimize+ /win32icon:CodexUsageBattery.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll CodexUsageBattery.cs
