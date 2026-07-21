# Produktions-, Mining- und Fortschrittsarchitektur

Stand: 17. Juli 2026

Dieses Dokument beschreibt den aktuellen technischen Stand nach der Erweiterung des Produktionssystems. Es trennt bewusst zwischen vollständig in den Spielablauf eingebundenen Funktionen, belastbaren Core-Modellen und vorbereiteten Erweiterungspunkten. Katalogumfang allein bedeutet nicht automatisch, dass jeder Inhalt bereits abschließend ausbalanciert oder mit einer eigenen Spezialoberfläche versehen ist.

## Statusbegriffe

- **Spielbar**: Core, Laufzeit, UI und Speicherung sind miteinander verbunden und im normalen Spielablauf erreichbar.
- **Implementiert**: Das Verhalten ist im Core vorhanden und getestet; Präsentation oder Balancing können noch weiter verfeinert werden.
- **Vorbereitet**: Stabile IDs, Metadaten oder Schnittstellen existieren, aber ein weiterführendes Spielsystem nutzt sie noch nicht vollständig.
- **Bewusste Grenze**: Nicht Teil dieser Ausbaustufe oder noch nicht durchgängig integriert.

## Verifizierter Inhaltsumfang

Die Zahlen beziehen sich auf die tatsächlich materialisierten Standardkataloge, nicht nur auf Mindestwerte in Tests.

| Katalog | Aktueller Umfang | Herleitung |
| --- | ---: | --- |
| Ressourcen in `resource_definitions.json` | 26 | 25 bestehende Rohstoffe plus Uranerz |
| Produktionsgegenstände | 135 | alle stabilen Felder in `ProductionItemIds` besitzen genau eine Definition |
| Rezepte | 198 | 144 bisherige Verarbeitungs-/Herstellungsrezepte, zwei neue Endgame-Rezepte und 26 Rohstoffarten × zwei Abbauvarianten |
| Maschinen | 28 | alle stabilen IDs in `MachineDefinitionIds` |
| Forschungen | 17 | ein azyklischer Baum über zehn Technologiestufen |
| Stoffphasen | 3 | Feststoff, Flüssigkeit, Gas |
| Forschungsbereiche | 9 | Grundlagen, Mining, Automatisierung, Metallurgie, Elektronik, Chemie, Energie, Nuklear, Weltraumtechnik |

## Ausgangslage vor der Erweiterung

Das Projekt besaß bereits eine brauchbare vertikale Architektur:

- stabile starke IDs für Items, Rezepte, Maschinen, Forschung und Verbindungen;
- zentrale Kataloge statt maschinenspezifischer Produktionslogik;
- einen gemeinsamen `MachineState` für Bau, Rezeptwahl, Input, Output, Fortschritt, Energie und Snapshots;
- atomare Inventaroperationen, die Zutaten erst nach vollständiger Vorprüfung entfernen;
- getrennte kabelbasierte Stromkomponenten und phasenabhängige Transportports;
- eine Godot-Laufzeitschicht für Platzierung, Maschinenansichten, Menüs, Chunk-Lebenszyklus und Speicherung.

Der vorherige Standardumfang war jedoch deutlich kleiner:

| Bereich | Vorheriger Stand | Engpass |
| --- | ---: | --- |
| Produktionsgegenstände | 59 | wenige Zwischenprodukte; kaum Chemie, Hightech, Abfall oder Werkzeuggestufung |
| Rezepte | 49 | überwiegend kurze lineare Ketten |
| Maschinen | 13 | keine Miner, spezialisierten Tanks, Batterie, Präzisions- oder Nuklearmaschinen |
| Forschungen | 6 | keine Kategorien, Stufen, Entdeckungsbedingungen oder Alternativgruppen |
| Erzvorkommen | je nach Komet 1 bis 24 endliche Vorkommen variabler Größe | Standortwahl beruhte auf Menge statt nachhaltiger Förderrate |
| Mining | nur manuell | keine kontinuierliche Förderung und keine Reinheit |

Weitere strukturelle Engpässe waren ein Itemmodell ohne Gefahr-/Eindämmungsdaten, Rezepte ohne Technologiestufe oder Quellenbindung, Maschinen ohne Archetyp, Platzierungsprofil, Geschwindigkeits-/Effizienzwert und Abschirmung sowie Save-Snapshots ohne Minerquelle, interne Akkuladung, Netzspeicher, Entdeckungen oder Strahlendosis.

Die Erweiterung verwendet die vorhandenen Katalog-, `MachineState`-, Verbindungs-, Strom- und Persistenzpfade weiter. Es existiert kein zweites paralleles Produktions- oder Forschungssystem.

## Architektur und Datenfluss

```text
Ressourcen-JSON + Weltseed
        |
        v
ResourceDepositGenerator ---> ResourceDepositDefinition/Purity
        |                                  |
        |                                  +--> manueller Abbau
        |                                  +--> ExtractionSourceBinding
        v
ProductionItemCatalog ---> RecipeCatalog ---> MachineState
                                  |              |
ResearchCatalog/State ------------+              +--> Input/Output/Batch
                                                 +--> interner Miner-Akku
MachineConnectionNetwork ----------------------->+--> Materialtransport
        |
        +--> ConnectedPowerGridSimulation ------> Produktion/Forschung/Batterie
                                                        |
                                                        v
                                         FactoryRuntimeController
                                                        |
                                         FactoryStateData Schema v6
```

Die Core-Schicht bleibt unabhängig von Godot. Die Präsentationsschicht übersetzt Katalog- und Zustandsdaten in Baumenü, Maschinenpanel, HUD, Weltansicht und Interaktionen. Der Factory-Store serialisiert ausschließlich veränderlichen Zustand; die unveränderte Welt wird weiterhin aus Seeds rekonstruiert.

## Neue und erweiterte Datenmodelle

### Gegenstände

