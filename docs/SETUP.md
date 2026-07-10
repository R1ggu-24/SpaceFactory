# Entwicklungsumgebung

## Erkannter Zustand am 10. Juli 2026

| Werkzeug | Status | Version | Nächster Schritt |
|---|---|---:|---|
| Git | vorhanden | 2.53.0.windows.2 | keiner |
| VS Code | vorhanden | 1.128.0 | optional C#-Erweiterung konfigurieren |
| .NET | projektlokal installiert | SDK 8.0.422 | `.tools/dotnet/dotnet.exe` verwenden |
| Godot .NET | projektlokal installiert | 4.7 stable mono | portable EXE unter `.tools/godot` verwenden |
| winget | defekter App-Alias | nicht ermittelbar | nicht verwendet |

## Durchgeführte Installationen

### .NET SDK

- Quelle: offizielles Microsoft-Skript `https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.ps1`
- Version: 8.0.422 x64
- Grund: C# kompilieren, wiederherstellen und testen
- Umfang: projektlokal in `.tools/dotnet`, keine PATH- oder Systemänderung
- Befehl: `dotnet-install.ps1 -Version 8.0.422 -InstallDir .tools/dotnet -Architecture x64 -NoPath`

### Godot Engine .NET

- Quelle: offizieller Download `downloads.godotengine.org`
- Version: 4.7 stable mono x64
- Grund: Godot-C#-Projekt bauen und die Demo ausführen
- Umfang: portabel und projektlokal in `.tools/godot`
- Befehl: offizielles `Godot_v4.7-stable_mono_win64.zip` herunterladen und mit `Expand-Archive` entpacken

### NuGet-Pakete

- Quelle: offizielles NuGet-Repository über `dotnet restore`
- Pakete: `Godot.NET.Sdk` 4.7.0, `Microsoft.NET.Test.Sdk` 17.13.0, `xunit` 2.9.3 und `xunit.runner.visualstudio` 3.0.2
- Grund: Godot-C#-Build und automatisierte Core-Tests
- Umfang: projektbezogener Paketcache; Referenzen stehen in den `.csproj`-Dateien

Beim ersten Start der projektlokalen .NET CLI meldete das SDK außerdem die Erstellung eines untrusted ASP.NET-Core-Entwicklungszertifikats. SpaceFactory verwendet dieses Zertifikat nicht; es wurde nicht als vertrauenswürdig eingestuft.

`.tools` ist absichtlich ignoriert. Auf einem neuen Rechner entweder beide Werkzeuge systemweit installieren oder dieselben offiziellen, projektlokalen Wege verwenden.

## Befehle mit lokalen Werkzeugen

```powershell
$dotnet = ".\.tools\dotnet\dotnet.exe"
$godot = Get-ChildItem .\.tools\godot -Filter "Godot*mono*.exe" -Recurse | Select-Object -First 1
& $dotnet restore
& $dotnet build
& $dotnet test
& $godot.FullName --path . --editor
```
