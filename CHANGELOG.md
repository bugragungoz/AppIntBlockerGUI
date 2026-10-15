# Changelog

## 2.0.0 - 2026-10-15

> **Not fully tested yet.** This version builds and its tests pass on Windows, but it has not yet
> been run against a real Windows Firewall. Try it in Windows Sandbox or a virtual machine first.

A rebuild around one job: the AppIntBlocker PowerShell script, with a window.

### Changed

- Two pages: **Block** (choose a folder, check its `.exe` files, block them) and **Blocked apps**
  (see, remove and back up the rules).
- Only `.exe` files get rules. A rule for a DLL never matches any traffic, because Windows Firewall
  matches the program of the process that opens a connection.
- Rule names match AppIntBlocker.ps1, so the app and the script see and remove each other's rules.
- Firewall changes go through one PowerShell script run by Windows PowerShell, with no temporary
  files and no user text on a command line.
- New look on the Bugra design system, with a dark and a light theme that follow the Windows app
  setting.

### Added

- A backup of the firewall before blocking (on by default) and on request; restore it with
  `netsh advfirewall import`.
- A check that refuses a whole drive, the Windows folder and folders such as Program Files itself.
- A confirmation before rules are replaced or removed.
- Tests, including runs of the firewall script in Windows PowerShell 5.1 and PowerShell 7 against
  stand-in cmdlets.

### Removed

- The network monitor, restore points, Windows Firewall overview and settings pages, the ASCII-art
  terminal, and the Npcap requirement.
- 17 of 18 NuGet packages; CommunityToolkit.Mvvm is the one left.
- The bundled copy of Sniffnet and the old documentation pages.

## Earlier versions

- 1.2.0: network monitor with per-process bandwidth graphs and blocking from the monitor.
- 1.1.0: cancellation of long operations, unit tests for the firewall service, a CI pipeline.
- 1.0.0: first version, with application blocking, rule management, restore points and settings.
