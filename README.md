# SpaceFactory

SpaceFactory ist ein entstehendes 2D-Top-down-Fabrik- und Erkundungsspiel im Weltraum. Dieses Repository enthält ein Godot-4.7-.NET-Grundgerüst, einen Godot-unabhängigen C#-Core und eine kleine technische Demo.

## Aktueller Stand

Die Demo zeigt ein per WASD steuerbares Raumschiff, einen langsameren Astronauten mit träger Beschleunigung, eine folgende Kamera, deterministisch erzeugte und kollidierbare Kometen, gerichtetes 5×5-Sektorstreaming, Debuganzeige, Sternenkarte (`M`) und ein vollständiges Einstellungsmenü (`Escape`). Kometen variieren in Form, Größe, Rotation, Gesteinsschichten, Rissen, Erhöhungen und Kratern. Auf ihren Oberflächen liegen 25 deterministisch verteilte Ressourcentypen, die der Astronaut abbauen und in einem begrenzten Inventar sammeln kann. Abgebaute Vorkommen bleiben auch nach dem Entladen ihres Sektors erschöpft. Fabrikbau und echtes Landen sind noch nicht implementiert.

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
- `F`: das Raumschiff verlassen
- `E`: in der Nähe wieder in das Raumschiff einsteigen
- Linke Maustaste halten: anvisiertes Ressourcenvorkommen in Reichweite abbauen
- `M`: Sternenkarte öffnen oder schließen
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

Weitere Details: [Architektur](docs/ARCHITECTURE.md), [Generierung](docs/PROCEDURAL_GENERATION.md), [Ressourcen und Abbau](docs/RESOURCE_SYSTEM.md), [Einstellungen](docs/SETTINGS.md), [Spielkonzept](docs/GAME_DESIGN.md).
