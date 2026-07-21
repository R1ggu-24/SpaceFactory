# Bau- und Maschinenoberfläche

Die Godot-Presentation für das Bausystem liegt unter
`scripts/presentation/building` und verwendet keine Typen aus der
Produktionsdomäne. Gameplay-Code übergibt unveränderliche UI-Snapshots aus
`BuildUiModels.cs` und verarbeitet Benutzerwünsche über Ereignisse. Dadurch
bleiben Platzierung, Inventartransaktionen und Produktion ausserhalb der UI.

## Baumenü

`BuildMenuController` stellt ausschließlich bereits erforschte Maschinen und
Verbindungen dar. Noch gesperrte Karten werden weder in Kategorien noch in der
Suche erzeugt; leere Kategorien sind unsichtbar. Jede sichtbare Karte zeigt eine
prozedurale Vorschau, Funktion sowie Baukosten mit Bestand und wichtigster
Fehlmenge. Der Controller bietet:

- `SetMachineCatalog(...)` zum Aktualisieren des Katalogs;
- `SetBuildActionLabel(...)` für den aktuell konfigurierten Build-Key;
- `Open()`, `Close()` und `CloseImmediately()`;
- `MachineSelected` und `Closed` als Integrationsereignisse.

Das responsive Raster verwendet je nach verfügbarer Breite ein bis drei
Spalten und teilt den freigeschalteten Katalog auf kompakte Seiten auf. Damit
bleiben Karten auch bei 1366 × 768 vollständig sichtbar, ohne horizontale oder
vertikale Scrollbar. Hover verändert ausschliesslich Farbe und Rahmen, niemals
die Kartengrösse.

## Hotbar-Platzierung, Werkbank und Abbau

Physische Kabel, Förderbänder, Rohre und Maschinenbausätze verwenden dieselben
grünen beziehungsweise roten Platzierungsvorschauen wie das Baumenü. Linksklick
bestätigt nur gültige Vorschauen; Rechtsklick bricht ab. Der ausgewählte
Hotbar-Slot ist dabei die genaue Transaktionsquelle.

Ein aktiver Platzierungsmodus blockiert nur konkurrierende Weltaktionen wie Erz-
oder Maschinenabbau, nicht die Astronautenbewegung. WASD, Kamera und Mausvorschau
laufen deshalb gleichzeitig weiter; Bewegung beendet die Vorschau nicht. Normales
Mausrad wechselt bei Hotbar-basierten Bauitems durch Hotbar und Hand-Slot und
beendet die vorherige Platzierung stillschweigend. Eine direkt im Baumenü
gewählte Maschine bleibt dagegen beim normalen Mausrad stabil; dadurch kann
kein unbeabsichtigter Hotbarwechsel das Baumenüobjekt ersetzen. Die zentral
konfigurierbare Aktion `rotate_building` (Standard `R`) dreht die aktive
rotierbare Vorschau, ohne den Slot zu wechseln.

`PlacementRotationState` hält den zuletzt gewählten Winkel zentral für die
laufende Spielsitzung. Neue rotierbare Maschinen übernehmen ihn unabhängig
davon, ob sie aus Baumenü oder Hotbar stammen. Der erste Winkel ist null Grad;
nicht rotierbare Definitionen verwenden weiterhin null Grad und verändern den
gemerkten Winkel nicht.

Gültigkeit, rote Vorschau und Fehlermeldung lesen dasselbe zentrale Prüfergebnis.
Die UI zeigt nur dessen wichtigsten kurzen Grund, beispielsweise `Kabel zu lang`,
`Anschluss belegt`, `Kein freier Anschluss`, `Falscher Anschlusstyp`,
`Gelände zu uneben`, `Ausserhalb des Kometen`, `Objekt blockiert`,
`Komet zu klein`, `Nur auf Erz platzierbar`, `Materialien fehlen` oder
`Zu weit entfernt`. Damit kann eine rote Vorschau nicht mehr einen abweichenden
pauschalen Anschlussfehler melden.

Die Werkbank fertigt Erz- und Maschinen-Abbauwerkzeuge sowie Mobile-Miner-
Bausätze. Mobile Miner erscheinen nicht mehr als direkte Baumenükarte, sondern
werden ausschließlich als gefertigter Bausatz aus der Hotbar auf eine freie
Erzquelle gesetzt. Fertige Werkbankausgaben, die in das persönliche Inventar
passen, werden automatisch übernommen; bei vollem Inventar verbleiben sie ohne
Verlust im Maschinenausgang.

