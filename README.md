# Firewall Monitor

WPF-Tool (.NET 8) für die Windows Firewall: Live-Ansicht von Verbindungen (zugelassen/blockiert), Analyse und Konfiguration.

## Funktionen
- Status der Profile Domäne/Privat/Öffentlich (grün/rot), einzeln ein-/ausschaltbar
- Live-Tracking aus `pfirewall.log` (FileSystemWatcher + Poll-Fallback), neueste Einträge oben, Auto-Scroll abschaltbar
- Pause/Fortsetzen ohne Datenverlust (Einträge werden zwischengespeichert)
- Filter (Spalte + Freitext, Aktion), Top-10 blockierte Prozesse / Remote-IPs, Detailpanel, CSV-Export der gefilterten Ansicht
- Regeln auflisten, anlegen, aktivieren/deaktivieren, löschen; Rechtsklick im Live-Grid → Regel für Prozess/IP

## Voraussetzungen
- Windows 10/11, .NET 8 SDK (`winget install Microsoft.DotNet.SDK.8`)
- Administratorrechte (Manifest `requireAdministrator`)

## Build & Start
```powershell
dotnet build src/FirewallMonitor
# Aus einer Administrator-Konsole:
dotnet run --project src/FirewallMonitor
```
`dotnet run` aus einer nicht-erhöhten Konsole schlägt wegen des Manifests mit "requires elevation" fehl; dann `FirewallMonitor.exe` direkt starten (UAC-Dialog).

## Einschränkungen
- `pfirewall.log` enthält keinen Prozess und keinen Regelnamen. Der Prozess wird best effort über die Socket-Tabellen (Port → PID) ermittelt; kurzlebige/blockierte Eingangsverbindungen bleiben "—".
- Beim ersten Start fragt das Tool, ob das Logging aktiviert werden soll (`netsh advfirewall set allprofiles logging …`).
- Regeln werden über den Namen angesprochen; bei doppelten Namen wirkt eine Aktion nur auf die erste Regel.

## Struktur
`Services/` (FirewallService, LogWatcherService, PfLogParser, ProcessResolver, CsvExporter) · `ViewModels/` (MVVM) · `Views/` (WPF)