`ProductionItemDefinition` beschreibt zentral:

- stabile Item-ID, Anzeigename, Kategorie und Icon-Key;
- individuelles Stapellimit, insbesondere `1` für Werkzeuge und Schutzanzüge;
- Phase `Solid`, `Liquid` oder `Gas`;
- Gefahr `None`, `Chemical` oder `Radioactive` samt Stärke;
- erforderliche Eindämmung `ChemicalResistant`, `RadiationShielding` oder `ShieldedWasteContainer`;
- optional einen wiederverwendbaren Transportbehälter mit Stoff, Füllmenge, Kapazität und leerem Rückgabegegenstand.

Die 135 Definitionen decken Rohstoffe, zerkleinerte Erze, Barren, Legierungen, Grundbauteile, Elektronik, Chemikalien, Forschungsprodukte, Miner-Bausätze, Werkzeuge, Schutzanzüge, Weltraumkomponenten, Nuklearbrennstoff, Abfälle sowie Tiefenraum-Steuerkern und Quanten-Navigationsmodul ab.

### Rezepte

`RecipeDefinition` enthält zusätzlich zu Ein-/Ausgaben, Dauer und Leistung:

- Technologiestufe 1 bis 10;
- optionale direkte Forschungsvoraussetzung;
- stabile Alternativgruppe;
- Tags wie `alternative`, `special-resource`, `radioactive`, `research-pack` oder `endgame`;
- zurückgegebene Behälter als Teil derselben atomaren Ausgabe;
- bei Extraktionsrezepten eine `SourceResourceId` statt normaler Zutaten.

`CombinedOutputs` fasst Produkte und Rückgabebehälter für die Kapazitätsprüfung zusammen. Eine Maschine startet daher keinen Zyklus, wenn später ein Produkt oder Rückgabebehälter verloren gehen würde.

### Maschinen

`MachineDefinition` ist um folgende Daten erweitert:

- `MachineArchetype`, etwa Extractor, ChemicalProcessing, PrecisionManufacturing, EnergyStorage oder NuclearProcessing;
- `MachinePlacementRequirement`: normal bebaubar, nur großer/riesiger Komet oder direkt auf einer Rohstoffquelle;
- Technologiestufe;
- Geschwindigkeits- und Effizienzmultiplikator;
- Strahlungsabschirmung;
- weiterhin zentral definierte Baukosten, Slotzahlen, Bauzeit, Generatorleistung und Brennstoffdaten.

`MachineStateSnapshot` speichert nun außerdem die gebundene Extraktionsquelle, interne Minerenergie und gespeicherte Netzenergie.

### Forschung

`ResearchDefinition` besitzt Kategorie, Stufe, Voraussetzungen, Materialkosten, Dauer, Leistungsbedarf, freigeschaltete Maschinen/Rezepte, erforderliche Ressourcenentdeckungen, Alternativgruppen und Tags. `ResearchCatalog` erstellt eine deterministische topologische Reihenfolge und lehnt Zyklen ab.

`ResearchState` verwaltet genau eine aktive globale Forschung, abgeschlossene Technologien und entdeckte Ressourcen. Materialien werden beim Start atomar verbraucht; fehlende Voraussetzungen oder Entdeckungen verändern das Inventar nicht.

## Erzquellen und Reinheit

### Deterministische Quellen

**Spielbar.** `ResourceDepositGenerator` erzeugt aus dem stabilen Ressourcen-Seed eines Kometen reproduzierbare Quellen.

- Kometen unter `190` Welteinheiten Radius sowie `Tiny` und `Small` erhalten keine Quelle.
- Geeignete Kometen erhalten genau ein bis drei Quellen.
- Eine Quelle besitzt standardmäßig einen festen Weltradius von `42`, unabhängig vom Kometenradius.
- Quellen sind unendlich; Fortschritt wird durch Rate, Reinheit und Maschinenleistung begrenzt.
- Zwischen Quellen bleiben mindestens `6` Welteinheiten Abstand.
- Die zentrale Lande-/Baufläche landbarer Kometen bleibt frei.
- Nach 256 Zufallsversuchen garantiert ein deterministisches Ringraster die Platzierung oder meldet einen klaren Konfigurationsfehler.
- Jeder Komet besitzt reproduzierbar eine Geologie: kohlenstoffreich, silikatisch, metallisch, volatilreich oder radiogen.
- Die Geologie verändert die Rohstoffgewichte deutlich: etwa Kohlenstoff/Troilit auf kohlenstoffreichen, Silikat/Olivin/Calcit auf silikatischen und Eis/Salze auf volatilreichen Kometen.
- Sehr seltene Rohstoffe erscheinen ausschließlich auf metallischen oder radiogenen Kometen.
- Uranerz ist zusätzlich auf `Large`/`Huge` und die seltene radiogene Geologie begrenzt.

Die Auswahl bleibt gewichtet: häufige Stoffe dominieren, seltene Stoffe erhalten auf großen Kometen bessere Chancen. Der neue Generator garantiert nicht mehr auf jedem Quellkometen ausdrücklich einen sehr häufigen Grundrohstoff; er wählt alle ein bis drei Quellen aus dem gewichteten kompatiblen Pool.

### Reinheit und Raten

| Reinheit | Wahrscheinlichkeit | Förderrate | manueller Zeitfaktor | Energieeffizienz |
| --- | ---: | ---: | ---: | ---: |
| Unrein | 35 % | 0,65× | 1,30× | 0,75× |
| Normal | 50 % | 1,00× | 1,00× | 1,00× |
| Rein | 15 % | 1,50× | 0,80× | 1,25× |

Die Standardbasis beträgt `12 Einheiten/min`. Konkrete Ressourcen können sie überschreiben; Uran verwendet `7 Einheiten/min` und einen manuellen Ertrag von eins. Der Standardertrag eines manuellen Zyklus ist durch Reinheit `1 / 2 / 3` Einheiten.

