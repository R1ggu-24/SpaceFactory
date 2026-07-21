# Ressourcen- und Abbausystem

## Datenkatalog

Alle 25 Ressourcentypen werden zentral in `data/resources/resource_definitions.json` konfiguriert. Eine Definition enthält eine stabile ID, Anzeigename, Seltenheit, Spawngewicht, Farbe, Darstellungsstil, Abbauzeit, Härte, Ertragsbereich, das einheitliche Stack-Limit von 200, Audio-/Partikel-Hooks und vorbereitete spätere Verwendungen. Neue Ressourcen benötigen keine Änderung am Generator, solange ihre Definition das bestehende Schema erfüllt.

Die fünf Darstellungsstile erzeugen Adern, Kristalle, metallische Einschlüsse, Eisflächen oder Mineralschichten pro Vorkommen aus einem eigenen Seed. Unregelmäßige Wirtsgestein-Kanten, kleine Schatten und sparsame Materialreflexe verbessern die Lesbarkeit, ohne starke Leuchteffekte zu erzeugen. Position, Skalierung und Details bleiben damit reproduzierbar, ohne identische Texturen zu wiederholen.

## Deterministische Verteilung

`ResourceDepositGenerator` verwendet den stabilen Geologie- und Surface-Seed eines Kometen. Mittlere, große und riesige Kometen besitzen immer ein bis drei normale, unendliche Erzquellen. Größe, Geologie und Seltenheit bestimmen die Ressourcenauswahl; Uran bleibt beispielsweise radiogenen großen oder riesigen Kometen vorbehalten.

Zusätzlich wird pro normaler Quelle mit 50 Prozent Wahrscheinlichkeit ein kleiner endlicher Erzstein erzeugt. Seine stabile `:ore-stone:v1:`-ID, Position, Erzart und fünf bis sieben Treffer sind vollständig deterministisch. Jeder erfolgreiche Treffer liefert seedbasiert fünf bis zehn Einheiten. Normale Quellen, Erzsteine und ihre Sicherheitsabstände werden gemeinsam platziert, sodass sie nicht überlappen und vollständig auf dem Kometen liegen.

Vorkommen werden in lockeren Gruppen platziert, dürfen sich nicht überlappen und bleiben innerhalb der Kometenoberfläche. Bei landbaren Kometen hält die Generierung die zentrale Fläche für spätere Landung und Fabrikbau frei. Die Berechnung geschieht nur beim Laden eines Sektors, nicht in jedem Frame.

## Abbau und Inventar

Der Astronaut visiert ein Vorkommen mit der Maus an und hält die linke Maustaste. Der Abbau funktioniert nur in Reichweite und wird beim Loslassen, beim Zielwechsel, beim Öffnen einer blockierenden UI oder beim Verlassen der Reichweite abgebrochen. Die nötige Zeit stammt aus der Ressourcendefinition. Fortschritt, Zielname und Ertrag erscheinen im HUD; Werkzeugstrahl, Schadensrisse und Partikel liefern direktes Feedback. Die Schadensdarstellung wird nur neu gezeichnet, wenn eine weitere sichtbare Rissstufe erreicht wird, statt die komplette prozedurale Vorkommensgrafik in jedem Fortschritts-Frame neu aufzubauen.

Das Astronauten-Inventar besitzt 24 feste Slots, eine separate Hotbar mit 6 Slots und vier reine Werkzeugslots. Der Abbau startet nur, wenn der Hand-Slot aktiv ist und dort das nicht stapelbare Erz-Abbauwerkzeug ausgewählt wurde. Stapelbare Rohstoffe verwenden maximal 200 Einheiten pro Slot; vorhandene Teilstapel werden zuerst gefüllt, danach folgen freie Slots. Ist nicht genügend Platz für den vollständigen Abbauertrag vorhanden, bleibt das Vorkommen bestehen. Im Raumschiff ist das unabhängige Lager mit 56 Slots zusammen mit dem persönlichen Inventar als Transferansicht erreichbar. Details stehen in [INVENTORY.md](INVENTORY.md). Die aktuelle Audioanbindung ist absichtlich ein Hook: `ResourceDepositView.AudioCueRequested` meldet Start und Abschluss, sodass endgültige Sounds später ohne Kopplung an die Weltgenerierung angeschlossen werden können.

## Persistenz

Die unveränderte Ressourcenwelt wird jederzeit aus Seeds rekonstruiert. `JsonResourceStateStore` speichert ausschließlich geänderte Restmengen beziehungsweise Treffer in `user://resource_deposits.json`, zusammen mit ID, Erzart, Position und Vorkommenstyp. Beim erneuten Betreten eines Sektors wird dieser Zustand über die stabile Vorkommens-ID angewendet. Ein vollständig abgebauter Erzstein bleibt deshalb auch nach Chunk-Wechsel und Neustart dauerhaft verschwunden.
