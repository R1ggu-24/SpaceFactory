# Spieler, Kamera und Raumschiff

## Kamera

Die Weltkameras sind vom UI getrennt; HUD, Einstellungen und Inventar liegen in eigenen `CanvasLayer`-Ebenen und behalten daher ihre Bildschirmgröße. Der unmittelbar vorherige Zoom betrug `0.32` im Schiff und `0.52` zu Fuß. Die aktuellen Werte sind zentral in den Controllern festgelegt:

- Raumschiff: `0.22`
- Astronaut: `0.36`

Kleinere Zoomwerte zeigen mehr Welt. Die Welt-Mausposition wird weiterhin über Godots aktive `Camera2D` berechnet, sodass Abbauziele nach dem Herauszoomen korrekt bleiben. Der kameraunabhängige Sternenhintergrund füllt den gesamten Viewport.

## Raumschiffgröße und Kollision

`PlayerShipController.VisualScaleMultiplier` ist `1.8`. Aus der ursprünglichen Sprite-Skalierung `0.22` entsteht damit `0.396`; Sprite und Kollisionspolygon werden beim Start aus den zentralen Basiswerten berechnet. Das Kollisions-Bounding-Rectangle wächst von `256 × 268` auf `460.8 × 482.4` Welteinheiten. Das Seitenverhältnis bleibt unverändert, und Mipmaps halten das stark verkleinerte hochauflösende Sprite ruhig.

Der Cockpit-Interaktionsradius bleibt bewusst bei 70 und wird nicht mit 1,8 multipliziert. Der Marker liegt an der vorderen Cockpitkante, damit dieser kleine Bereich auch mit aktiver Astronaut-Schiff-Kollision erreichbar bleibt. Der erste Ausstiegsabstand skaliert dagegen von 72 auf 129,6. Jeder Kandidat wird mit einem Sicherheitskreis von 28 Einheiten sowohl gegen das echte skalierte Schiffspolygon als auch gegen Kometen und Rohstoffvorkommen geprüft.

## Flug und Boost

Das normale Zieltempo beträgt 650 Welteinheiten pro Sekunde. Die Aktion `ship_boost` liegt standardmäßig auf `Shift` und verwendet den zentralen Multiplikator `2.0`, also ein Boost-Zieltempo von 1300. Beschleunigung und Rücknahme werden kurz interpoliert, Positionssprünge vermieden und die Bewegung bleibt in Godots kontinuierlicher `CharacterBody2D`-Kollisionsprüfung.

Pause, Fokusverlust und ein Wechsel aus der Schiffsteuerung entwaffnen den Boost bis zum Loslassen der Taste. Die zwei Antriebsflammen sitzen an den skalierten Düsenpositionen, werden im Normalflug sichtbar verlängert und gehen im Boost weich in eine längere, hellere Variante über.

## Befestigen und Drift

Die remapbare Aktion `ship_docking` liegt standardmässig auf `H`. Sie verwendet dieselbe zentrale `ShipDockingRules`-Entscheidung für HUD-Hinweis und tatsächliche Aktion. Befestigt werden kann nur an grossen Kometen, wenn eine echte Polygonoberfläche nahe genug, die Schiffskontur frei, eine sichere Ausstiegsposition vorhanden und das Schiff langsam genug ist.

Zentrale Startwerte in `ShipDockingConfiguration.Default`:

- maximaler Abstand zwischen Schiffshülle und Oberfläche: `96`
- maximales Tempo beim Befestigen: `90`
- maximale unbemannte Driftgeschwindigkeit: `110`
- Geschwindigkeitsübernahme des Astronauten: `85 %`
- Landebeinfortschritt: `4,0` pro Sekunde, entsprechend `0,25 s` bis vollständig aus- oder eingefahren

Beim Befestigen wird das Schiff mit der Nase von der Oberfläche weg ausgerichtet und speichert Kometen-ID, relative Position und relative Rotation. Zwei gezeichnete futuristische Befestigungsbeine fahren zur Oberfläche aus. Solange die Verbindung besteht, folgt das Schiff dem Kometentransform stabil; Flug, Boost und Antriebsflammen bleiben deaktiviert, Ein- und Aussteigen funktioniert weiter.

Beim Lösen bleibt die Geschwindigkeit zunächst null und das Schiff wird geringfügig von der Oberfläche freigestellt. Verlässt der Spieler ein unbefestigtes Schiff, wird dessen aktuelle Geschwindigkeit auf `110` begrenzt und mit der bestehenden `CharacterBody2D`-Kollision weiterbewegt. Rohstoffvorkommen besitzen dafür neben ihrer Mining-Interaktionsfläche eine physische `StaticBody2D`-Kollision; das Schiff blockiert Kometen und Rohstoffe, der weiterhin frei schwebende Astronaut dagegen nicht. Der Astronaut übernimmt `85 %` der Schiffdrift, sodass das Schiff erreichbar bleibt; seine eigene Bewegungsgeschwindigkeit ist höher als das Driftlimit. Während der Astronaut aktiv ist, hält das Streaming zusätzlich einen kleinen Sektorradius um das driftende Schiff geladen.