Die Darstellung zeigt den Reinheitsnamen, verändert Grundfarbe und Glanzdetails und zeigt bei Minern Reinheit und effektive Rate im Maschinenpanel.

### Manueller Abbau und Werkzeuge

**Spielbar.** Der manuelle Basiszyklus wurde global auf `1,75×` der Ressourcen-Abbauzeit verlangsamt. Die aktive Hotbar-Ausrüstung bestimmt Härtegrenze und Geschwindigkeit:

| Werkzeug | Härtegrenze | Geschwindigkeitsfaktor | Besonderheit |
| --- | ---: | ---: | --- |
| Standard | 4,0 | 1× | frühe und weiche Rohstoffe |
| Verbessert | 8,5 | 2× | seltenere Metalle |
| Hochleistung | unbegrenzt | 4× | kann Uran mit Härte 9,5 abbauen |

Unendliche Quellen behalten ihren Restmengenmarker nach jedem Zyklus. Alte endliche `ResourceDepositDefinition`-Objekte werden von den Extraktionsregeln weiterhin korrekt einmalig geleert.

### Mobiler Miner

**Spielbar.** Der mobile Miner wird direkt auf einer unendlichen Quelle platziert und bindet ID, Rohstoff, Reinheit und Basisrate.

- kein Stromport und keine Anforderung an das Kabelnetz;
- Startenergie `210 kWs`;
- Eigenbedarf `0,35 kW`;
- ein mobiler Akkupack lädt `420 kWs` nach;
- sechs Ausgabeslots;
- Geschwindigkeitsfaktor `0,45`, Effizienz `0,75`;
- stoppt ohne interne Energie oder bei vollem Ausgang;
- kann manuell geleert oder über seinen Feststoffausgang abgeführt werden.

### Automatischer Miner

**Spielbar.** Auch der automatische Miner muss auf die passende Quelle gesetzt werden.

- acht Ausgabeslots;
- Geschwindigkeitsfaktor `1,6`, Effizienz `1,15`;
- fordert rezept- und reinheitsabhängigen Netzstrom an;
- produziert nur mit passender Quelle, ausreichender Energie, freiem Ausgang und vorhandenem Ausgangsanschluss;
- meldet `MissingResourceSource`, `WaitingForEnergy`, `OutputFull` oder `WaitingForLogistics` über den gemeinsamen Maschinenstatus.

Der Rezeptkatalog deckt alle 26 Weltressourcen ab und besitzt für jede genau eine mobile und eine automatische Variante. Damit sind 52 quellgebundene Extraktionsrezepte vorhanden. Die Rohstoff-ID-Mengen der mobilen und automatischen Varianten werden gegen den vollständigen Rohstoffkatalog getestet, sodass eine neu ergänzte Weltressource nicht unbemerkt ohne Minerrezept bleiben kann. Beim Platzieren eines Miners wird das zur Quelle gehörende Extraktionsrezept über dieselbe zentrale Freischaltprüfung wie im Maschinenmenü geprüft; ein Miner kann deshalb keine Ressource beziehungsweise Technologiestufe vorzeitig erschließen.

## Produktionsstufen und zentrale Ketten

Die Technologiestufe ist eine verbindliche Freischaltgrenze. Normale Rezepte dürfen höchstens eine Stufe über der höchsten abgeschlossenen Forschungsstufe liegen; ohne Forschung sind damit ausschließlich Stufe-1-Rezepte zugänglich. Explizite Rezeptanforderungen, in Forschungsdefinitionen gelistete Freischaltungen und Ressourcenentdeckungen können den Zugang zusätzlich einschränken. Alternative und Endgame-Rezepte benötigen weiterhin eine explizit freigeschaltete Alternativgruppe oder abgeschlossene Forschung derselben beziehungsweise einer höheren Stufe.

| Stufe | Produktionsschwerpunkt | Repräsentative Produkte und Abhängigkeiten |
| ---: | --- | --- |
| 1 | manuelle Gewinnung und Grundfertigung | Erz → zerkleinertes Erz → Barren; Eisenplatte, Eisenstange, Schrauben; Kupferdraht und -kabel; Rohre; erster Basisgenerator |
| 2 | Stahl und erste Automatisierung | Eisen + Kohlenstoff → Stahl → Platten/Träger; verstärkte Platte; Rotor, Pumpe, Ventil, kleiner Motor; mobiler Miner; Förder- und Rohrteile |
| 3 | netzgebundene Automatisierung | automatischer Miner, Automatisierungsmodul, Leiterplatte, frühe Elektronikteile, verbesserte Werkzeuge |
| 4 | Elektronik und Chemie | Sensor, Steuerungsmodul, Computerchip, Stromregler; Schwefelsäure, Kunststoff, Schmiermittel, Kühlmittel; Flüssigkeits-/Gastanks |
| 5 | fortgeschrittene Industrie | Hochleistungsmotor, Turbine, Kompressor, Präzisionsbauteil, Titanrahmen, Kühlsystem, Druckbehälter, Produktionscomputer, Hochleistungsbatterie |
| 6 | integrierte Fabrik- und Raumschiffkomponenten | Fabriksteuerung, Raumschiffmodul, Antriebsteil, Landebein, Scanner, Navigationscomputer, Strommodul, Treibstoffpumpe, Lebenserhaltung |
| 7 | Nuklearvorstufe und Weltraumlogistik | Uranaufbereitung, Nuklear-Forschungspakete, Hochleistungswerkzeug, Transportdrohne |
| 8 | Nuklearenergie und Abfall | Brennstoffzellen, Reaktor, Wiederaufarbeitung, Stabilisierung, Abschirmbehälter und Schutzanzüge |
| 9 | seltene Katalysatoren und Effizienzvarianten | Rhodium-, Ruthenium-, Osmium- und Iridiumvarianten für Elektronik, Brennstoffzellen und Navigation |
| 10 | Tiefenraumoptimierung | verdichtete Präzisions- und Strommodulrezepte; Tiefenraum-Steuerkern und Quanten-Navigationsmodul als konkrete Endprodukte |

