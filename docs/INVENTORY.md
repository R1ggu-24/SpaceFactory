# Inventar, Hotbar und Raumschifflager

## Aufbau

Das Inventarsystem trennt Daten, Regeln und Darstellung:

- `SlotInventory`, `InventorySlot`, `InventoryTransfer`, `HotbarState` und `ToolInventoryState` liegen im Godot-unabhängigen Core.
- Das Astronauten-Inventar besitzt 24 Slots im Raster 6 × 4.
- Die separate Hotbar besitzt genau 6 Slots; ihr aktiver Slot wird unabhängig vom Inhalt gespeichert.
- Die Werkzeugleiste besitzt 4 Slots. Ihre gemeinsame Slot-Akzeptanzregel liest ausschließlich die Kategorie `Tool` aus dem zentralen Produktionskatalog; normale Materialien und Baugegenstände werden bereits von der Core-Transferlogik abgelehnt.
- Das Raumschifflager besitzt 56 Slots im Raster 8 × 7.
- Rohstoffe und stapelbare Bauteile verwenden grundsätzlich maximal 200 Einheiten pro Slot. Gegenstände können über den zentralen Produktionskatalog eine kleinere Grenze definieren; das Abbauwerkzeug ist beispielsweise nicht stapelbar.
- Dieselben Gegenstandsgrenzen gelten in Maschineninventaren. Frühere überstapelte Werkzeugausgaben werden beim Laden auf freie Maschinenslots verteilt und bei Transfers stückweise ohne Verlust übernommen.
- `ItemPresentationCatalog` verbindet die zentralen Ressourcen- und Gegenstandsdefinitionen mit Namen, Farbe, Icon und Stapelgrenze. Erz- und Maschinen-Abbauwerkzeug verwenden in Inventar, Hotbar und an der Spielfigur ihre jeweiligen Sprites.
- Ein leerer Slot rendert ausschließlich seinen normalen Hintergrund und Rahmen. Der frühere Punkt beziehungsweise Platzhalter in der Mitte wurde zentral aus der wiederverwendeten Slotdarstellung entfernt und erscheint deshalb auch nicht in Hotbar, Werkzeugleiste, Lager oder Maschinenslots.

Beim Hinzufügen werden zuerst nicht volle Stapel derselben Gegenstandsart gefüllt und danach freie Slots belegt. Passt ein vollständiger Abbauertrag nicht mehr hinein, bleibt das Vorkommen bestehen und das HUD meldet `Inventar voll`.

## Hotbar und Werkzeug

Die Hotbar bleibt beim Steuern des Astronauten unten mittig sichtbar. Die sechs gleich großen Slots verwenden überall denselben Abstand. Rechts daneben spiegelt ein gleich großer, schreibgeschützter Hand-Slot das ausgewählte Werkzeug; er ist kein siebter Lagerplatz. Die sechs Aktionen `hotbar_slot_1` bis `hotbar_slot_6` verwenden standardmäßig die Tasten `1` bis `6`, werden aber wie alle anderen Eingaben über das zentrale Keybind-System konfiguriert und in den Einstellungen gespeichert.

`activate_hand_slot` aktiviert standardmäßig per mittlerer Maustaste den Handmodus. `previous_tool` beziehungsweise `next_tool` wechseln zyklisch durch die vier Werkzeugslots und überspringen leere Slots; standardmäßig stehen dafür Pfeil oben/links und Pfeil unten/rechts bereit. Ein normaler Hotbar-Slot beendet den Handmodus. Die Option `Hotbar mit Mausrad wechseln` ist standardmäßig aktiv und wird zusammen mit den übrigen Einstellungen gespeichert. Ausserhalb von Menüs durchläuft das Rad immer `Slot 1 … Slot 6 → Hand-Slot → Slot 1`. Auch ein platzierbarer Gegenstand hält den Zyklus nicht an: Der nächste Radimpuls beendet dessen Vorschau still und wählt den nächsten Slot. Die Baurotation verwendet deshalb die konfliktfrei konfigurierbare Aktion `rotate_building` (Standard `R`).

Die dauerhaft sichtbare Hotbar zeigt keine Tastaturbezeichnungen. Sie enthält nur Icon, Menge, Auswahl- und Statusrahmen. Der Hand-Slot besitzt einen zusätzlichen Abstand und einen eigenen Werkzeugrahmen, aber keine dauerhafte `TOOL`-, `MAX`- oder Mengenkennzeichnung für nicht stapelbare Werkzeuge.

