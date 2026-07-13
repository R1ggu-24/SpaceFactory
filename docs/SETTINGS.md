# Einstellungsmenü

Das Einstellungsmenü wird über die Aktion `pause` geöffnet, standardmäßig mit `Escape`. Es läuft in einem eigenen `CanvasLayer` mit dem Prozessmodus `Always`, während der komplette `World`-Zweig pausierbar ist. Dadurch bleiben Menü, Bestätigungsdialoge und Video-Countdown bedienbar, ohne dass Schiff, Astronaut, Abbau oder Sektorstreaming im Hintergrund weiterlaufen.

## Bedienung

- Maus oder Pfeiltasten navigieren durch native Godot-Controls.
- Die jeweils eingestellten Bewegungstasten unterstützen zusätzlich die vertikale Menünavigation.
- `Enter` bestätigt die aktuelle Auswahl.
- `Escape` beendet zuerst eine Tastenaufnahme oder einen Dialog, wechselt danach aus einem Untermenü zurück und schließt zuletzt das Hauptmenü.

## Tastatur und Maus

Der Godot-unabhängige Katalog `InputActionCatalog` definiert alle 15 sichtbaren Aktionen, ihre InputMap-ID, Bezeichnung und Standardbelegung. Dazu gehören auch das gemeinsame Ein-/Aussteigen, das Inventar, der standardmäßig auf `Shift` liegende Raumschiff-Boost, das standardmäßig auf `H` liegende Befestigen beziehungsweise Lösen des Raumschiffs am Kometen und das Baumenü auf `B`. Das Baumenü verwendet die InputMap-ID `build_menu`; die frühere Aktion `build_mode` wird beim Laden alter Einstellungen migrationssicher übernommen und ist nicht parallel aktiv. Eine neue Belegung wird zunächst als Entwurf gehalten. Bei einer Doppelbelegung warnt das Menü und bietet einen konfliktfreien Tausch der beiden Belegungen an. Erst `Speichern` aktualisiert die Godot-`InputMap` und die dauerhafte Konfiguration. Bewegungsaktionen behalten Pfeiltasten als zusätzliche, feste Alternative.

Gameplay-Hinweise lesen ihre Tastenbezeichnungen direkt aus der aktuellen `InputMap`, sodass Einsteigen, Aussteigen, Karte und Abbau nach einer Änderung korrekt beschriftet bleiben. Die frühere permanente Tastenübersicht im Spiel wurde entfernt; die vollständige Belegung ist dauerhaft unter `Einstellungen → Tastatureinstellungen` erreichbar, während im Spiel nur kontextabhängige Hinweise erscheinen.

## Audio

`default_bus_layout.tres` definiert `Master`, `Music`, `SFX`, `Ambience` und `UI`. Alle fünf Werte werden sofort auf `AudioServer` angewendet und gespeichert. Gesamtton und Musik besitzen getrennte Stummschalter. Der Test-Sound wird zur Laufzeit mit `AudioStreamGenerator` erzeugt und benötigt keine externe Audiodatei.

## Video

Videoänderungen werden zunächst als Entwurf bearbeitet. `Anwenden` aktiviert Auflösung, Fenstermodus, FPS-Limit, V-Sync, Kantenglättung, Helligkeit und UI-Skalierung. Änderungen an Auflösung, Fenstermodus oder gewünschter Bildwiederholrate öffnen einen zwölfsekündigen Bestätigungsdialog. Ohne Bestätigung werden die vorherigen Werte automatisch wiederhergestellt.

Die gewünschte Bildwiederholrate wird gespeichert. Nicht jedes Betriebssystem erlaubt Godot, eine beliebige Monitorfrequenz zuverlässig zu erzwingen; die Frequenz dient deshalb zugleich als Vorgabe für spätere plattformspezifische Displayadapter. Textur-, Schatten- und Effektqualität steuern bereits die prozeduralen Schichten, Facetten, Risse und Erhöhungen der Kometen. Die Partikeldichte verändert die Zahl der Abbaupartikel und ist als gemeinsamer Hook für weitere Effekte vorbereitet.

## Speicherung

Die versionierte Konfiguration liegt unter `user://settings.json`. `JsonGameSettingsStore` fängt fehlende oder beschädigte Dateien ab und verwendet dann normalisierte Standardwerte. Die Modelle und Konfliktlogik befinden sich in `SpaceFactory.Core.Settings` und sind ohne Godot testbar; Laufzeitanwendung und Oberfläche bleiben in der Presentation-/Infrastructure-Schicht.