### Beispielketten

```text
Eisenerz -> zerkleinertes Eisenerz -> Eisenbarren -> Platte/Stange -> Schrauben
Eisenbarren + verarbeiteter Kohlenstoff -> Stahlbarren -> Stahlplatte/Stahlträger
Stahl + Rotor + Kupfer -> Motor -> Hochleistungsmotor -> Raumschiffantriebsteil

Kupfererz -> Kupferbarren -> Draht -> Kabel
Silikat + Kohlenstoff + Kupfer -> Elektronikteile/Leiterplatte
Leiterplatte -> Sensor/Steuerungsmodul -> Computer -> Produktionscomputer -> Fabriksteuerung

Wassereis -> Wasser -> Wasserstoff + Sauerstoff
Schwefel + Wasser + Sauerstoff -> Schwefelsäure
Wasserstoff + Kohlenstoff -> Treibstoff/Kunststoff -> Schmiermittel und Chemieprodukte

Fabriksteuerung + Strommodule + Nuklearzelle + Präzisionsteile -> Tiefenraum-Steuerkern
Navigation + Iridium + Hochleistungsscanner + Nuklearzelle -> Quanten-Navigationsmodul
```

Spezialminerale besitzen eigene Verarbeitungswege: Troilit liefert Schwefel und Eisen, Schreibersit Phosphor, Olivin Silikat und Nickel, Kamacit/Taenit Meteoritenlegierungen, Halit Kühlmittel, Sylvin Batteriechemie; Palladium, Rhodium, Osmium und Ruthenium ermöglichen ertragreichere Hightech-Varianten.

### Progressionsgraph und Bootstrap-Sicherheit

Eine automatisierte Erreichbarkeitsprüfung simuliert den vollständigen Katalogpfad von allen gewinnbaren Rohstoffen und dem Starter-Abbauwerkzeug bis Stufe 10. Sie baut iterativ jede bezahlbare und freigeschaltete Maschine, führt alle dann erreichbaren Rezepte aus und schließt Forschung erst ab, wenn Voraussetzungen, Materialien und Entdeckungen erreichbar sind. Der Test verlangt anschließend, dass alle 28 Maschinen, alle 17 Forschungen sowie Tiefenraum-Steuerkern und Quanten-Navigationsmodul erreichbar sind.

Dabei wurden frühere Bootstrap-Zyklen in Baukosten, Rezepten und Forschungskosten aufgelöst. Das betrifft insbesondere Montagemaschine, mobilen und automatischen Miner, erweiterten Fabrikator, Präzisionshersteller, die frühe Leiterplatten- und Akkupackproduktion, Wasserstoff-Chemie sowie Abfall- und Weltraumforschung. Ein späteres Katalog-Edit, das erneut eine Maschine oder Forschung von ihrem eigenen Output abhängig macht, lässt den Progressionsgraph-Test fehlschlagen.

## Maschinenpark

Die 28 Maschinen teilen sich den gemeinsamen Bau-, Produktions-, Status-, Inventar- und Snapshotpfad.

- **Verarbeitung und Mining:** Zerkleinerer, Schmelzer, Gießerei, Raffinerie, Wasseraufbereiter, Elektrolyseur, mobiler Miner, automatischer Miner, Chemieanlage, Uran-Aufbereitungsanlage, Abfallverarbeitungsanlage.
- **Herstellung:** Konstruktor, Fabrikator, Montagemaschine, erweiterter Fabrikator, Präzisionshersteller, Brennstoffzellen-Fabrikator.
- **Energie und Infrastruktur:** Basisgenerator, Treibstoffgenerator, Atomkraftwerk, Strommast, Batteriebank.
- **Lager und Fluidlogistik:** allgemeiner Lagercontainer, Flüssigkeitstank, Gastank, Pumpstation, Atommülllager.
- **Forschung:** Forschungsstation.

Maschinen fordern nur dann Produktionsleistung an, wenn sie aktiviert, vollständig gebaut, korrekt konfiguriert und tatsächlich arbeitsfähig sind. Fehlendes Material, voller Ausgang, fehlende Quelle oder fehlende Logistik verbrauchen keinen Produktionsstrom.

## Logistik, Phasen, Tanks und Batterie

### Materialnetze

**Spielbar.** `MachinePortCatalog` leitet erlaubte Input-/Output-Items aus den tatsächlichen Rezepten und ihren Phasen ab.

- Förderbänder transportieren gerichtete Feststoffe mit bis zu `8 Einheiten/s`.
- Flüssigkeits- und Gasrohre bleiben getrennte Verbindungstypen und transportieren je `20 Einheiten/s`.
- Jeder Materialport unterstützt eine Verbindung.
- Transfers prüfen Quelle, Zielphase, Itemfilter und vollständige Zielkapazität; bei einem fehlgeschlagenen Add wird die Entnahme zurückgerollt.
- Tanks besitzen 40 Slots und akzeptieren nur ihre Phase.
- Die Pumpstation puffert Flüssigkeiten und Gase in acht Slots; sie ist derzeit ein phasengetrennter Transportpuffer, keine Druck- oder Strömungspumpe.
- Das Atommülllager besitzt 50 Slots und akzeptiert ausschließlich radioaktive Feststoffe.

Gefüllte Transportbehälter stellen jeweils `100` Einheiten eines Stoffes dar und geben den passenden leeren Behälter atomar zurück. Teilfüllstände, Mischen, Druck und Temperatur werden noch nicht simuliert.

### Strom und Netzspeicher

**Spielbar.** Stromkabel bilden echte getrennte Komponenten. Generatoren, Maschinen, Forschung, Schiffanschlüsse und Batterien wirken nur innerhalb ihrer Komponente. Topologie wird bei Verbindungsänderungen invalidiert, nicht in jedem Frame vollständig neu aufgebaut.

