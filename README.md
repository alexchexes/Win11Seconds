# Win11Seconds

**Bring back the seconds to your Windows 11 tray — on click.**

* [Why](#why)
* [Features](#features)
* [Usage](#usage)
* [Interaction](#interaction)
* [Troubleshooting](#troubleshooting)
* [Development](#dev)
* [Buy me a coffee](#buy-me-a-coffee)

<img width=600 src=https://github.com/user-attachments/assets/0340d295-7dd8-4a88-8f81-2eb0390d5066 />

<img width=600 src=https://github.com/user-attachments/assets/e60e5e9c-dbc2-4a94-8e75-4883247cb4fa />

## Why

Microsoft removed a very simple but useful feature from Windows 11: clicking on the system clock in the tray no longer shows the clock with seconds ticking in real time. Nobody has any idea why they would do that (except, perhaps, their ignorance) but people really miss it:

* https://answers.microsoft.com/en-us/windows/forum/all/how-can-i-show-time-with-seconds-when-clicking-on/5702b0c4-8009-4332-80f8-b636a2279ab8

* https://www.reddit.com/r/Windows11/comments/1bt6qs2/i_know_this_is_dumb_but_does_anyone_else_miss_the/

Yes, there is [a way](https://www.elevenforum.com/t/turn-on-or-off-show-seconds-in-system-tray-clock-in-windows-11.10591/) to add seconds to the Windows clock right in the system tray (so you always see them, even without clicking), but as you can see from the discussions above, not everyone wants that — seconds are usually needed in a specific moment, not always.

Hence, this simple utility brings back the ability to “click in tray and see seconds” to your Windows 11 — without bloating you with features you likely don’t need, which more advanced programs usually include.

## Features

- Extremely lightweight: **200 KB** single `.exe` file
- Minimal CPU & memory footprint: uses **8 MB of RAM** and nearly zero CPU while in the background
- Click the tray icon and see the `HH:mm:ss` ticking in real time
- Dark/light theme auto-detect
- Resizable, drag-to-move, double-click to maximize/unmaximize (looks like a full-screen clock, supports any aspect ratio display, even vertical)
- Remembers last position/size
- Popup is "always-on-top" when activated

## Usage

1. Make sure you have the [.NET Desktop Runtime 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed (you can check by running `dotnet --list-runtimes` in CMD — must be version 8.x.x, not 6, 7, or 9). If not, install it — it's just a 50 MB download and many other apps use it.
2. Download the `Win11Seconds.exe` file [from the latest release](https://github.com/alexchexes/Win11Seconds/releases/latest/download/Win11Seconds.exe).  
3. Run it. Sit tight. The icon appears in your Windows 11 tray in a second. Now click it to see the seconds!

Optionally, to make the program auto-start with Windows, add a shortcut to the file in one of these folders:
* `%userprofile%\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup` (Win+R → `shell:startup`) — for your user only
* `%ProgramData%\Microsoft\Windows\Start Menu\Programs\Startup` (Win+R → `shell:common startup`) — for all users

## Interaction

- **Left-click tray icon**: Show/hide popup clock (with seconds)  
- **Double-click popup clock**: Maximize/Unmaximize  
- **Right-click tray icon or popup clock**: Show context menu  
- **Hover over the top right corner of the popup clock**: Show "Close" button

## Troubleshooting

- If something doesn't work, make sure [.NET Desktop Runtime 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) is installed and that your Windows 11 installation is up to date.
- If you have any problems, let me know [by creating issue](https://github.com/alexchexes/Win11Seconds/issues)

# Dev

- Install the [.NET SDK](https://dotnet.microsoft.com/en-us/download) (version 8 LTS)

```powershell
# clone repo and navigate to it

# to start program
dotnet run

# to create .exe file
dotnet publish -c Release -r win-x64 --self-contained=false /p:PublishSingleFile=true
```

# Buy me a coffee

<a href="https://ko-fi.com/alexchexes">
  <img src=https://github.com/user-attachments/assets/59c35381-4cb5-472a-a730-15dbe76862eb width=100px />
</a>

https://ko-fi.com/alexchexes
