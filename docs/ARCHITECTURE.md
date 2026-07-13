# Architektur

```mermaid
flowchart TD
    Presentation[Godot Presentation] --> Application[Application Use Cases]
    Infrastructure[Infrastructure Adapters] --> Application
    Application --> Core[SpaceFactory.Core]
    Infrastructure --> Core
```

`SpaceFactory.Core` enthält reine C#-Domänenlogik und kennt keine Godot-Typen. Application koordiniert künftig Laden, Landen, Transfers, Bauen und Speichern. Infrastructure übernimmt JSON, Dateizugriffe, Persistenz und Generatoradapter. Presentation enthält Nodes, Szenen, Eingabe, Kamera und UI. Abhängigkeiten zeigen nach innen zum Core.

```mermaid
sequenceDiagram
    participant Ship
    participant Root as GameRoot
    participant Generator
    Ship->>Root: betritt neue Sektorkoordinate
    Root->>Generator: Seed + Koordinate + Einstellungen
    Generator-->>Root: GeneratedSectorContent
    Root->>Root: nahe 5x5 aktiv laden, entfernte entladen
    Root->>Root: 14.000 Einheiten als leichte Kartendaten scannen
```

Weltänderungen werden als Deltas über der aus dem Seed rekonstruierten Grundwelt gespeichert. Schiff- und On-Foot-Steuerung bleiben getrennte Zustände. Fabrikmodelle, Rezepte, Inventartransaktionen, Energie, Forschung und Treibstoff liegen im Godot-unabhängigen Core. `FactoryRuntimeController` aktiviert davon nur die aktuell geladenen Kometennetze; `MachineView` und die Menüs visualisieren den Zustand. Der versionierte Infrastructure-Adapter speichert Maschinen-, Forschungs- und Tank-Snapshots unter `user://factory_state.json`.
