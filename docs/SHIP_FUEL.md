# Raumschiff-Treibstoff

Der Godot-unabhängige Core unter `SpaceFactory.Core.Ships.Fuel` verwaltet den Tank, Boostverbrauch und das atomare Betanken aus dem bestehenden `SlotInventory`. Die zentralen Startwerte sind:

- Tankkapazität: `1800` Treibstoffeinheiten
- Boostverbrauch: `1` Einheit pro tatsächlich aktiver Boost-Sekunde
- maximale Dauer bei vollem Tank: `1800 s` beziehungsweise `30 min`
- Mindestmenge zum Starten: `0,000001` Einheiten; der letzte Teil-Frame leert den Rest vollständig
- Inhalt eines gefüllten Treibstoffbehälters: `100` Einheiten

`ShipFuelTank.ConsumeBoostFuel` erhält ausdrücklich, ob der Boost in diesem Simulationsschritt wirklich aktiv war. Normales Fliegen, ein lediglich gedrückter Boost-Button, ein blockiertes oder befestigtes Schiff verbrauchen dadurch keinen Treibstoff. Eine Zeitscheibe wird nur freigegeben und abgezogen, wenn sie vollständig bezahlt werden kann; ein leerer Tank verhindert den Boost, ohne den Normalflug zu beeinflussen.

`ShipRefuelService.TransferFilledContainers` verwendet die zentralen Produkt-IDs `fuel_container` und `empty_fuel_container`. Es werden nur vollständige 100-Einheiten-Behälter entleert. Vor jeder Mutation prüft der Dienst gemeinsam den freien Tankraum und den Inventarplatz für alle zurückzugebenden Leerbehälter. Nicht passende Restkapazität bleibt deshalb im gefüllten Behälter; Tank und Inventar werden weder teilweise verändert noch dupliziert.
