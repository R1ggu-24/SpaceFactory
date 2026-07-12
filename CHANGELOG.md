# Changelog

Alle wesentlichen Änderungen werden nach Keep a Changelog dokumentiert.

## [Unreleased]

### Added

- Godot-.NET-Projekt, technische Weltraumdemo und Input-Actions
- Dezenter schwarzer Sternenhintergrund mit wenigen, unterschiedlich hellen Sternen
- Eigenständiges freigestelltes Spielerschiff-Sprite mit Kollision und WASD-Steuerung
- Freigestellte Top-down-Astronautenfigur mit eigenem On-Foot-Controller und Moduswechsel über `F`
- Unendliche deterministische Kometenfelder mit regionaler Dichte, Leerzonen und sektorübergreifender Kollisionsvermeidung
- Prozedurale Kometenumrisse, Oberflächenvariation, Krater, Rotation, Kollision und vorbereitete Lande-Metadaten
- Gerichtetes 5×5-Chunk-Streaming mit zeitlich begrenzten Zustandsprüfungen
- Zuverlässiger Windows-Launcher für Build, direkten Spielstart und Godot-Editor
- Hintergrund und Sternenkarte in kameraunabhängige Canvas-Layer verschoben
- Kometendichte reduziert und regionale Leerzonen vergrößert
- Stabile Oberflächen-, Terrain-, Ressourcen- und Persistenzmetadaten für spätere Lande- und Fabriksysteme ergänzt
- Kometen um Gesteinsschichten, Risse, unregelmäßige Felder, Farbstreuung und Kantenrippen erweitert
- Kollisionsgeometrie entfernter Sektoren auf zehn Punkte reduziert
- Astronautenbewegung verlangsamt und um kontrollierte Beschleunigung, Abbremsung und Drehträgheit ergänzt
- Seltene `Huge`-Kometen auf 1300–1800 Welteinheiten Radius vergrößert und bei Platzierungskonflikten priorisiert
- Expliziten deterministischen Kometenfeld-Planer mit elliptischen Clustern und zufälligen Lücken von drei bis sieben Sektoren ergänzt
- Normale Feldgrößen auf ungefähr 65 % klein, 25 % mittel und 9 % groß abgestimmt; Riesen separat über lokale Seltenheitsmaxima verteilt
- Sicheres deterministisches Entdeckungsfeld wenige Sektoren vom Start ergänzt, damit der erste Kometenkontakt garantiert erreichbar ist
- Prozedurale Kometendarstellung an einen dunklen realistischen Felsstil mit gerichteter Beleuchtung, tiefen Kratern, gebrochenen Facetten und organisch gestreckten Silhouetten angepasst
- Datengetriebener Katalog mit 25 Ressourcentypen, Seltenheiten, Gewichten, Farben, Abbauzeiten, Härte und späteren Verwendungszwecken ergänzt
- Deterministische, größenabhängige Ressourcenvorkommen mit garantierten Grundressourcen, seltenen Spezialfunden und freien Flächen auf landbaren Kometen ergänzt
- Abbau durch gehaltene linke Maustaste mit Reichweitenprüfung, Fortschritt, Abbruchbedingungen, Werkzeuganimation, Partikelfeedback und Audio-Hooks ergänzt
- Größenangepassten Astronauten, sicheren Ausstieg und distanzabhängigen Wiedereinstieg über `E` ergänzt
- Ressourcen-HUD und begrenztes Astronauten-Inventar mit individuellen Stack-Limits ergänzt
- Persistente Vorkommens-Deltas unter `user://resource_deposits.json` ergänzt, damit abgebaute Ressourcen nach Sektor-Neuladen entfernt bleiben
- Futuristisches Einstellungsmenü mit Hauptseite, Tastatur-, Audio- und Video-Untermenüs sowie Bestätigungsdialog zum Beenden ergänzt
- Vierzehn dauerhaft speicherbare Tastatur-/Mausaktionen mit Konflikterkennung, Tauschfunktion, Standardbelegungen und sofortiger InputMap-Aktualisierung ergänzt
- Audio-Bus-Layout mit fünf Lautstärkereglern, Stummschaltern und prozeduralem UI-Testton ergänzt
- Videoeinstellungen für Auflösung, Fenstermodus, FPS, V-Sync, Qualitätsstufen, Kantenglättung, Helligkeit und UI-Skalierung ergänzt
- Zeitlich abgesicherte Bestätigung für riskante Videoänderungen mit automatischer Rücksetzung ergänzt
- Welt-Prozesszweig explizit pausierbar gemacht, damit hinter geöffneten Einstellungen keine Bewegung, Interaktion oder Generierung weiterläuft
- Versionierte Einstellungen werden unter `user://settings.json` gespeichert und beim Start wiederhergestellt
- Godot-unabhängiger Core für Inventar, Schiff und deterministische Sektoren
- Automatisierte Core-Tests, Beispieldaten, Dokumentation und CI