- Basisgenerator: `12 kW`, ohne Brennstoff.
- Treibstoffgenerator: `60 kW`, `120 s` pro Treibstoffbehälter.
- Atomkraftwerk: `1200 kW`, `300 s` pro Brennstoffzelle.
- Schutzschalter löst nach `1,5 s` Überlast aus und muss manuell zurückgesetzt werden.
- Die 60-Sekunden-Historie misst Kapazität, tatsächliche Produktion, Bedarf und Verbrauch.

Die Batteriebank ist ein echter Netzspeicher:

- Kapazität `18.000 kWs`;
- maximale Lade- und Entladeleistung jeweils `120 kW`;
- Ladeeffizienz `94 %`;
- lädt ausschließlich aus tatsächlich gelieferter Generator-/externer Überschussleistung;
- entlädt erst zur Deckung eines Defizits und nie gleichzeitig mit dem Laden;
- erzeugt keine Energie und überträgt nichts zwischen getrennten Komponenten;
- Ladeinput zählt als reale Netzlast und tatsächliche Generatorproduktion;
- gespeicherte Energie wird im Maschinensnapshot und Schema v6 erhalten.

## Forschung und Freischaltungen

### Technologiebaum

**Spielbar.** Der Baum enthält 17 Technologien; jede Stufe 1 bis 10 ist vertreten.

| Stufe | Technologie | Zentrale Voraussetzung/Ziel |
| ---: | --- | --- |
| 1 | Grundlegende Automatisierung | Einstieg, Montagemaschine und erste Forschungspakete |
| 2 | Mobile Rohstoffgewinnung | nach Grundautomatisierung; mobiler Miner |
| 2 | Erweiterte Metallverarbeitung | nach Grundautomatisierung; Gießerei und Legierungen |
| 3 | Verbesserte Abbautechnik | Mobile Gewinnung + Metallurgie; automatischer Miner und Werkzeug II |
| 3 | Industrielle Automatisierung | Mining + Metallurgie; erweiterter Fabrikator |
| 4 | Fortgeschrittene Elektronik | nach industrieller Automatisierung |
| 5 | Chemieingenieurwesen | Elektronik + Metallurgie; Chemieanlage und Tanks |
| 5 | Wasserstofftechnologie | nach Chemie; Elektrolyse |
| 6 | Treibstoffproduktion | Wasserstoff + Metallurgie; Raffinerie |
| 6 | Verbesserte Energieversorgung | Treibstoff + Elektronik; Generator und Batteriebank |
| 6 | Präzisionsfertigung | Automatisierung + Elektronik; Präzisionshersteller |
| 7 | Nukleare Aufbereitung | Chemie + Präzision + Uranentdeckung |
| 7 | Raumschiffbauteile | Präzision + Elektronik |
| 8 | Nukleare Energieerzeugung | Nuklearaufbereitung + Energie + Uranentdeckung |
| 8 | Radioaktives Abfallmanagement | nach Nuklearenergie |
| 9 | Integrierte Weltraumsysteme | Raumschiff, Nuklearenergie und Abfallmanagement |
| 10 | Tiefenraum-Optimierung | nach integrierten Weltraumsystemen |

Forschung verbraucht nicht nur rohe Erze, sondern ab Stufe 2 zunehmend Automatisierungs-, Elektronik-, Chemie-, Nuklear- und Weltraum-Forschungspakete. Eine laufende Forschung bleibt an genau eine Forschungsstation gebunden, fordert deren vollständige Leistung und pausiert andernfalls mit `WaitingForEnergy`.

Die Rezeptansicht und Minerplatzierung verwenden gemeinsam die zentrale Laufzeitprüfung. Normale Rezepte werden bis maximal eine Stufe über der höchsten abgeschlossenen Forschung sichtbar und nutzbar; explizite Forschungs- oder Entdeckungsanforderungen bleiben trotzdem bindend. Dadurch bleibt genug Vorlauf zum Herstellen des nächsten Forschungspakets, ohne dass mehrere spätere Stufen vorzeitig übersprungen werden können.

### Entdeckungen und alternative Rezepte

Uranforschung wird durch die gespeicherte Entdeckung von Uranerz blockiert. Beim Kartenscan eines Sektors übergibt `GameRoot.ScanSectorForMap` alle dort generierten Quellen-IDs an `FactoryRuntimeController.SynchronizeResourceDiscoveries`; neue Funde markieren den Factory-Save als geändert und aktualisieren die Freischaltansicht. Spezialressourcen-Rezepte mit dem Tag `special-resource` bleiben verborgen, bis ihre Rohstoffinputs auf diesem Weg entdeckt sind. Entdeckungen sind Teil des `ResearchStateSnapshot`.

Alternative Rezepte tragen stabile Gruppen, Tags und Stufen. Die Laufzeit schaltet Alternativen über eine explizit freigegebene Gruppe oder als Stufen-Fallback über eine abgeschlossene Forschung gleicher/höherer Stufe frei. Die Gruppennamen sind zwischen Forschungs- und Rezeptkatalog harmonisiert; der Stufenpfad bleibt als bewusst breitere Fortschrittsfreigabe erhalten.

Die Forschungsdefinitionen verwenden inzwischen dieselben stabilen Gruppennamen wie der Rezeptkatalog. Eine automatisierte Referenzprüfung stellt sicher, dass jede `UnlockedRecipe`-ID und jede `UnlockedAlternativeRecipeGroup` mindestens ein wirklich vorhandenes Rezept bezeichnet. `make_high_performance_battery`, `make_deep_space_control_core` und `make_quantum_navigation_module` sind konkrete Rezepte; die beiden Tiefenraumprodukte werden direkt durch `DeepSpaceOptimization` freigeschaltet.

