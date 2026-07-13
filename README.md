# SpaceFactory

SpaceFactory ist ein entstehendes 2D-Top-down-Fabrik- und Erkundungsspiel im Weltraum. Dieses Repository enthält ein Godot-4.7-.NET-Grundgerüst, einen Godot-unabhängigen C#-Core und eine kleine technische Demo.

## Aktueller Stand

Die Demo zeigt ein per WASD steuerbares Raumschiff mit treibstoffabhängigem Shift-Boost, sichere Drift nach dem Aussteigen und eine Befestigung an grossen Kometen, einen langsameren Astronauten mit träger Beschleunigung, eine folgende Kamera sowie gerichtetes 5×5-Sektorstreaming. Eine nordfeste, anklickbare Minimap und die interaktive Sternenkarte (`M`) teilen sich erkundete Fog-of-War-Daten, Kometenziele und einen Navigationspfeil. Kometen variieren in Form, Größe, Rotation, Gesteinsschichten, Rissen, Erhöhungen und Kratern. Auf ihren Oberflächen liegen 25 deterministisch verteilte Ressourcentypen. Der Astronaut kann sie abbauen und auf grossen beziehungsweise sehr grossen Kometen zwölf Maschinentypen errichten. Ein lokales Stromnetz, 49 Rezepte, Behälterketten, Forschung, Lagerung und persistente Maschinenzustände bilden den ersten vollständigen Fabrikablauf.

## Voraussetzungen und Start

Benötigt werden .NET SDK 8 und Godot 4.7 .NET. In der eingerichteten Arbeitskopie liegen beide Werkzeuge bereits projektlokal unter `.tools`.

### Spiel direkt starten

Im Explorer `Start-SpaceFactory.cmd` doppelt anklicken oder im Projektordner ausführen:

```powershell
.\Start-SpaceFactory.cmd
```

Das Startskript baut das Projekt und öffnet anschließend direkt das Spiel.

### Godot-Editor öffnen

```powershell
.\Start-SpaceFactory.cmd -Editor
```

Im Editor anschließend `F6` oder `F5` drücken. Details und manuelle Befehle stehen in [docs/SETUP.md](docs/SETUP.md).

### Steuerung

- `WASD` oder Pfeiltasten: Raumschiff beziehungsweise Astronaut bewegen
- `E`: das Raumschiff verlassen beziehungsweise nahe der Cockpitkante wieder einsteigen
- `Shift` halten: Raumschiff-Boost mit doppeltem Zieltempo
- `H`: ein langsames, oberflächennahes Raumschiff befestigen oder wieder lösen
- `B`: als Astronaut Baumenü öffnen; beim Platzieren drehen Mausrad, Links- und Rechtsklick die Vorschau beziehungsweise bestätigen oder brechen ab
- `I`: Inventar öffnen oder schließen; im Schiff zusätzlich das Raumschifflager anzeigen
- Linke Maustaste halten: anvisiertes Ressourcenvorkommen in Reichweite abbauen
- `M` oder Linksklick auf die Minimap: interaktive Sternenkarte im Raumschiff öffnen
- `Escape`: Einstellungsmenü öffnen, zurückgehen oder schließen

Tasten können im Einstellungsmenü neu belegt werden. Audio- und Videooptionen werden ebenfalls dauerhaft gespeichert.

### Manuelle Entwicklerbefehle

```powershell
.\.tools\dotnet\dotnet.exe restore
.\.tools\dotnet\dotnet.exe build
.\.tools\dotnet\dotnet.exe test
```

## Struktur

- `src/SpaceFactory.Core`: reine, testbare Spiellogik
- `scripts`: Application, Infrastructure und Godot-Präsentation
- `scenes`: Godot-Szenen
- `data`: datengetriebene Beispielinhalte
- `tests`: Core-Tests
- `docs`: Architektur, Design, Setup und Roadmap

Weitere Details: [Architektur](docs/ARCHITECTURE.md), [Bau und Produktion](docs/FACTORY_SYSTEM.md), [Generierung](docs/PROCEDURAL_GENERATION.md), [Minimap und Sternenkarte](docs/EXPLORATION_MAP.md), [Ressourcen und Abbau](docs/RESOURCE_SYSTEM.md), [Inventar und Lager](docs/INVENTORY.md), [Spieler, Kamera und Raumschiff](docs/PLAYER_AND_SHIP.md), [Einstellungen](docs/SETTINGS.md), [Spielkonzept](docs/GAME_DESIGN.md).
