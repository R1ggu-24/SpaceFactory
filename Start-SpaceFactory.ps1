param(
    [switch]$Editor,
    [switch]$SkipBuild,
    [switch]$Headless,
    [int]$QuitAfterFrames = 0
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$localDotnet = Join-Path $projectRoot ".tools\dotnet\dotnet.exe"
$localGodot = Get-ChildItem (Join-Path $projectRoot ".tools\godot") `
    -Filter "Godot*_mono_win64.exe" -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1

if (Test-Path -LiteralPath $localDotnet) {
    $dotnet = $localDotnet
    $env:DOTNET_ROOT = Split-Path -Parent $localDotnet
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
} else {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnetCommand) {
        throw ".NET SDK 8 wurde weder projektlokal noch im PATH gefunden. Siehe docs/SETUP.md."
    }

    $dotnet = $dotnetCommand.Source
}

if ($null -ne $localGodot) {
    $godot = $localGodot.FullName
} else {
    $godotCommand = Get-Command godot -ErrorAction SilentlyContinue
    if ($null -eq $godotCommand) {
        throw "Godot 4.7 .NET wurde weder projektlokal noch im PATH gefunden. Siehe docs/SETUP.md."
    }

    $godot = $godotCommand.Source
}

Push-Location $projectRoot
try {
    if (-not $SkipBuild) {
        & $dotnet build SpaceFactory.sln --nologo -m:1
        if ($LASTEXITCODE -ne 0) {
            throw "Der Build ist fehlgeschlagen."
        }
    }

    $godotArguments = @("--path", $projectRoot)
    if ($Editor) {
        $godotArguments += "--editor"
    }
    if ($Headless) {
        $godotArguments += "--headless"
    }
    if ($QuitAfterFrames -gt 0) {
        $godotArguments += @("--quit-after", $QuitAfterFrames)
    }

    & $godot @godotArguments
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "Godot wurde mit Exitcode $LASTEXITCODE beendet."
    }
} finally {
    Pop-Location
}
