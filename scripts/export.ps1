param([string]$OutputDirectory = '')
. "$PSScriptRoot\common.ps1"
$exportTarget = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $ProjectRoot 'artifacts\Expedition' }
New-Item -ItemType Directory -Path $exportTarget -Force | Out-Null
Push-Location -LiteralPath $ProjectRoot
try {
    dotnet build 'game/CardRPG.csproj' --nologo --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    Invoke-Godot -Arguments @('--headless', '--path', ('"' + $GameRoot + '"'), '--import')
    Invoke-Godot -Arguments @('--headless', '--path', ('"' + $GameRoot + '"'), '--export-release', '"Windows Desktop"', ('"' + (Join-Path $exportTarget 'CardRPG-Expedition.exe') + '"'))
    Write-Output "Demo exported to $exportTarget"
} finally { Pop-Location }
