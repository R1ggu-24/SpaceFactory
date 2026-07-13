# Maschinenwelt und Platzierung

Die Godot-Presentation unter `scripts/presentation/building` trennt Darstellung und Platzierungsgeometrie von den Produktionszuständen im Core. `MachinePresentationCatalog` enthält für alle zwölf Maschinentypen Footprint, Glyph, Grund- und Akzentfarbe sowie Interaktionsradius. Das Platzierungsraster beträgt zentral `24` Welteinheiten, der Rotationsschritt `10°`.

## Maschinenansicht

`MachineView` ist ein prozedural gezeichneter `StaticBody2D` auf dem eigenen Kollisionslayer `8`. `Configure` verankert die Ansicht anhand von `MachinePlacement` direkt als Kind des zugehörigen `AsteroidView`; dadurch folgt sie dessen Position und Rotation ohne eine Nachführsuche pro Frame. Die Kollisionsfläche reserviert sofort den vollständigen Footprint. Während der im Core definierten Bauzeit von `1,2` bis `1,8` Sekunden zeigt die Ansicht ein cyanfarbenes Hologramm, Scanlinie, Prozentstatus und anschließend den aktuellen Maschinenstatus. Der Interaktionsradius bleibt Teil des Presentation-Katalogs und kann über `RequestInteraction` einheitlich geprüft werden.

## Platzierungsprüfung

`MachinePlacementPreview` erhält über `ConfigureWorldSources` leichte Provider für die aktuell geladenen Kometen und Maschinen. `Start`, `Cancel`, `Rotate`, `Refresh` und `TryGetPlacement` bilden die vollständige Orchestrierungs-API. Nur während eines aktiven Platzierungsmodus werden die Provider und die Weltmaus ausgewertet.

Die Mausposition wird im lokalen Koordinatensystem des Kometen auf das 24er-Raster gesetzt. Die Vorschau bleibt relativ zur Kometenrotation ausgerichtet. Eine Platzierung ist nur gültig, wenn:

- die tatsächliche Kometengröße `Large` oder `Huge` ist und ein SurfaceProfile besitzt,
- der komplette gedrehte Footprint innerhalb von `BuildableRadius` und prozeduralem Umriss liegt,
- kein echter Krater den Footprint schneidet und die aus Roughness, Elevation und TerrainSeed ermittelte lokale Höhendifferenz klein genug ist,
- der Footprint keine bereits geladene Maschine überlappt,
- der vom Aufrufer bereitgestellte Material-Callback zustimmt.

Eine transparente prozedurale Maschinenglyphe und ihr Footprint werden gemeinsam grün oder rot gezeichnet. Die Vorschau liefert genau einen priorisierten Grund wie `Komet zu klein`, `Nicht genügend Platz`, `Krater oder unebene Fläche`, `Maschine blockiert` oder `Materialien fehlen`. Die Geometrie-Smokes prüfen Katalogvollständigkeit, Raster, Rotation, Separating-Axis-Überlappung sowie Kometengröße, Umriss, BuildableRadius, Krater und Terrain.
