# CLAUDE.md

## Arbeitsweise (Token sparen)
- Antworte kurz und direkt. Kein Einleitungs- oder Schlusstext, keine Wiederholung meiner Frage.
- Erkläre nur, wenn ich danach frage. Bei Code: erst der Code, dann maximal 1–2 Sätze.
- Gib bei Änderungen nur den geänderten Teil aus (Edit/Diff), nie die ganze Datei.
- Lies nur Dateien, die für die Aufgabe nötig sind. Nutze Grep/Glob gezielt, statt Verzeichnisse komplett zu durchsuchen.
- Lies große Dateien nur abschnittsweise (offset/limit).
- Lies eine Datei nicht erneut, wenn sie sich nicht geändert hat.
- Starte keine Subagenten, außer ich verlange es ausdrücklich.
- Keine ungefragten Refactorings, Zusatzfeatures, Tests oder Dokumentation.
- Bei unklaren Anforderungen: eine kurze Rückfrage, bevor du viel Code schreibst.
- Bei Fehlern: nur relevante Zeilen der Ausgabe betrachten (z. B. mit `tail`/`grep`), nicht den ganzen Log.

## Sprache
- Antworte auf Deutsch. Code, Variablennamen und Commit-Messages auf Englisch.

## Projekt
- Zweck: DIese Tool soll eine Liveansicht der Windows Firewall machen. Also aktuellen traffice anzeigen und welche Ports IP geblockt werden.
- Struktur: Änderungen in eine chnagelog.md schreiben und auch die readme aktuelle halten. 

## Code-Konventionen
- Folge dem bestehenden Stil im Projekt.
- Kleine, fokussierte Änderungen. Keine neuen Abhängigkeiten ohne Rückfrage.
- Keine Secrets, `.env`-Dateien oder Zugangsdaten lesen oder ausgeben.

## Nicht anfassen
- `node_modules/`, `.venv/`, `dist/`, `build/`, Lock-Dateien, generierte Dateien.

## Git
- Nur committen, wenn ich es sage. Kurze Commit-Messages im Imperativ.
