# Ressourcen- und Abbausystem

## Datenkatalog

Alle 25 Ressourcentypen werden zentral in `data/resources/resource_definitions.json` konfiguriert. Eine Definition enthält eine stabile ID, Anzeigename, Seltenheit, Spawngewicht, Farbe, Darstellungsstil, Abbauzeit, Härte, Ertragsbereich, das einheitliche Stack-Limit von 200, Audio-/Partikel-Hooks und vorbereitete spätere Verwendungen. Neue Ressourcen benötigen keine Änderung am Generator, solange ihre Definition das bestehende Schema erfüllt.

Die fünf Darstellungsstile erzeugen Adern, Kristalle, metallische Einschlüsse, Eisflächen oder Mineralschichten pro Vorkommen aus einem eigenen Seed. Unregelmäßige Wirtsgestein-Kanten, kleine Schatten und sparsame Materialreflexe verbessern die Lesbarkeit, ohne starke Leuchteffekte zu erzeugen. Position, Skalierung und Details bleiben damit reproduzierbar, ohne identische Texturen zu wiederholen.

## Deterministische Verteilung

`ResourceDepositGenerator` verwendet den stabilen visuellen Seed eines Kometen. Größe und Seltenheit bestimmen Anzahl und Auswahl der Ressourcentypen sowie die Zahl der Vorkommen. Kleine Kometen tragen wenige überwiegend häufige Rohstoffe; große und riesige Kometen besitzen mehr Vorkommen und bessere Chancen auf seltene Materialien. Mindestens ein sehr häufiger Grundrohstoff wird garantiert.

Vorkommen werden in lockeren Gruppen platziert, dürfen sich nicht überlappen und bleiben innerhalb der Kometenoberfläche. Bei landbaren Kometen hält die Generierung die zentrale Fläche für spätere Landung und Fabrikbau frei. Die Berechnung geschieht nur beim Laden eines Sektors, nicht in jedem Frame.

## Abbau und Inventar

Der Astronaut visiert ein Vorkommen mit der Maus an und hält die linke Maustaste. Der Abbau funktioniert nur in Reichweite und wird beim Loslassen, beim Zielwechsel, beim Öffnen einer blockierenden UI oder beim Verlassen der Reichweite abgebrochen. Die nötige Zeit stammt aus der Ressourcendefinition. Fortschritt, Zielname und Ertrag erscheinen im HUD; Werkzeugstrahl, Schadensrisse und Partikel liefern direktes Feedback. Die Schadensdarstellung wird nur neu gezeichnet, wenn eine weitere sichtbare Rissstufe erreicht wird, statt die komplette prozedurale Vorkommensgrafik in jedem Fortschritts-Frame neu aufzubauen.

Das Astronauten-Inventar besitzt 20 feste Slots mit maximal 200 Einheiten pro Stapel. Vorhandene Teilstapel werden zuerst gefüllt, danach folgen freie Slots. Ist nicht genügend Platz für den vollständigen Abbauertrag vorhanden, bleibt das Vorkommen bestehen. Im Raumschiff ist zusätzlich ein unabhängiges Lager mit 50 Slots erreichbar; Transfers verwenden in beiden Richtungen dieselbe verlustfreie Core-Logik. Details stehen in [INVENTORY.md](INVENTORY.md). Die aktuelle Audioanbindung ist absichtlich ein Hook: `ResourceDepositView.AudioCueRequested` meldet Start und Abschluss, sodass endgültige Sounds später ohne Kopplung an die Weltgenerierung angeschlossen werden können.

## Persistenz

Die unveränderte Ressourcenwelt wird jederzeit aus Seeds rekonstruiert. `JsonResourceStateStore` speichert ausschließlich geänderte Restmengen in `user://resource_deposits.json`. Beim erneuten Betreten eines Sektors wird dieser Zustand über die stabile Vorkommens-ID angewendet. Das hält den Spielstand klein und verhindert, dass abgebaute Vorkommen beim Chunk-Streaming zurückkehren.
