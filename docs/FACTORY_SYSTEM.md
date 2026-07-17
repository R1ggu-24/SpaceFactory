# Bau-, Produktions- und Treibstoffsystem

## Spielablauf

Das Baumenü wird zu Fuss über die konfigurierbare Aktion `build_menu` (Standard `B`) geöffnet. Im Raumschiff bleibt die Aktion gesperrt. Nach der Auswahl folgt eine transparente Vorschau der Weltmaus:

- Raster: 24 Welteinheiten
- Rotation: 10 Grad pro Mausradschritt
- Linksklick: gültige Maschine bauen
- Rechtsklick oder Escape: abbrechen
- B: zur Maschinenauswahl zurückkehren

Gebaut werden kann nur auf `Large`- und `Huge`-Kometen. Die Prüfung verwendet den echten Kometentyp, den vollständigen prozeduralen Umriss, den `BuildableRadius`, Krater, Terrainunebenheit, Maschinen-Footprints sowie die aktuellen Materialien. Baukosten werden atomar und ausschliesslich aus dem Astronauteninventar entfernt. Eine ungültige Vorschau entfernt nichts. Der Aufbau reserviert die Fläche sofort und dauert abhängig von der Maschine 1,2 bis 1,8 Sekunden.

Der grobe `BuildableRadius` beträgt `0,70 × Kometenradius` auf `Large` und `0,78 × Kometenradius` auf `Huge`. Umriss, Krater und Terrain prüfen anschliessend weiterhin die genaue Fläche. Die sechs Welteinheiten Maschinen-Clearance werden je zur Hälfte auf den neuen und vorhandenen Footprint verteilt. Dadurch werden sichtbar gültige Positionen nicht mehr durch einen doppelt angesetzten Sicherheitsabstand fälschlich mit `Nicht genügend Platz` abgelehnt.

Eine nahe Maschine wird mit der konfigurierten E-Aktion geöffnet. Wenn Cockpit und Maschine gleichzeitig erreichbar sind, gewinnt das nähere Objekt. Das Panel pausiert die Welt nicht, blockiert aber Astronautenbewegung, Abbau und andere Interaktionen.

## Maschinen und Rezepte

`DefaultMachineCatalog` enthält dreizehn Maschinen in fünf erweiterbaren Kategorien:

- Verarbeitung: Zerkleinerer, Schmelzer, Giesserei, Raffinerie, Wasseraufbereiter, Elektrolyseur
- Herstellung: Konstruktor, Fabrikator
- Energie: Basisgenerator, Treibstoffgenerator, kleiner Strommast
- Lagerung: Lagercontainer
- Forschung: Forschungsstation

`DefaultRecipeCatalog` enthält 49 zentrale Rezepte. Die gemeinsame `MachineState`-Logik prüft vollständige Eingaben und freien Ausgaberaum, entfernt Zutaten erst beim Start eines gültigen Zyklus und erzeugt Ausgaben atomar. Rezept und Startschalter bleiben im Snapshot erhalten. Die UI lädt jeweils einen vollständigen Rezeptstapel und entnimmt Ausgaben nur, wenn das Astronauteninventar alles aufnehmen kann. Sowohl loser Treibstoff als auch Wasserstoffbehälter können in der Raffinerie verlustfrei in Treibstoffbehälter überführt werden.

Der Lagercontainer verwendet 30 Slots. Alle Inventare behalten das zentrale Stapellimit von 200. Produkte, Rohstoffe und Behälter werden über denselben `ItemId`- und `ItemPresentationCatalog` dargestellt. Beim Ein- und Auslagern wird so viel wie sicher hineinpasst übertragen; der Rest bleibt unverändert zurück und kann in weiteren Durchgängen entnommen werden, auch wenn das 20-Slot-Astronauteninventar kleiner als der Container ist.

Die Weltansichten der zwölf Maschinen verwenden individuelle Top-down-Silhouetten im gemeinsamen dunklen Titan-/Graphitstil. Bewegte Walzen, Hitze, Guss- und Montagearme, Pumpen, Blasen, Turbinen oder Scanner laufen nur, wenn die zugehörige Maschine wirklich produziert. Kleine Statusleuchten zeigen Bereitschaft, Produktion und Wartezustände; die Darstellung verändert keine Core-Zustände.

## Transport und Verbindungen

