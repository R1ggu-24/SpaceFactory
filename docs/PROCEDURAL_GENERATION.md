# Prozedurale Generierung

Jeder Sektor erhält einen lokalen Zustand aus Welt-Seed, X, Y und einem Generierungs-Salt. Die Werte werden mit festen 64-Bit-Konstanten gemischt und initialisieren einen lokalen SplitMix64-Generator. Es gibt keinen globalen Zufallszustand; die Ladereihenfolge kann das Ergebnis daher nicht verändern.

Die Einstellungen definieren Sektorgröße, minimale/maximale Asteroidenzahl und Gewichte für `Tiny`, `Small`, `Medium`, `Large` und `Huge`. Position, Radius, Typ und Ressource werden aus dem lokalen Generator abgeleitet. Gleiche Eingaben erzeugen byte-stabil dieselben Domänenwerte innerhalb derselben Generatorversion.

Die Demo hält nur den aktuellen Sektor und acht Nachbarn. Entfernte Sektoren werden entladen. Später können Generatorversion, Biome, Gefahren und besondere Orte über separate Salts ergänzt werden, ohne unabhängige Teilsysteme zu verschieben. Persistente Spieleränderungen werden nach der Regenerierung als Deltas angewendet.
