# Minimap und Sternenkarte

Minimap und grosse Sternenkarte verwenden dieselbe `ExplorationMapService`-Instanz im Godot-unabhängigen Core. Die Karte erzeugt keine eigene Welt: `SectorContentGenerator` bereitet die deterministischen Kometen- und Rohstoffdaten einmal vor, aktive Sektoren verwenden dieselben Daten für ihre Godot-Nodes.

## Erkundungszustände

- `Unknown`: Der Sektor wurde noch nie gescannt und ist auf keiner Karte sichtbar.
- `Scanned`: Leichte Kartenmetadaten liegen innerhalb des Minimap-Scanbereichs vor.
- `ActiveLoaded`: Der Sektor besitzt aktive Weltobjekte, Ressourcen und Kollisionen.
- `UnloadedDiscovered`: Die Weltobjekte wurden entladen, die Entdeckungsdaten bleiben erhalten.

Aktiv geladen wird weiterhin ein gerichteter Radius von zwei Sektoren. Die Minimap scannt im Raumschiff einen kreisförmigen Bereich von 14.000 Welteinheiten, ungefähr das 1,5- bis 2-Fache des üblichen sichtbaren Kamerabereichs. Dabei werden für den äusseren Ring nur Metadaten vorbereitet; Godot-Objekte entstehen erst im aktiven Streamingbereich.

## Bedienung

- Die kreisförmige Minimap unten rechts ist nur im Raumschiff sichtbar. Norden bleibt fest oben, während sich ausschließlich das zentrierte Schiffsymbol mit der echten Schiffsausrichtung dreht.
- Ein Linksklick innerhalb der runden Minimap öffnet die grosse Karte über denselben zentralen UI-Zustand wie `M` und zentriert zunächst auf das Schiff.
- `M` öffnet oder schliesst die grosse Karte ausschliesslich im Raumschiff.
- Ziehen mit linker oder mittlerer Maustaste verschiebt die Karte; das Mausrad zoomt um den Mauszeiger.
- Ein Linksklick auf einen entdeckten Kometen oder eigenen Marker setzt oder entfernt das exklusive Navigationsziel. Der HUD-Pfeil zeigt danach dynamisch zum Ziel.
- Sobald das Raumschiff näher als die zentrale Grenze `TARGET_REACHED_DISTANCE = 150` kommt, endet nur die aktive Navigation. Pfeil, Entfernung und Kartenhervorhebungen verschwinden; Komet, Marker und Entdeckungsdaten bleiben erhalten.
- Rechtsklick setzt einen einfachen Marker innerhalb eines bereits entdeckten Sektors.
- **Zum Raumschiff** schaltet eine dauerhafte Kartenverfolgung um. Zoomen erhält die Verfolgung; manuelles Ziehen, ein Kometenziel, **Letzter Komet** oder eine Markeraktion an einem anderen Kartenort deaktivieren sie.
- Schaltflächen zentrieren auf das Schiff oder den zuletzt entdeckten Kometen; landbare Kometen lassen sich filtern.

Karte und Inventar sind gegenseitig exklusiv. Beide pausieren das Spiel im Schiff nicht, sodass WASD und Shift-Boost aktiv bleiben. Nur das Pausenmenü pausiert den Szenenbaum. Das Astronauten-Inventar blockiert weiterhin Astronautenbewegung, Abbau und Interaktionen, ohne die Welt zu pausieren.

## Zentrale Daten

`ExplorationMapService` speichert gescannte Sektoren, entdeckte Kometen, bekannte Rohstoffvorkommen, Besuchsstatus, Zielwahl und Kartenmarker. Abgebaute Vorkommen werden aus den Kartendaten entfernt. Nicht gescannte Sektoren können über die Karten-API nicht ausgelesen werden.
