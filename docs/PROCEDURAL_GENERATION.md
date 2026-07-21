# Prozedurale Generierung

## Deterministische Sektoren

Die praktisch unendliche Welt ist in Sektoren von standardmäßig 5000 × 5000 Welteinheiten unterteilt. Aus Welt-Seed, Sektor-X/Y, System-Salt und festen 64-Bit-Konstanten entsteht für jeden Abschnitt ein eigener SplitMix64-Zustand. Es gibt keinen globalen Zufallsgenerator; Lade-Reihenfolge und bisherige Flugstrecke verändern deshalb kein Ergebnis.

Jeder felsige Komet erhält eine stabile ID sowie reproduzierbare Werte für Position, Größe, Ressource, Rotation, Form-Seed, Rauheit, Höhenvariation und Krater. Große, ausreichend ebene Körper erhalten zusätzlich `SupportsLanding`. Dieses Flag ist nur eine Vorbereitung; Landen und Bauflächen sind noch nicht implementiert.

## Natürliche Verteilung

Der `CometFieldPlanner` plant explizite Kometenfelder statt einer weltweiten gleichmäßigen Dichte. Eine große Planungszelle umfasst standardmäßig 16 × 16 Sektoren und besitzt mit 76 Prozent Wahrscheinlichkeit einen Feldkandidaten. Dessen Mittelpunkt wird innerhalb der Zelle zufällig verschoben. Haupt- und Nebenradius, Rotation, Intensität und verlangter Randabstand sind ebenfalls seedbasiert variabel, sodass elliptische Felder unterschiedlicher Form und Dichte entstehen und das Planungsraster nicht sichtbar wird.

Feldkandidaten benachbarter Zellen vergleichen stabile Prioritäten. Überlappen ihre Ausschlussradien, bleibt nur der höher priorisierte Kandidat. Zwischen akzeptierten Feldern liegen dadurch zufällige Mindestlücken von drei bis sieben Sektoren. Außerhalb der Felder besteht pro Sektor nur eine Chance von 2,0 Prozent auf einen einzelnen kleinen Kometen. Gegenüber den vorherigen Werten steigen beide zentralen Wahrscheinlichkeiten damit um rund elf Prozent, während Lücken, Kollisionsabstände und die separate Riesenkometenkarte unverändert bleiben.

Damit der feste Spielstart nicht zufällig in einer extrem langen Leerstelle liegt, besitzt die Startregion ein priorisiertes, deterministisches Entdeckungsfeld. Es beginnt außerhalb des sicheren Startsektors und liegt ungefähr zwei bis vier Sektoren in positiver X-Richtung. Der Spieler muss dadurch zunächst fliegen, findet aber nach kurzer Strecke garantiert mehrere Kometen. Das Feld verdrängt überlappende Zufallsfelder, ohne die globale Dichte außerhalb der Startregion zu erhöhen.

In normalen Feldern werden `Tiny` und `Small` gemeinsam mit ungefähr 65 Prozent, `Medium` mit 25 Prozent und `Large` mit 9 Prozent gewählt. `Huge` ist aus dieser normalen Ziehung vollständig ausgeschlossen.

Extrem große Kometen verwenden eine separate Seltenheitskarte. Ein Sektor darf nur dann einen solchen Körper erzeugen, wenn sein stabiler Seltenheitswert innerhalb von sechs Sektoren in jeder Richtung das lokale Maximum ist. Auch ihr Kandidaten-Seed ist von der normalen Felddichte getrennt. Dadurch verändern Dichteanpassungen weder Lage noch Aussehen der Riesen, und zwei Riesen können niemals direkt nebeneinander entstehen. `WorldGenerationDefaults.LargeCometScaleMultiplier` vergrößert große und riesige Kometen zentral linear um 1,5. Riesen erreichen damit Radien von 1950 bis 2700 Welteinheiten und mindestens 1267,5 Welteinheiten bebaubaren Radius.

## Überlappungsfreiheit

Jeder Sektor erzeugt zunächst deterministische Kandidaten. Die notwendige Nachbarschaft wird dynamisch aus maximalem Kometenradius, Sicherheitsabstand und Sektorgröße berechnet; mit den aktuellen 1,5-fach skalierten Riesen werden auch Kandidaten bis zwei Sektoren Entfernung verglichen. Jeder Kandidat besitzt eine stabile Zufallspriorität. Überschneiden sich zwei Sicherheitsradien, bleibt ausschließlich der Kandidat mit der höheren Priorität bestehen. Die Entscheidung ist dadurch unabhängig davon, welcher Sektor zuerst geladen wurde, und funktioniert auch über Sektorgrenzen hinweg.

Der Mindestabstand setzt sich aus beiden Kometenradien und `minimumCometSpacing` zusammen. Der Startpunkt des Schiffs besitzt zusätzlich eine reproduzierbare Sicherheitszone. Spätere Fabriken oder abgebaute Körper werden als gespeicherte Weltänderungen über dieses unveränderte Grundlayout gelegt.

## Darstellung

Godot erzeugt aus den Core-Daten pro Komet einen unregelmäßigen Polygonumriss. Drei Wellenfrequenzen und lokales Rauschen variieren die Form, ohne den gemeinsamen Stil zu verlieren. Rauheit und Höhenvariation steuern unregelmäßige Gesteinsfelder, helle und dunkle Schichten, Kantenrippen, Mikrokrater und Rissnetze. Die Core-Kraterdaten bestimmen Lage, stark variierende Größe, Tiefe und Ausrichtung der großen Krater. Nahe Sektoren verwenden den detaillierten Umriss als Kollisionsfläche; entfernte Sektoren reduzieren ihn deterministisch auf zehn Punkte und deaktivieren die dort nicht nutzbaren Ressourcen-Interaktionsflächen.

Landbare Kometen besitzen ein `AsteroidSurfaceProfile` mit stabiler Surface-ID, begehbarem und bebaubarem Radius, vorgeschlagener Anzahl von Landezonen, getrennten Seeds für Terrain und Ressourcen sowie einem dauerhaften Persistenzschlüssel. Diese Daten bilden später die Grenze zwischen Weltraumansicht und begehbarer Oberfläche. Landen, Gebäudeplatzierung, Abbau und Save-Deltas sind noch nicht implementiert.

## Streaming

Die Präsentationsschicht hält maximal eine 5×5-Sektorfläche aktiv. Beim Schiffsflug wird deren Mittelpunkt um einen Sektor in Flugrichtung verschoben. Dadurch werden neue Inhalte deutlich außerhalb des sichtbaren Bereichs vorgeladen, während weit zurückliegende Sektoren aus dem Szenenbaum entfernt werden. Der Streamingzustand wird höchstens fünfmal pro Sekunde geprüft, nicht mit vollständiger Generierungsarbeit in jedem Frame. Kollisions-LOD wird nur neu an Godot übergeben, wenn sich die Detailstufe eines Sektors tatsächlich ändert.

Beim erneuten Betreten werden dieselben Sektoren aus Seed und Koordinate neu aufgebaut. Persistente Änderungen werden später nach der Grundgenerierung als Sektordeltas angewendet.
