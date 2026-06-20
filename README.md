# Kappy Manager

Kappy Manager is a lightweight Windows Task Manager-style desktop application originally built for my laptop with the in-box .NET Framework toolchain.

This app was designed to meet the need for a reliable Task Manager replacement on a machine where Windows Task Manager could not run without constantly crashing.

## Features

- Live process list with CPU, memory, thread count, PID, and descriptions
- Process search, end-task action, and reduced-priority efficiency mode
- CPU and memory history graphs plus memory, network, and uptime statistics
- Startup application inventory from registry and Startup folders
- User/session resource summary
- Detailed process image paths and priorities
- Windows service inventory with start and stop controls
- Run-new-task dialog

## License

Kappy Manager is released under the MIT License.

Copyright (c) 2026 Jonthan Kaplan

## Run

Double-click `Start Kappy Manager.cmd`.

The launcher builds `Kappy Manager.exe` automatically if needed, then starts it. To build manually:

```powershell
.\build.ps1
```

To build and launch:

```powershell
.\build.ps1 -Run
```

## Permissions

The application runs with normal user permissions. Ending elevated or protected processes and changing some services requires running `Kappy Manager.exe` as administrator.

## Compatibility

- Windows 10/11 x64
- .NET Framework 4.x Windows desktop components
- No external packages or network access required