Ein neuer Spielstand erhält das Erz-Abbauwerkzeug und das getrennte Maschinen-Abbauwerkzeug als normale, nicht stapelbare Gegenstände. Werkzeuge können per Drag-and-drop zwischen persönlichem Inventar und Werkzeugleiste verschoben werden; die normale Hotbar nimmt sie nicht mehr an. Das aktive Erzwerkzeug baut ausschließlich Rohstoffe ab; das aktive Maschinenwerkzeug entfernt Maschinen, Miner, Masten, Kabel, Förderbänder und Rohre atomar und gibt Baugegenstände sowie gespeicherte Inhalte zurück.

Stromkabel, Förderbandsegmente, Transportrohre und gefertigte Maschinenbausätze sind physische Gegenstände. Wird ein solcher Gegenstand in der Hotbar ausgewählt, startet sein vorhandener Platzierungsmodus automatisch. Ein anderer Hotbar-Slot beendet die laufende Vorschau still; Rechtsklick bricht sie ab. Verbrauch und Fehler-Rollback erfolgen immer im konkret ausgewählten Hotbar-Slot.

## Öffnen und Spielzustand

`I` öffnet abhängig vom gesteuerten Zustand genau eine Ansicht:

- Zu Fuß: persönliches Inventar mit 24 Slots im Raster 6 × 4, vier kleinere zeilengleich angeordnete Werkzeugslots rechts und die Hotbar darunter. Die Werkzeugspalte besitzt kein zweites Kartenpanel; eine schmale graue Trennlinie verbindet sie optisch mit dem persönlichen Inventar.
- Im Raumschiff: persönliches Inventar links und das unabhängige Raumschifflager mit 56 Slots rechts; die Hotbar wird in dieser Transferansicht ausgeblendet.
- An einem Lagercontainer: persönliches Inventar links und der echte Maschinen-Lagerbestand rechts; auch hier bleibt die Hotbar ausgeblendet.

Während eines Inventarfensters werden Weltklicks, Bewegung, Abbau und Ein-/Aussteigen blockiert. `I` schließt das Fenster. `Escape` schließt zuerst ein offenes Item-Kontextmenü, danach das Inventar zurück ins Spiel; erst ein weiterer Druck aus dem Spiel öffnet das Pausenmenü.

## Transfers

Drag-and-drop verwendet für persönliche Slots, Werkzeugslots, Hotbar, Schiffslager und Lagercontainer dieselbe zentrale Transferfunktion. Bei Lageransichten werden vorhandene Teilstapel des Zielinventars vor freien Slots aufgefüllt. Ein leerer Zielslot übernimmt den Stapel, gleiche stapelbare Gegenstände werden bis zu ihrer individuellen Grenze zusammengeführt und unterschiedliche vollständige Stapel werden getauscht. Nicht verschiebbare Restmengen bleiben im Quellslot. Werkzeugslots akzeptieren ausschließlich katalogisierte Werkzeuge; die normale Hotbar ist für platzierbare Gegenstände reserviert. Die UI prüft dieselben Regeln vor dem Drop und markiert gültige beziehungsweise ungültige Ziele, ohne den Zustand zu verändern. Der Slot selbst ist verbindlich die Maus- und Drag-Oberfläche; seine rein dekorativen Margin-, Icon- und Mengen-Kinder ignorieren Mausereignisse. Auch der frühere ScrollContainer wurde durch einen nicht interaktiven Layout-Container ersetzt, damit keine unsichtbare Ebene eine Drag-Geste übernehmen kann.

Der aktive Hotbar-Index bleibt beim Verschieben erhalten. Ändert sich dadurch der Inhalt des aktiven Slots, wird die ausgerüstete Ausrüstung sofort aktualisiert. Die Core-Tests prüfen Überlauf, volle Ziele, Tauschen, nicht stapelbare Werkzeuge sowie die Erhaltung der Gesamtmenge.

Lageransichten ergänzen `Alles einlagern` und `Alles nehmen` als kleine Iconbuttons zwischen beiden Bereichen; die Beschriftung steht ausschließlich im Tooltip. Der Einlagern-Pfeil zeigt zum rechten Lager, der Nehmen-Pfeil zurück zum linken Inventar. Beide Wege verwenden denselben bestmöglichen Core-Transfer: Teilstapel im Ziel werden zuerst gefüllt, inkompatible Items bleiben liegen und ein volles Ziel beendet den Vorgang ohne Verlust. Wenn gar nichts passt, meldet die UI kurz `Kein freier Platz im Ziel` beziehungsweise `Nichts zu verschieben`. Persönliches Inventar und geöffnetes Lager besitzen jeweils ein eigenes kleines Papierkorb-Icon unten rechts. Ein darauf abgelegter Stapel wird unabhängig von Wert, Seltenheit oder Stapelbarkeit unmittelbar gelöscht; es gibt bewusst keinen Bestätigungsdialog. Transfer- und Fehlermeldungen erscheinen nur noch kurz als mausdurchlässiger Toast; eine permanente Status- oder Bedienungszeile belegt keinen Menüplatz mehr.

