# Bau- und Maschinenoberfläche

Die Godot-Presentation für das Bausystem liegt unter
`scripts/presentation/building` und verwendet keine Typen aus der
Produktionsdomäne. Gameplay-Code übergibt unveränderliche UI-Snapshots aus
`BuildUiModels.cs` und verarbeitet Benutzerwünsche über Ereignisse. Dadurch
bleiben Platzierung, Inventartransaktionen und Produktion ausserhalb der UI.

## Baumenü

`BuildMenuController` stellt Maschinen in den fünf Kategorien Verarbeitung,
Herstellung, Energie, Lagerung und Forschung dar. Jede Karte zeigt eine
prozedurale Vorschau, Funktion, Freischaltzustand sowie Baukosten mit Bestand
und wichtigster Fehlmenge. Der Controller bietet:

- `SetMachineCatalog(...)` zum Aktualisieren des Katalogs;
- `SetBuildActionLabel(...)` für den aktuell konfigurierten Build-Key;
- `Open()`, `Close()` und `CloseImmediately()`;
- `MachineSelected` und `Closed` als Integrationsereignisse.

Das responsive Raster verwendet je nach verfügbarer Breite ein bis drei
Spalten. Der Katalog scrollt nur vertikal; Hover verändert ausschliesslich
Farbe und Rahmen, niemals die Kartengrösse.

## Maschinenpanel

`MachinePanelController` zeigt Rezeptauswahl, Eingaben, Ausgaben,
Produktionsfortschritt, Energieversorgung und Maschinenstatus. Der dauerhafte
Startzustand wird als Toggle dargestellt. Benutzerwünsche werden über
`RecipeSelectionRequested`, `ActiveStateChangeRequested` und `Closed`
weitergegeben.

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
