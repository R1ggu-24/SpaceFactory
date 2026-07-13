# Inventar und Raumschifflager

## Aufbau

Das Inventarsystem trennt Daten, Regeln und Darstellung:

- `SlotInventory`, `InventorySlot` und `InventoryTransfer` liegen im Godot-unabhängigen Core.
- Das Astronauten-Inventar besitzt 20 Slots, das separate Raumschifflager 50 Slots.
- Jeder Slot enthält höchstens eine Rohstoff-ID und maximal 200 Einheiten.
- `ItemPresentationCatalog` verbindet die zentralen `ResourceDefinition`- und `ProductionItemDefinition`-Kataloge zu einem einzigen Presentation-Modell. Rohstoffe behalten ihre Farbe und ihren vorhandenen prozeduralen Gesteinsstil. Zwischenprodukte, Barren, Legierungen, Komponenten und Transportbehälter erhalten dagegen stabile eigene Farben und prozedurale Glyphen; Namen und Stapelgrenzen werden nicht parallel in der UI definiert.
- Die Presentation-Schicht baut die Slot-Controls einmal auf und aktualisiert danach nur deren Inhalt und Zustand.

Im geöffneten Raumschifflager zeigt ein kompaktes Tankmodul den aktuellen Füllstand. `InventoryMenuController.SetFuelTankState` aktualisiert ausschließlich diese Anzeige. Der Button `Tank auffüllen` löst `RefuelRequested` aus; Verbrauch, Behältertausch und Fehlermeldungen bleiben in der aufrufenden Spiellogik. Deren Ergebnis kann über `ShowExternalStatus` im gemeinsamen Inventar-Footer angezeigt werden.

Beim Hinzufügen werden zunächst nicht volle Stapel derselben Rohstoffart gefüllt und erst danach freie Slots belegt. Passt ein kompletter Abbauertrag nicht mehr hinein, bleibt die Operation unverändert und das Vorkommen erhalten; das HUD meldet `Inventar voll`.

## Öffnen und Spielzustand

`I` öffnet zu Fuß ausschließlich das persönliche Inventar. Im Raumschiff werden persönliches Inventar und Schiffslager gemeinsam geöffnet. `I` oder `Escape` schließen das Overlay wieder.

Das Inventar ist eine einzige dauerhaft vorhandene Fullscreen-UI. Beim Öffnen wird der pausierbare Weltzweig angehalten. Dadurch sind Bewegung, Abbau, Ein-/Aussteigen, Kamera und Mausklicks in die Welt gemeinsam blockiert, ohne einen zweiten Gameplay-Zustand zu erzeugen. Nach dem Schließen bleibt der zuvor aktive Steuerungsmodus erhalten.

## Transfers

Drag-and-drop ruft in beiden Richtungen dieselbe zentrale Transferfunktion auf. Ein leerer Zielslot übernimmt den Stapel, gleiche Rohstoffe werden bis 200 zusammengeführt und bei unterschiedlichen Rohstoffen werden vollständige Stapel getauscht. Nicht verschiebbare Restmengen bleiben im Quellslot. Vor dem Drop verwendet die UI dieselben Regeln als mutierungsfreie Vorschau für gültige und ungültige Zielmarkierungen.

Die Core-Tests prüfen insbesondere Transfers in beide Richtungen, Stapel mit 199 Einheiten, Überlauf, volle Zielstapel, volle Inventare, Tauschen und die Erhaltung der Gesamtmenge.

## Responsive Darstellung

Das Overlay nutzt den verfügbaren Viewport statt einer festen Fensterbreite. Auf normalen Desktopauflösungen stehen die Bereiche nebeneinander; bei schmalen Ansichten werden Slotgröße, Abstände und Raster kompakter angeordnet. Horizontaler Overflow ist deaktiviert. Die 20 Astronauten-Slots und 50 Schiffsslots bleiben bei 1366×768, 1920×1080 und 2560×1440 innerhalb des sichtbaren Bereichs; vertikales Scrollen bleibt nur als Reserve für ungewöhnlich kleine Fenster.

## Speicherung

Inventarinhalte werden in diesem Schritt bewusst nur zur Laufzeit gehalten. Eine dauerhafte Datenbank- oder Spielstandanbindung wird ergänzt, sobald das gewünschte Speichermodell feststeht. Das bestehende Speichern abgebauter Weltvorkommen ist davon unabhängig.