Der sechste Baumenübereich `TRANSPORT` enthält Stromkabel, Förderband, Flüssigkeitsrohr und Gasrohr. Nach der Auswahl wird zuerst die Quellmaschine und mit einem zweiten Linksklick die Zielmaschine gewählt. Beide Maschinen müssen auf demselben Kometen liegen, kompatible Anschlüsse besitzen und höchstens 900 Welteinheiten voneinander entfernt sein. Eine erfolgreiche Verbindung verbraucht genau ein passendes Element aus dem Astronauteninventar; ungültige Versuche verändern weder Inventar noch Netzwerk.

- Stromkabel bilden echte zusammenhängende Stromkomponenten. Generatorleistung erreicht nur Maschinen innerhalb derselben kabelbasierten Komponente.
- Förderbänder übertragen ausschliesslich feste Gegenstände gerichtet vom Ausgang zum Eingang, maximal 8 Items pro Sekunde.
- Flüssigkeitsrohre übertragen ausschliesslich flüssige Produkte und Gasrohre ausschliesslich gasförmige Produkte, jeweils maximal 20 Einheiten pro Sekunde.
- Flüssigkeits- und Gasleitung verwenden dasselbe baubare Transportrohr, bleiben im Netzwerk aber getrennte, nicht kompatible Verbindungstypen.
- Produktionsports werden aus den tatsächlichen Rezepten und Stoffphasen abgeleitet. Jeder Stromanschluss erlaubt genau ein Kabel; der Strommast besitzt sechs getrennte sichtbare Anschlüsse, die intern ein gemeinsames Netzsegment bilden. Lager besitzt bidirektionale Ports pro Stoffphase.

Neue Spielstände enthalten im Raumschifflager je fünf Stromkabel, fünf Förderbänder und fünf Transportrohre. Zum Bauen müssen die benötigten Elemente wie andere Baumaterialien in das Astronauteninventar übertragen werden. Transfers prüfen Quellmenge und Zielkapazität vorab und rollen einen fehlgeschlagenen Zieltransfer zurück; Gegenstände werden daher weder dupliziert noch gelöscht. Weitere Architekturdetails stehen in [LOGISTICS_SYSTEM.md](LOGISTICS_SYSTEM.md).

## Stoffe und Behälter

Wasser, Wasserstoff, Sauerstoff und Treibstoff sind echte Gegenstände. Transportbehälter fassen zentral 100 Einheiten und unterscheiden leere Wasser-, Gas- und Treibstoffbehälter von gefüllten Wasserstoff-, Sauerstoff-, Wasser- und Treibstoffbehältern. Rezeptausgaben und zurückgegebene Behälter werden als eine gemeinsame atomare Ausgabe behandelt, wodurch nichts dupliziert oder gelöscht wird.

## Energie und Forschung

Stromkabel zerlegen die Maschinen eines Kometen in echte getrennte Netzkomponenten. Jede Komponente wird durch `ConnectedPowerGridSimulation` als eigenes Netz mit eigenem Schalter, Überlastschutz und 60-Sekunden-Messverlauf simuliert; nicht verbundene Maschinen teilen keine Generatorleistung:

- Basisgenerator: 12 kW, erster Bau kostenlos, danach normale Kosten
- Treibstoffgenerator: 60 kW, ein Treibstoffbehälter liefert 120 Generatorsekunden
- Produktionsstrom wird nur bei einem tatsächlich laufenden Zyklus abgerechnet
- fehlende Materialien, fehlende Energie und volle Ausgaben pausieren, ohne den Startschalter auszuschalten
- überlastete Netze lösen nach `1,5 s` den Schutzschalter aus und liefern bis zum manuellen Wiedereinschalten keinen Strom
- Raumschiffanschluss A und B sind als getrennte externe Quellen mit je `40 kW` vorbereitet und verbrauchen nur gelieferte Leistung aus demselben Schiffstank (`0,0005` Treibstoff pro kW-s)

Die Forschungsstation nutzt sechs zentral definierte Forschungen mit Materialkosten, Voraussetzungen, Dauer, Leistungsbedarf sowie freigeschalteten Maschinen und Rezepten. Forschungsfortschritt und abgeschlossene Technologien werden gespeichert.

## Raumschifftank

