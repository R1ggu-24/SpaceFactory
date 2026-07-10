# SpaceFactory

SpaceFactory ist ein entstehendes 2D-Top-down-Fabrik- und Erkundungsspiel im Weltraum. Dieses Repository enthält ein Godot-4.7-.NET-Grundgerüst, einen Godot-unabhängigen C#-Core und eine kleine technische Demo.

## Aktueller Stand

Die Demo zeigt ein per WASD steuerbares Raumschiff, eine folgende Kamera, deterministisch erzeugte Asteroiden, 3×3-Sektorstreaming, Debuganzeige, Sternenkarte (`M`) und Pause (`Escape`). Fabrikbau, Landen und Speichern sind noch nicht implementiert.

## Voraussetzungen und Start

Das Repository bringt projektlokale Werkzeuge nicht mit. Benötigt werden .NET SDK 8 und Godot 4.7 .NET. Details stehen in [docs/SETUP.md](docs/SETUP.md).

```powershell
dotnet restore
dotnet build
dotnet test
godot --path . --editor
```

Alternativ `project.godot` im Godot-.NET-Editor öffnen und `F6`/`F5` drücken.

## Struktur

- `src/SpaceFactory.Core`: reine, testbare Spiellogik
- `scripts`: Application, Infrastructure und Godot-Präsentation
- `scenes`: Godot-Szenen
- `data`: datengetriebene Beispielinhalte
- `tests`: Core-Tests
- `docs`: Architektur, Design, Setup und Roadmap

Weitere Details: [Architektur](docs/ARCHITECTURE.md), [Generierung](docs/PROCEDURAL_GENERATION.md), [Spielkonzept](docs/GAME_DESIGN.md).
