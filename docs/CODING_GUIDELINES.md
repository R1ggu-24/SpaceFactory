# Coding Guidelines

- Nullable und Warnungen als Fehler bleiben aktiv; vier Leerzeichen, UTF-8 und finale Leerzeile verwenden.
- Typen und öffentliche Member sind PascalCase, lokale Werte camelCase, private Felder `_camelCase`.
- Domänenlogik bleibt Godot-frei im Core und erhält schnelle, deterministische Tests.
- Godot-Szenen bleiben klein; Nodes koordinieren Darstellung und Eingabe, nicht Geschäftsregeln.
- Komposition, frühe Rückgaben und kleine Methoden bevorzugen. Abstraktionen erst mit klarer zweiter Implementierung oder Testnaht einführen.
- Szenennamen und C#-Klassennamen müssen zusammenpassen. Ressourcenpfade werden zentral und stabil gehalten.
- Tests müssen unabhängig sein und dürfen weder Reihenfolge noch globale Zufallszustände voraussetzen.

Commit-Beispiele: `feat: add player ship movement`, `fix: prevent inventory overflow`, `refactor: extract sector generation`, `test: add deterministic world tests`, `docs: update project setup`.