## Nuklear-, Abfall- und Strahlungslogik

### Nuklearkette

```text
Uranerz
  -> zerkleinertes Uranerz
  -> Urankonzentrat + chemischer Abfall
  -> angereichertes Uran + radioaktiver Abfall
  -> Uran-Brennstoffzelle
  -> Atomkraftwerk (bis 1200 kW)
  -> verbrauchte Brennstoffzelle
  -> Wiederaufarbeitung: Urankonzentrat + radioaktiver Abfall
  -> Stabilisierung mit Calcit und Silikat
  -> abgeschirmter Abfallbehälter / Atommülllager
```

**Spielbar.** Der Reaktor verwendet denselben bedarfsgeregelten Generatorpfad wie andere Quellen. Er verbraucht nur für tatsächlich gelieferte Energie Brennstoff. Eine Brennstoffzelle entspricht bei Vollleistung 300 Generatorsekunden. Die verbrauchte Zelle wird atomar in den Output gelegt. Sobald dafür kein Platz vorhanden ist, liefert der Reaktor sofort `0 kW` und meldet `OutputFull` – auch mitten in der noch verbleibenden Laufzeit einer bereits begonnenen Brennstoffzelle. Die Restlaufzeit bleibt während der Blockade unverändert und läuft erst nach Freigabe des Ausgangs weiter. Damit ist Abfalllogistik für dauerhaften Betrieb relevant und blockierter Abfall kann nicht durch bereits vorgeladenen Brennstoff umgangen werden.

Aufbereitung und Anreicherung erzeugen chemischen beziehungsweise radioaktiven Abfall. Wiederaufarbeitung gewinnt einen Teil als Konzentrat zurück, vermehrt aber den zu stabilisierenden Rest. Das abgeschirmte Lager akzeptiert nur radioaktive Feststoffe und besitzt vollständige Maschinenabschirmung.

### Strahlung

**Spielbar als Expositions- und Warnsystem.** Radioaktive Items tragen eine Stärke; geladene Maschineninventare, Uranquellen und radioaktive Fracht im persönlichen Astronauten-Inventar oder in der Hotbar erzeugen distanzabhängige Quellen. Persönlich getragene Fracht wird als lokale Quelle an der Astronautenposition zusammengefasst. Das Raumschifflager bleibt durch den Schiffsschutz außen vor und exponiert den Spieler nicht. Die Dosis folgt einem quadratischen Entfernungsabfall mit Mindestdistanz `24` und Referenzdistanz `120`.

- Warnschwelle: `25` Dosis;
- kritisch: `70`;
- Maximum: `100`;
- passive Erholung ohne Quelle: `0,015/s`;
- Standardanzug: `15 %` Schutz;
- verbesserter Anzug: `60 %`;
- Nuklearanzug: `90 %`;
- Maschinenabschirmung reduziert die ausgestrahlte Stärke; Reaktor `95 %`, Urananlage `78 %`, Brennstoffzellen-Fabrikator `90 %`, Atommülllager `100 %`.

Die aus der Dosis abgeleitete Anzugintegrität wirkt jetzt direkt auf den Astronauten. Sie interpoliert kontinuierlich zwischen voller Leistung bei Integrität `1` und folgenden sicheren Untergrenzen bei Integrität `0`:

- Bewegung mindestens `0,55×` der normalen Geschwindigkeit;
- manueller Abbau mindestens `0,65×` der normalen Effizienz, zusätzlich zur Werkzeuggeschwindigkeit.

HUD, Warnstufe, aktuelle Rate, Anzugintegrität und persistierte Dosis sind angebunden. Der Spieler wird im Raumschiff nicht exponiert.

**Bewusste Grenzen:** Die funktionalen Bewegungs- und Abbaueinbußen ersetzen noch keine Lebens-/Tod-, Anzugenergie- oder Reparaturmechanik; die Integrität erholt sich gemeinsam mit der Dosis automatisch. Eindämmungsanforderungen sind Metadaten und beim spezialisierten Atommülllager erzwungen, jedoch noch nicht bei jedem allgemeinen Inventar, Förderband und Transportbehälter. Es gibt noch keine Reaktorwärme, Kühlmittelpflicht, Störfälle oder Kontaminationswolken.

## UI- und Laufzeitstatus

**Spielbar:**

- gemeinsame Maschinenpanels zeigen Rezept, Inputs, Outputs, Fortschritt, Leistung und Status;
- Miner ergänzen Reinheit, Rate und beim mobilen Miner Akkustand;
- Batterie zeigt gespeicherte Energie gegen Kapazität;
- Strahlungs-HUD zeigt Dosis, Rate, Warnstufe und Anzugintegrität; diese skaliert Bewegung und manuellen Abbau;
- Bauplatzierung bindet Miner ausschließlich an passende unendliche Quellen;
- Produktionsanimationen und Statusleuchten bleiben vom Core-Zustand abgeleitete Präsentation;
- blockierte Outputs, Materialmangel, fehlender Strom, fehlende Quelle und fehlende Logistik sind unterscheidbare Zustände.

**Noch nicht abschließend:** ein eigener grafischer Technologiebaum, individuelle Spezialdialoge für jede neue Maschine, ausführliche Rezeptsuche/Produktionsstatistik und finales Langzeitbalancing. Der gemeinsame Maschinenpanel-Pfad macht die 28 Definitionen funktional zugänglich, ersetzt aber noch keine spezialisierte Endgame-UX.

## Speicherung, Migration und reduzierte Simulation

### Factory-Schema v6

`FactoryStateData.CurrentVersion` ist `6`. Der Codec liest Version 1 bis 6 und schreibt ausschließlich v6. Version 6 ergänzt insbesondere:

- `ExtractionSourceBinding` pro Miner mit Quellen-ID, Rohstoff, Reinheit und Basisrate;
- interne Energie des mobilen Miners;
- gespeicherte Energie der Batteriebank;
- entdeckte Ressourcen im Forschungssnapshot;
- akkumulierte Strahlendosis und aktuellen Anzugschutz.

