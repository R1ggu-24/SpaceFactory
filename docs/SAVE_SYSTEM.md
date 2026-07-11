# Speichersystem

Die unveränderte Grundwelt wird nicht gespeichert: Welt-Seed und Generatorversion reichen zur Rekonstruktion. Ein Spielstand soll Spieler-/Schiffszustand, Inventare, Upgrades, entdeckte Sektoren, Marker sowie Änderungen je Sektor enthalten.

Sektordeltas beschreiben gebaute oder zerstörte Maschinen, Lagerinhalte und abgebaute Ressourcenvorkommen anhand stabiler IDs. Beim Laden wird zuerst der Grundsektor generiert und danach sein Delta angewendet. Fabriken werden als Maschineninstanzen mit Definition-ID, Position, Zustand und Verbindungen gespeichert.

Für Ressourcenvorkommen ist dieses Delta-Prinzip bereits umgesetzt. Jedes Vorkommen erhält aus Welt-, Kometen- und Vorkommens-Seed eine stabile ID. Nur veränderte Restmengen werden in `user://resource_deposits.json` gespeichert; unveränderte Vorkommen werden beim erneuten Laden kostenlos aus dem Seed rekonstruiert. Beim Laden eines Sektors wird der gespeicherte Restwert auf die generierte Grundwelt angewendet, sodass vollständig abgebaute Vorkommen nicht wieder erscheinen.

Das Dateiformat erhält eine eigene `saveVersion`; Migrationen wandeln ältere Versionen schrittweise um. Atomisches Schreiben, Backups und vollständige Implementierung folgen in einer späteren Phase.