Über jeder platzierten Maschine stehen dauerhaft Maschinenname und aktueller
Status. Das getrennte Maschinen-Abbauwerkzeug entfernt platzierte Objekte nur
in Reichweite und führt Entfernung sowie vollständige Rückgabe als eine atomare
Transaktion aus. Dazu zählen auch die bereits reservierten Eingaben eines
laufenden Produktionsbatches; der kostenlose erste Basisgenerator erzeugt beim
Abbau keine nie bezahlten Baustoffe.

## Maschinenpanel

`MachinePanelController` zeigt Rezeptauswahl, Eingaben, Ausgaben,
Produktionsfortschritt, Energieversorgung und Maschinenstatus. Der dauerhafte
Startzustand wird als Toggle dargestellt. Benutzerwünsche werden über
`RecipeSelectionRequested`, `ActiveStateChangeRequested` und `Closed`
weitergegeben.

Das Maschinenpanel besitzt keinen Scrollcontainer. Das persönliche Inventar
belegt ungefähr das linke Drittel, die kompakte Maschinensteuerung die rechten
zwei Drittel. Normale Slots behalten verbindlich 64 Pixel Grösse und 6 Pixel
Abstand. Rezeptauswahl, Materialfluss, Fortschritt, Energie und Schalter bleiben
bei 1366 × 768, 1920 × 1080 und 2560 × 1440 vollständig sichtbar. Dauerhafte
Footer-, Status- und Bedienungstexte wurden entfernt; notwendiges Feedback wird
nur noch temporär eingeblendet. Leere Ein- und Ausgangsslots verwenden wie alle
anderen Inventarslots keinen Mittelpunkt-Platzhalter.

Maschinen-Ein- und -Ausgänge verwenden denselben verzögerten Itemnamen-Hover wie
Inventar, Lager und Hotbar. Der kleine Papierkorb unten rechts ist dieselbe
`InventoryTrashDropTarget`-Komponente wie im Inventar; ein abgelegter Stapel wird
direkt über die vorhandene Maschineninventar-Transaktion gelöscht, ohne zweiten
Dialog oder abweichende Sonderlogik.

Für jeden erwarteten Eingang werden Icon, Name sowie `vorhanden / benötigt`
direkt angezeigt. Die Ausgabespalte entsteht datenbasiert aus den physischen
Ausgaben des ausgewählten Rezepts: Einfache Rezepte zeigen genau einen Slot,
Mehrfachrezepte entsprechend mehrere, und radioaktive Abfälle erhalten einen
eigenen markierten Slot. Beim Rezeptwechsel wird diese Menge neu aufgebaut.
Volle notwendige Ausgänge blockieren die Produktion weiterhin in den zentralen
Produktionsregeln, sodass weder Haupt- noch Nebenprodukte verloren gehen.

Der Treibstoffgenerator ergänzt kompakte Schaltflächen `Tank auffüllen` und
`Tank leeren`. Auffüllen tauscht im klar bezeichneten `Tank-Slot 01` atomar
einen vollen gegen einen leeren Behälter; Leeren führt den umgekehrten Tausch
im selben Slot aus. Gestapelte Restbehälter werden sicher umgelagert oder die
gesamte Transaktion wird zurückgerollt.
Der interne Generatorvorrat, Eingabe und Ausgabe werden gemeinsam vorab geprüft
und bei einem Fehler vollständig zurückgesetzt.

`UpdateView(...)` aktualisiert laufende Werte in bestehenden Controls. Rezept-
und Materialzeilen werden nur neu aufgebaut, wenn sich ihre Definitionen oder
die Rezeptauswahl ändern, nicht bei jedem Produktionstick.

## Szenen und Smoke-Checks

- `scenes/ui/building/BuildMenu.tscn`
- `scenes/ui/building/BuildMachineCard.tscn`
- `scenes/ui/building/MachinePanel.tscn`

Beide Controller besitzen `RunConstructionSmokeTest()`. Die Headless-Prüfung
validiert Kategorien, Kosten- und Sperrzustände, responsive Layouts,
Rezeptauswahl, Materialfluss, Energie, Status, Fortschritt und Startschalter.
Erfolgsmarker sind `BUILD_MENU_UI_SMOKE_OK` und
`MACHINE_PANEL_UI_SMOKE_OK`.