Weiterhin gespeichert werden Maschinenzustände und exakte Slots, Produktionsbatch und Fortschritt, Generatorbrennstoff, Verbindungen und Ports, getrennte Stromnetzschalter/Schutzschalter, Forschung und Stationsbindung, Inventar/Hotbar/Schiffslager, Schiffstank, Schiffanschlüsse, Dockingpose und letzter UTC-Simulationszeitpunkt je bebautem Kometen.

Migrationen aus v1 bis v5 ergänzen neue Felder sicher:

- fehlende Entdeckungen werden leer initialisiert;
- alte Miner besitzen zunächst keine Quellenbindung und müssen gültig neu gebunden/platziert werden;
- interne und gespeicherte Energie beginnen bei `0`, wenn das alte Schema sie nicht kannte;
- Strahlendosis startet sicher bei `0` mit Standardschutz;
- bestehende IDs, Inventarslots, Maschinen, Verbindungen, Forschung und Produktionsfortschritte bleiben erhalten.

Snapshots validieren endliche Werte, Slotgrenzen, eindeutige IDs, Quellenbindungen, Energiegrenzen und bekannte Zustandsformen vor der Wiederherstellung. Das Schreiben bleibt atomar über eine temporäre Datei.

### Offline-Simulation

Aktive Fabriken werden im Intervall von `0,1 s` simuliert. Beim Nachladen verwendet die reduzierte Simulation Schritte bis `10 s` und maximal `3600 s` pro Nachholvorgang. Sie nutzt dieselben Maschinen-, Materialtransport-, Strom- und Forschungszustände, ohne Weltansichten oder Partikeleffekte zu erzeugen. So bleiben Produktion, Minerakkus, Netzspeicher, Generatorbrennstoff, Outputs und Forschung konsistent, ohne jeden vergangenen Frame nachzustellen.

Die 60-Sekunden-Stromgrafik ist absichtlich transient und wird nach dem Laden neu aufgebaut. Strahlendosis wird gespeichert, aber nicht offline weiterberechnet, weil der Spieler keinem entladenen Kometenraum zugeordnet wird.

### Separater Ressourcenstatus

Die prozeduralen Quellen werden aus Seeds rekonstruiert. Unendliche v2-Quellen benötigen keinen Depletionszustand. Die Ressourcenregeln können alte endliche Definitionen weiterhin verarbeiten; vorhandene `resource_deposits.json`-Einträge verwenden jedoch alte `:deposit:`-IDs, während neue Quellen `:source:v2:` verwenden. Es existiert keine explizite ID-Migration. Bereits abgebaute alte Vorkommen können deshalb nach dem Generatorwechsel als neue unendliche Quelle erscheinen. Der Factory-Save selbst bleibt davon unberührt.

## Erweiterungspunkte

Die Architektur unterstützt folgende spätere Ausbauten ohne neues Parallelsystem:

- neue Items über `ProductionItemDefinition`, inklusive Phase, Gefahr und eigenem Stapellimit;
- neue Rezepte über Katalogdaten, Tags, Stufe, Alternativgruppe und optionale Quellenbindung;
- weitere Miner-MK-Stufen über Archetyp, Geschwindigkeits-/Effizienzmultiplikator und `ExtractionSourceBinding`;
- weitere geologische Kometenprofile durch zusätzliche Kompatibilitäts- und Gewichtsdaten;
- echte Fluidmengen, Druck und Temperatur hinter den bestehenden Liquid-/Gas-Ports;
- strengere Gefahrgutregeln über `ContainmentRequirement` in Inventar- und Transferakzeptanz;
- Reaktorwärme, Kühlkreise und Störfälle auf Basis des bestehenden Energie-, Fluid- und Gefahrenmodells;
- mehrere parallele Forschungsprojekte durch Erweiterung des derzeit globalen `ResearchState`;
- externe JSON-/Resource-Dateien für Items, Maschinen, Rezepte und Forschung; aktuell sind nur Rohstoffdefinitionen extern, die übrigen Standardkataloge sind datenorientierte C#-Definitionen und erfordern eine Neukompilierung;
- echte Langzeitstatistik zusätzlich zum momentanen 60-Sekunden-Netzringpuffer.

## Bekannte bewusste Grenzen

1. Es gibt den mobilen und den automatischen Miner, aber noch keine weiteren MK2-/MK3-Maschinenstufen.
2. Flüssigkeiten und Gase sind diskrete Itemstapel mit phasengetrennten Leitungen, keine kontinuierlichen Volumen-, Druck- oder Temperaturnetze.
3. Die Pumpstation ist ein bidirektionaler Puffer; aktive Pumpenleistung und Flussrichtung gegen Druck werden noch nicht simuliert.
4. Forschungskategorien und Stufen sind vorhanden, aber es gibt noch keinen vollwertigen visuellen Baum.
5. Die Freischaltlogik begrenzt normale Rezepte auf höchstens eine Stufe über der höchsten abgeschlossenen Forschung; alternatives und Endgame-Balancing bleibt trotz der automatisierten Erreichbarkeitsprüfung ein manueller Langzeittestpunkt.
6. Strahlung reduziert Bewegung und Abbau, besitzt aber noch keine Lebens-/Tod-, Anzugenergie- oder Reparaturmechanik.
7. Eindämmungsmetadaten werden noch nicht in jedem Transport- und Inventarpfad erzwungen.
8. Atomkraftwerke besitzen noch keine Wärme-, Kühlmittel-, Alterungs- oder Störfallsimulation.
9. Offline-Nachholung ist absichtlich auf eine Stunde begrenzt; längere Abwesenheit wird nicht vollständig nachsimuliert.
10. Produktionskataloge sind strukturell datenbasiert, aber außer den Weltressourcen noch als C#-Kataloge definiert.

