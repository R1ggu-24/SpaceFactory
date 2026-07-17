# Speichersystem

Die unveränderte Grundwelt wird nicht gespeichert: Welt-Seed und Generatorversion reichen zur Rekonstruktion. Ein Spielstand soll Spieler-/Schiffszustand, Inventare, Upgrades, entdeckte Sektoren, Marker sowie Änderungen je Sektor enthalten.

Sektordeltas beschreiben gebaute oder zerstörte Maschinen, Lagerinhalte und abgebaute Ressourcenvorkommen anhand stabiler IDs. Beim Laden wird zuerst der Grundsektor generiert und danach sein Delta angewendet. Fabriken werden als Maschineninstanzen mit Definition-ID, Position, Zustand und Verbindungen gespeichert.

Für Ressourcenvorkommen ist dieses Delta-Prinzip bereits umgesetzt. Jedes Vorkommen erhält aus Welt-, Kometen- und Vorkommens-Seed eine stabile ID. Nur veränderte Restmengen werden in `user://resource_deposits.json` gespeichert; unveränderte Vorkommen werden beim erneuten Laden kostenlos aus dem Seed rekonstruiert. Beim Laden eines Sektors wird der gespeicherte Restwert auf die generierte Grundwelt angewendet, sodass vollständig abgebaute Vorkommen nicht wieder erscheinen.

Der Fabrikzustand besitzt bereits ein eigenes versioniertes Format in `user://factory_state.json`.
Maschinen, ihre Verbindungen, Forschung, die aktive Forschungsstation, beide Spielerinventare,
der erste kostenlose Basisgenerator, Raumschifftreibstoff und die letzten
UTC-Simulationszeitpunkte pro Komet werden über `IFactoryStateStore` gespeichert. Schema v3 liest
weiterhin v1- und v2-Dateien. Fehlende Verbindungen werden sicher als leer migriert; bei v1 gilt
das zusätzlich für die noch nicht vorhandenen Spielerinventare. Der
`JsonFactoryStateStore` schreibt zunächst eine validierte Tempdatei und ersetzt die Zieldatei erst
danach per Godot-Umbenennung. Details und Validierungsregeln stehen in
[`FACTORY_PERSISTENCE.md`](FACTORY_PERSISTENCE.md). Künftige Versionsmigrationen können vor der
expliziten DTO-zu-Core-Konvertierung ergänzt werden.
