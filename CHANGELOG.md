# Changelog

## 0.3.0 – 2026-11-12
- Redesigned UI for clarity: top tabs instead of sidebar, profile status as compact chips in the header, connection table gets most of the space.
- Context side panel replaces stat tiles, top-10 cards and details box: overview (counts, most blocked processes, top remote addresses; click filters the list) or structured details of the selected connection with quick rule actions and raw line.
- Pause banner, empty states, monospace for IPs/ports, status/direction as dot/arrow instead of badges.
- Rules page: direction filter, status dot, program file name with full path as tooltip, context menu.
- Settings page with sections (appearance, logging incl. log path, about).
- Themed message dialog replaces Windows MessageBox; rule dialog shows validation errors inline.
- Calmer neutral color palettes for light and dark.
- Auto-scroll pauses while an entry is selected.

## 0.2.1 – 2026-11-12
- Fix: search/filter now matches `ip:port` as shown in the table (IP and port columns).

## 0.2.0 – 2026-11-12
- New modern UI: own Fluent-style theme (cards, rounded controls, switches, badges, slim scrollbars), side navigation, stat tiles.
- Selectable color scheme (Light / Dark / System), stored in `%AppData%\FirewallMonitor\settings.json`; title bar follows the theme.
- Restyled rule dialog; rule status as badge.

## 0.1.0 – 2026-11-12
- Initial version: profile status/toggle, live log tracking, pause mode, filter/top-10/detail/CSV export, rule management and quick rule creation.