Der Tank besitzt 1800 Treibstoffeinheiten. Aktiver Shift-Boost verbraucht 1 Einheit pro Sekunde; ein voller Tank reicht daher genau 1800 Sekunden beziehungsweise 30 Minuten. Ein letzter Teil-Frame verbraucht den verbleibenden Rest, statt fast eine Einheit unbrauchbar im Tank zu lassen. Normalflug, ein befestigtes Schiff und durch Kollision vollständig blockierte Boostbewegung verbrauchen nichts. Bei leerem Tank bleibt Normalflug möglich.

Im Raumschifflager leert `Tank auffüllen` nur vollständige Treibstoffbehälter, die komplett in den Tank passen. Pro Behälter werden 100 Einheiten übertragen und ein leerer Treibstoffbehälter atomar zurückgegeben.

## Chunk-Lebenszyklus und Speicherung

`FactoryRuntimeController` organisiert Zustände über einen Kometenindex nach Kometen-ID. Nur Maschinen geladener Kometen besitzen `MachineView`-Nodes und vollständige Kollisions-/Animationsdarstellung. Beim Entladen werden lediglich die Views entfernt; `MachineState` bleibt erhalten. Beim erneuten Laden wird die Maschine relativ zum Kometen an derselben Position und Rotation erzeugt. Simulationszeitstempel werden nur für tatsächlich bebaute Kometen geführt, damit eine praktisch unendliche Reise den Spielstand nicht mit leeren Kometen anwachsen lässt. Die Topologie des lokalen Stromnetzes wird pro Komet wiederverwendet und nur bei neuen Maschinen erweitert.

`JsonFactoryStateStore` speichert versioniert unter `user://factory_state.json`:

- alle Maschinen-Snapshots und Inventarslots
- alle Verbindungen mit Typ, Quell-/Zielmaschine und ihren konkreten Ports
- Bau- und Produktionsfortschritt
- Rezept, Startschalter und Generatorrestlaufzeit
- Forschung und erster kostenloser Basisgenerator
- Raumschifftank
- Astronauten- und Raumschifflager mit exakten Slotpositionen
- Bindung einer laufenden Forschung an ihre konkrete Forschungsstation
- Stromnetz-Schalter und Schutzschalterzustände
- Raumschiff-Stromanschluss-Schalter A/B
- Raumschiff-Dockingpose für spätere Wiederherstellung
- letzter UTC-Simulationszeitpunkt pro Komet

Das aktuelle Factory-Schema ist Version `4`. Spielstände der Versionen 1 bis 3 werden migriert. Persistierte Verbindungen werden erst nach ihren Maschinen wiederhergestellt und erneut auf vorhandene, kompatible Endpunkte geprüft.

Entladene Fabriken und eine exakt an ihre Station gebundene Forschung werden beim Laden in leichten 10-Sekunden-Schritten nachsimuliert. Um lange Ladehänger zu vermeiden, ist ein einzelner Nachholvorgang auf eine Stunde begrenzt. Die reguläre aktive Simulation läuft im 0,1-Sekunden-Takt; gespeichert wird bei Änderungen gebündelt frühestens nach einer Sekunde. Ein fehlgeschlagener Schreibvorgang lässt den Zustand als ungespeichert markiert und wird erneut versucht.

## Zentrale Dateien

- Core: `src/SpaceFactory.Core/Production`, `Power`, `Research`, `Construction`, `Ships/Fuel`
- Runtime und Platzierung: `scripts/presentation/building`
- UI: `scenes/ui/building`
- Persistenz: `scripts/application/factory` und `scripts/infrastructure/persistence`
- Integration: `scripts/presentation/bootstrap/GameRoot.cs`

## Prüfungen

Die Core-Tests decken Kataloge, Produktionszyklen, atomare Materialtransfers, kabelbasierte Stromkomponenten, sechs Strommastports, getrennte Netze, Überlastung, Raumschiffanschlüsse, Portkompatibilität, gerichteten Feststoff-/Flüssigkeits-/Gastransport, Generatorbrennstoff, Forschung, Tankverbrauch und Betankung ab. Die Headless-Smokes prüfen zusätzlich UI-Konstruktion, 24er-Raster, 10-Grad-Rotation, Kometenoberflächen, B nur zu Fuss, Startausrüstung, nicht pausierende UI-Zustände, Tankverbrauch sowie den versionierten Schema-v4-Persistenz-Roundtrip.
