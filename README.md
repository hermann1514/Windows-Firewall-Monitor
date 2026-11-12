# Firewall Monitor

WPF-Tool (.NET 8) für die Windows Firewall: Live-Ansicht von Verbindungen (zugelassen/blockiert), Analyse und Konfiguration.

## Funktionen
- **Kopfzeile:** Status der Profile Domäne/Privat/Öffentlich; Klick auf ein Profil schaltet es um (Ausschalten mit Bestätigung)
- **Verbindungen:** Live-Tracking aus `pfirewall.log` (FileSystemWatcher + Poll-Fallback), neueste Einträge oben, Auto-Scroll abschaltbar
- **Pause/Fortsetzen** ohne Datenverlust; ein Hinweisbalken zeigt, wie viele Einträge warten
- **Suche** über alle Spalten oder eine gewählte Spalte (Prozess, IP, Port, …), Filter Alle/Zugelassen/Blockiert und Alle/Ein/Aus (Richtung), CSV-Export der aktuellen Ansicht
- **Seitenleiste:** ohne Auswahl eine Übersicht (Zähler, meist blockierte Prozesse, häufigste Remote-Adressen – Klick filtert die Liste); mit Auswahl die Details der Verbindung inkl. Rohzeile und Regel-Schnellaktionen
- **Regeln:** auflisten, nach Richtung filtern, anlegen, aktivieren/deaktivieren, löschen (auch per Rechtsklick)

## Design
Eigenes, zurückhaltendes Theme ohne zusätzliche NuGet-Pakete (`Themes/Colors.*.xaml`, `Themes/Controls.xaml`): neutrale Farben, Farbe nur für Bedeutung (rot = blockiert), IPs/Ports in Monospace, eigene Dialoge statt Windows-MessageBox. Hell/Dunkel/System wählbar unter *Einstellungen*; Wechsel zur Laufzeit. Programmsymbol: `Assets/app.ico` (Schild mit Aktivitätslinie, 16–256 px).

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
