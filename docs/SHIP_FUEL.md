# Raumschiff-Treibstoff

Der Godot-unabhängige Core unter `SpaceFactory.Core.Ships.Fuel` verwaltet den typisierten Tank, Boostverbrauch und das atomare Betanken aus dem bestehenden `SlotInventory`. Die zentralen Werte sind:

- Tankkapazität: `1800` Treibstoffeinheiten
- Boostverbrauch: `1` Einheit pro tatsächlich aktiver Boost-Sekunde
- maximale Dauer bei vollem Tank: `1800 s` beziehungsweise `30 min`
- Inhalt eines gefüllten Treibstoffbehälters: `100` Einheiten
- Standard-Boost: `2600` Welteinheiten pro Sekunde
- Hochleistungs-Boost: `2600 × 1,7 = 4420` Welteinheiten pro Sekunde

`ShipFuelType` trennt `Standard` und `HighPerformance`. Ein neuer `ShipFuelTank` startet vollständig mit Standardtreibstoff. Normales Fliegen bleibt mit `975` Welteinheiten pro Sekunde für beide Sorten identisch. Der Tank enthält zu jeder Zeit höchstens eine Sorte: Solange noch Treibstoff vorhanden ist, lehnt `TryAddFuel` eine andere Sorte atomar ab. Ein leerer Tank kann beim nächsten vollständigen Behälter seine Sorte wechseln.

Die bisher gespeicherten IDs `fuel` und `fuel_container` bleiben absichtlich erhalten und bezeichnen jetzt Hochleistungstreibstoff. Dadurch bleiben alte Inventare und Produktionsketten kompatibel. Standardtreibstoff verwendet die neuen IDs `standard_fuel` und `standard_fuel_container`.

## Herstellung

- Zerkleinerter Kohlenstoff + Wasserstoff → Standardtreibstoff
- Standardtreibstoff + leerer Treibstoffbehälter → Standardtreibstoffbehälter
- Die bisherigen Raffinerierezepte erzeugen Hochleistungstreibstoff beziehungsweise Hochleistungstreibstoffbehälter.

Die Standardkette wird mit der Wasserstofftechnologie verfügbar und läuft in der Chemieanlage. Die Hochleistungskette bleibt an die spätere Treibstoffforschung und die Raffinerie gebunden.

`ShipRefuelService` erkennt beide Behältertypen. In der kombinierten Schiffsansicht wählt der Spieler einen Behälterslot im persönlichen Inventar oder im Raumschifflager und verwendet `Tank auffüllen` beziehungsweise `Tank leeren`. Vor jeder Mutation prüft der Dienst gemeinsam Treibstofftyp, freien Tankraum und Inventarplatz für den zurückzugebenden Behälter. Enthält der gewählte Slot einen Stapel, wird genau ein Behälter getauscht und der Rest über die normalen Stapelregeln sicher umgelagert. Scheitert irgendein Schritt, werden Tank und das vollständige Inventarsnapshot atomar wiederhergestellt.

Behälter sind im derzeitigen stapelbasierten Itemmodell bewusst diskrete Gebinde zu exakt `100` Einheiten; teilweise gefüllte Einzelinstanzen existieren noch nicht. Deshalb wird nur aufgefüllt, wenn weitere 100 Einheiten in den Tank passen, und nur geleert, wenn der Tank mindestens 100 Einheiten enthält. Ein kleiner Rest bleibt verlustfrei im Tank, bis wieder ein vollständiger Behälter befüllt werden kann.

Der Treibstoffgenerator verwendet dieselbe diskrete und atomare Behälteridee in seinem Maschinenpanel. Der zentrale `Tank-Slot 01` ist Eingabeslot `0`: `Tank auffüllen` ersetzt dort genau einen vollen Behälter durch einen leeren, `Tank leeren` führt denselben Tausch in Gegenrichtung aus. Der Behälter bleibt im Tank-Slot; ein eventuell gestapelter Rest wird über die normalen Stapelregeln sicher umgelagert. Tankgrenze, Slotinhalt und Umlagerungsplatz werden vor dem gemeinsamen Commit geprüft; der interne Generatorvorrat bleibt über seinen bestehenden `GeneratorFuelSecondsRemaining`-Zustand gespeichert.

## Start- und Testausstattung

`ShipFuelConfiguration.NewGameFuelType` definiert Standardtreibstoff als neue Startsorte. `HighPerformanceTestCargo` beschreibt zentral eine zusätzliche, leicht entfernbare Testladung von genau `18` Hochleistungstreibstoffbehältern – also eine vollständige Tankfüllung. Die persistente Startzustandskomposition übernimmt diese Definition; sie ist mit `IncludeHighPerformanceTestTankInNewGame` zentral abschaltbar.

Für die Speicherung müssen `CurrentFuel` und `CurrentFuelType` gemeinsam persistiert werden. Alte Speicherstände ohne Typ werden als Hochleistungstreibstoff migriert, weil deren bisherige ID und Produktionskette dem heutigen Hochleistungstreibstoff entsprechen. Neue Spielstände speichern ausdrücklich `Standard`.