## Testmatrix

| Bereich | Automatisierte Abdeckung | Aktueller Nachweis | Noch manuell zu prüfen |
| --- | --- | --- | --- |
| Katalogintegrität und Progression | eindeutige 135 Items, 198 bekannte Rezepte, 28 Maschinen, bekannte Inputs/Outputs, Forschungs-Rezept-IDs und Alternativgruppen, Phasen/Gefahren; iterativer Erreichbarkeitsgraph von Rohstoffen/Starterwerkzeug über alle Maschinen und Forschungen bis zu beiden Stufe-10-Endprodukten; Schutz vor Bootstrap-Zyklen | `ProductionCatalogTests` | Balancing, verständliche Anzeigenamen aller 198 Rezepte und tatsächliche Spieldauer des vollständigen Durchlaufs |
| Quellgenerierung | deterministisch, 0 auf kleinen Kometen, 1–3 Quellen, Radius 42, Abstand, Landefläche, fünf Geologien mit Gewichten, sehr seltene Stoffe nur metallisch/radiogen, Reinheitsverteilung und Uran-Geologie-/Größenlimit | `WorldGenerationTests`, `ResourceGenerationTests` | optische Lesbarkeit über viele Seeds |
| manueller Abbau | Reinheitsfaktoren, langsamere Basiszeit, unendliche Wiederholung, Legacy-Vorkommen | `ResourceExtractionTests` | vollständiger Maus-/Laser-/Inventarablauf je Werkzeugstufe |
| Werkzeuge | Item-zu-Stufe, Härte und Geschwindigkeit | `MiningToolRulesTests` | Hotbarwechsel während eines Abbauvorgangs |
| Miner | vollständige 26/26-Rohstoffabdeckung, Quellenbindung, Strompflicht, interner Akku, Ausgangsanschluss, Snapshot und gemeinsame Rezept-/Stufenfreischaltung bei Platzierung | `ProductionCatalogTests`, `ExtractionMachineStateTests` | Platzieren/Leeren/Förderband über Chunk-Wechsel im laufenden Spiel |
| Produktion | atomare Inputs/Outputs, Fortschritt, volle Outputs, Behälterrückgabe, Energiezustände | `MachineProductionTests`, `ProductionCatalogTests` | mehrstündige gemischte Fabrik und Rezeptwechsel unter Last |
| Logistik | Portkompatibilität, getrennte Medien, Durchsatzguthaben, Rollback, getrennte Netze | `MachineConnectionNetworkTests` | lange Band-/Rohrketten und UI-Feedback bei Blockaden |
| Speziallager | Flüssigkeit/Gas-Phasenfilter und radioaktive Feststoffe | `SpecializedStorageTests` | Drag-and-drop und Kabel/Rohrentfernung im Spiel |
| Batteriebank | Überschussladen, 94-%-Wirkungsgrad, Defizitentladung, Isolation, Energieerhaltung | `BatteryBankPowerTests` | Panelwerte und längere Lastwechselkurven |
| Strom/Forschung | bedarfsgerechte Erzeugung, Überlastung, Generatorbrennstoff, Forschungsleistung | `PowerAndResearchTests` | vollständige Forschung mit mehreren realen Netzkomponenten |
| Forschungsbaum | exakt 17 Technologien, DAG, alle zehn Stufen, Entdeckungsgate, Snapshot, Legacy, referenziell vorhandene Rezepte/Gruppen sowie Erreichbarkeit aller Forschungskosten ohne Bootstrap-Zyklus | `ResearchProgressionTests`, `ProductionCatalogTests` | visuelle Führung, Freischaltmeldungen und tatsächliches Timing im gesamten Baum |
| Nuklear/Abfall | Kettenreferenzen, Reaktorbrennstoff/-output, Abschirmung, Abfallprodukte sowie sofortiger Stopp bei während einer aktiven Brennstofflaufzeit vollem Ausgang ohne Verlust der Restlaufzeit | `PowerAndResearchTests`, `ProductionCatalogTests` | komplette End-to-End-Kette mit Blockieren, Leeren und Wiederanlaufen der realen Abfalllogistik |
| Strahlung | Dosis, Entfernung, Abschirmung, Erholung, Snapshot sowie Untergrenzen `0,55×` Bewegung und `0,65×` Abbau; Runtime-Quelle für persönliche Inventar-/Hotbar-Fracht, aber nicht für geschütztes Schiffslager | `RadiationExposureTests` plus Runtime-Anbindung in `FactoryRuntimeController` | HUD, Anzugintegrität, Warnungen, Anzugwechsel und radioaktive Frachttransfers nahe realen Maschinen |
| Persistenz v6 | Roundtrip, v1–v5-Migration, Quellenbindung, Miner-/Batterieenergie, Entdeckungen, Dosis | `FactoryStateJsonCodec.RunSchemaV6SmokeTest` im Debug-Start | echter Save/Reload mit gebauter Nuklearfabrik und Chunk-Wechsel |
| Regression | gesamte Core-Testsuite und isolierter Godot-Lauf bis 1.200 Frames | `249/249` Core-Tests sowie alle Factory-, UI-, Kabel-, Energie-, Karten-, Docking- und Settings-Smokes am 17. Juli 2026 bestanden | interaktiver mehrstündiger Langzeittest |

Automatisierte Tests belegen nun zusätzlich die katalogseitige Erreichbarkeit des vollständigen Progressionsgraphen, aber nicht die endgültige Progressionskurve oder deren Spieldauer. Vor einem Balancing-Release sind insbesondere mehrere Stunden Produktion, ein kompletter interaktiver Stufe-1-bis-10-Durchlauf, tatsächliche Save-Migrationen aus Spielständen jeder Version sowie ein End-to-End-Nuklearbetrieb mit absichtlich blockierter Abfalllogistik erforderlich.
