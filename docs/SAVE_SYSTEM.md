# Speichersystem

Die unveränderte Grundwelt wird nicht gespeichert: Welt-Seed und Generatorversion reichen zur Rekonstruktion. Ein Spielstand soll Spieler-/Schiffszustand, Inventare, Upgrades, entdeckte Sektoren, Marker sowie Änderungen je Sektor enthalten.

Sektordeltas beschreiben gebaute oder zerstörte Maschinen, Lagerinhalte und abgebaute Ressourcenvorkommen anhand stabiler IDs. Beim Laden wird zuerst der Grundsektor generiert und danach sein Delta angewendet. Fabriken werden als Maschineninstanzen mit Definition-ID, Position, Zustand und Verbindungen gespeichert.

Das Dateiformat erhält eine eigene `saveVersion`; Migrationen wandeln ältere Versionen schrittweise um. Atomisches Schreiben, Backups und vollständige Implementierung folgen in einer späteren Phase.
