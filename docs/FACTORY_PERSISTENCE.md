# Factory-Persistenz

Der vollständige veränderliche Fabrikzustand wird versioniert in `user://factory_state.json`
gespeichert. Die unveränderte prozedurale Welt bleibt seed-basiert und wird nicht dupliziert.

`IFactoryStateStore` bildet die Application-Schnittstelle. `JsonFactoryStateStore` ist der
Godot-Dateiadapter, während `FactoryStateJsonCodec` eine dateisystem- und Godot-unabhängige
Roundtrip-Testnaht für JSON bereitstellt. Der Codec serialisiert die starken Core-Typen nicht
direkt: Maschinen-, Rezept-, Item- und Forschungs-IDs, Enums, Placement und Inventarslots werden
explizit auf primitive, versionierte DTO-Felder abgebildet und beim Laden neu konstruiert.

Der Zustand enthält:

- alle `MachineStateSnapshot`s mit Kometen-Placement, Rezept, Ein-/Aus-Schalter, Status,
  Bau- und Produktionsfortschritt, laufendem Batch, Generatorbrennstoff sowie Ein- und
  Ausgabeinventaren;
- alle `MachineConnectionSnapshot`s mit stabiler Verbindungs-ID, Typ und den Maschinen-
  und Port-IDs beider Endpunkte;
- den vollständigen `ResearchStateSnapshot`;
- ob der kostenlose erste Basisgenerator bereits gebaut wurde;
- den aktuellen Raumschifftreibstoff;
- alle belegten Slots des Astronauten-Inventars (20 Slots) und des
  Raumschifflagers (50 Slots) mit exakter Slotposition, Item-ID und Menge;
- die ID der Forschungsstation, an der eine laufende Forschung gebunden ist;
- Stromnetz-Schalter, Schutzschalter und Überlast-Timer;
- Raumschiff-Stromanschluss-Schalter A/B;
- Raumschiff-Dockingpose mit Kometen-ID, relativer Position/Rotation, Landebeinzustand und globaler Fallbackpose;
- je Komet den letzten Simulationszeitpunkt als UTC-Zeitstempel.

Schema v2 ergänzte die beiden Spielerinventare und die aktive Forschungsstation. Schema v3
ergänzte die Maschinenverbindungen. Schema v4 ergänzt die getrennten Stromnetz-Zustände,
Raumschiff-Stromanschlüsse und Dockingpose. Bestehende v1-, v2- und v3-Spielstände werden beim Lesen
automatisch auf v4 migriert; ihre Verbindungsliste ist leer, sofern das Ursprungsschema noch keine Verbindungen kannte. Da v1 auch keine Spielerinventare
oder Stationsbindung enthielt, starten diese Inventare dabei leer und die Stationsbindung ist
`null`. Beim nächsten Speichern wird ausschließlich das aktuelle v4-Format geschrieben. Die
Item-ID ist absichtlich katalogunabhängig gespeichert, damit Rohstoffe, Zwischenprodukte und
Behälter denselben verlustfreien Speicherpfad verwenden.

Fehlt die Datei oder ist sie leer, beschädigt, unvollständig, doppelt belegt oder besitzt sie
eine nicht unterstützte Version, liefert der Store einen sicheren leeren Startzustand. Maschinen-
IDs, Slotindizes, Mengen, starke IDs, endliche Fortschrittswerte, Forschung, Tankgrenzen,
Stromnetz-Timer, Schiffanschlusszustände und Dockingkoordinaten werden vor der Verwendung
validiert. Verbindungen benötigen eindeutige IDs, vorhandene platzierte Maschinen oder gültige
Schiff-Stromendpunkte auf demselben Kometen sowie eindeutige Endpunktpaare. Ein valides
`factory_state.json.tmp` kann nach einem unterbrochenen ersten Schreibvorgang zur
Wiederherstellung verwendet werden.

`InventoryStatePersistence` bildet zwischen `SlotInventory` und den primitiven
`InventorySlotState`-Einträgen ab. Die Wiederherstellung löscht den Zielinhalt erst nach einer
vollständigen Validierung und setzt belegte Slots anschließend exakt an ihren gespeicherten Index.
`FactoryRuntimeController.AttachShipInventory(...)` bindet das separat besessene Schiffslager an
die Laufzeit; bis dahin bleibt sein geladener Snapshot unverändert für den nächsten Save erhalten.

Beim Speichern wird JSON zuerst vollständig erzeugt und validiert. Anschließend schreibt und
flusht der Adapter `user://factory_state.json.tmp`; erst danach ersetzt `DirAccess.Rename` die
Zieldatei. Ein Serialisierungs- oder Schreibfehler verändert dadurch den letzten gültigen
Spielstand nicht. Schlägt nur die Umbenennung fehl, bleibt die Tempdatei als Wiederherstellungs-
quelle erhalten. Der Store meldet Erfolg oder Fehlschlag an die Laufzeit zurück; nur nach einem
erfolgreichen Austausch wird der Zustand als gespeichert markiert.
