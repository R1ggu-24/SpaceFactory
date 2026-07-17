# Maschinenwelt und Platzierung

Die Godot-Presentation unter `scripts/presentation/building` trennt Darstellung und Platzierungsgeometrie von den Produktionszuständen im Core. `MachinePresentationCatalog` enthält für alle zwölf Maschinentypen Footprint, Glyph, Grund- und Akzentfarbe sowie Interaktionsradius. Das Platzierungsraster beträgt zentral `24` Welteinheiten, der Rotationsschritt `10°`.

## Maschinenansicht

Alle zwölf Maschinen besitzen eine eigene, aus der Funktion abgeleitete Top-down-Silhouette. Der gemeinsame Stil verwendet dunkles Titan und Graphit, eingelassene Platten, Schrauben, Leitungen sowie kleine Cyan-, Blau- oder Wärmeakzente. Zerkleinerer, Schmelzer, Giesserei, Konstruktor, Fabrikator, Wasseraufbereiter, Elektrolyseur, Raffinerie, beide Generatoren, Lagercontainer und Forschungsstation bleiben dadurch auch ohne Beschriftung unterscheidbar. `MachineGlyphControl` verwendet dieselbe Formsprache in Baumenü, Maschinenpanel und Platzierungsvorschau.

Funktionsbezogene Animationen werden nur bei einem tatsächlich produzierenden Zustand neu gezeichnet. Dazu gehören unter anderem bewegte Walzen und Staub, dezente Hitze- und Dampfpartikel, Guss- und Montagearme, Pumpen, Blasen und Flussimpulse, Turbinen sowie Scanner- und Forschungsbewegungen. Ruhende Maschinen verursachen dafür keine kontinuierlichen Redraws. Kleine Statusleuchten zeigen Produktion, Bereitschaft, fehlende Energie beziehungsweise Materialien und volle Ausgaben, ohne die Umgebung grossflächig auszuleuchten.

Stabile lokale Anschlusspunkte für Strom, Item-Eingang/-Ausgang und Rohr-Eingang/-Ausgang liegen im `MachinePresentationCatalog`. `MachineView.GetWorldConnectionAnchor` transformiert sie zusammen mit Position und Rotation der Maschine, sodass Kabel, Bänder und Rohre exakt an der gedrehten Grafik verankert bleiben.

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

Die grobe bebaubare Fläche nutzt jetzt `0,70 × Radius` für `Large` und `0,78 × Radius` für `Huge`. Prozeduraler Umriss, Krater und Terrain bleiben danach weiterhin die autoritativen Detailprüfungen. Damit wird sichtbar flache Fläche nicht mehr vorzeitig als `Nicht genügend Platz` verworfen. Der konfigurierte Maschinenabstand von sechs Welteinheiten wird hälftig auf beide Footprints verteilt; zuvor wurde der volle Abstand auf beiden Seiten angesetzt und damit unbeabsichtigt verdoppelt.

## Verbindungsdarstellung

Das Baumenü enthält den eigenen Bereich `TRANSPORT` mit Stromkabel, Förderband, Flüssigkeitsrohr und Gasrohr. Die Platzierung verwendet zwei Klicks: zuerst Quellmaschine beziehungsweise Stromanschluss, danach die kompatible Zielmaschine. Währenddessen zeigt `MachineConnectionPlacementPreview` die Verbindung bis zur Weltmaus, den aktuell gewählten Anschluss und einen klaren Fehlerzustand bei inkompatiblen oder zu weit entfernten Zielen.

`MachineConnectionView` zeichnet die gespeicherte Verbindung kometenlokal zwischen den gedrehten Maschinenankern. Kabel verwenden eine schmale Energieader, Förderbänder eine gerichtete industrielle Route und Flüssigkeits- beziehungsweise Gasrohre getrennte Farben. Flussmarker werden nur kurz bei realer Energie- oder Materialübertragung animiert. Beim Entladen eines Chunks verschwinden die Nodes; beim erneuten Laden werden sie aus den persistenten Endpunkten rekonstruiert.