## Itemanzeige und Kontextmenü

Normale belegte Slots zeigen ausschließlich Icon und bei stapelbaren Gegenständen die Menge unten rechts. Nicht stapelbare Werkzeuge zeigen nur ihr Icon; die früheren Marker `MAX`, `TOOL` und Hotbar-Tastentexte werden nicht mehr gerendert. Leere Slots besitzen weder Icon noch Punkt. Dekorative Kindelemente sind mausdurchlässig, damit Drag-and-drop immer am eigentlichen Slot beginnt.

Ein gemeinsamer Hover-Presenter zeigt den Gegenstandsnamen nach `0,2 s` klein neben dem Zeiger, hält ihn kurz sichtbar und blendet ihn innerhalb von insgesamt ungefähr einer Sekunde wieder aus. Beim Wechsel des Hover-Ziels wird die alte Anzeige sofort verworfen. Inventar-, Lager-, Hotbar- sowie Maschinen-Ein- und -Ausgabeslots verwenden dieselbe Instanz und dieselben Zeiten.

Ein Rechtsklick auf ein Item öffnet direkt am Slot ein kleines, an den Viewport geklemmtes Kontextmenü. Die zentrale Optionsmatrix zeigt `Item auswählen` nur für Werkzeuge und platzierbare Items, `Stapel teilen` und `Einzelnes nehmen` nur bei Mengen über eins, außerdem `Item(s) fallen lassen` und – außerhalb der normalen Hotbar – `Item ausrüsten`. Teilen und Einzelentnahme suchen einen freien Slot ohne die Gesamtmenge zu verändern. Ausrüsten verwendet den ersten freien normalen Hotbar-Slot; ist die Hotbar voll, wird Slot 1 atomar getauscht. Auswählen aktiviert ein Werkzeug über die Werkzeugleiste beziehungsweise startet die bestehende Hotbar-Platzierung. Veraltete Kontextanfragen prüfen weiterhin die erwartete Item-ID und können deshalb keinen inzwischen ausgetauschten Stapel verändern.

## Gegenstände in der Welt

Alle sichtbaren Slotansichten verwenden dieselbe Drag-Adresse. Ein Drop ausserhalb des Hauptfensters kann daher aus persönlichem Inventar, Hotbar, Werkzeugleiste, Schiffslager, Lagercontainer sowie Maschinen-Ein- und -Ausgängen ein echtes Weltobjekt erzeugen. Die Runtime reserviert zuerst exakt den Quellstapel, sucht danach eine kollisionsfreie Position und rollt bei jedem Fehler in genau denselben Slot zurück.

Weltobjekte übernehmen die begrenzte Geschwindigkeit des aktuell gesteuerten Astronauten oder Raumschiffs, erhalten im Stillstand nur einen kleinen Impuls und rotieren langsam. Sie werden bei freiem Inventar in Reichweite wieder aufgenommen. Entfernte Ansichten werden nur entladen; ID, Item, Menge, Position, Rotation und Geschwindigkeit bleiben im persistenten Fabrikzustand erhalten.

## Responsive Darstellung

Die Raster sind bei 1366 × 768, 1920 × 1080 und 2560 × 1440 vollständig ohne Browser- oder Container-Scrollbars sichtbar. Normale Inventar- und Lagerslots bleiben auf allen drei Auflösungen bei 64 Pixeln mit 6 Pixeln Abstand; nur die ausdrücklich kompaktere Werkzeugspalte verwendet 44-Pixel-Slots. Bei wenig Platz werden ausschließlich äussere Ränder, Panel-Chrome und dekorative Texte reduziert. Die dauerhaft sichtbare Hotbar wird bei grossen Menüs und im Raumschiff ausgeblendet.

## Speicherung und Migration

`factory_state.json` speichert alle persönlichen Slots, alle Hotbar- und Werkzeugslots, den aktiven Hotbar- und Werkzeugindex, den Handmodus, alle Schiffsslots, die Inventare platzierter Maschinen und die persistenten Welt-Drops. Die Migration erweitert alte Spielstände auf 24/6/4/56 Slots, behält vorhandene Slotpositionen bei und ergänzt nur leere neue Slots. Fehlt das Maschinen-Abbauwerkzeug, wird es idempotent in den ersten freien persönlichen, Werkzeug-, Hotbar- oder Schiffsslot eingesetzt. Ein in einer Maschine gelagertes Exemplar wird erkannt; bei vollständig belegten Altständen erfolgt die verlustfreie Vergabe erst, sobald ein Slot frei wird. Individuelle Hotbar-Keybinds und die Mausradoption werden wie die übrigen Einstellungen separat in `settings.json` gespeichert.
