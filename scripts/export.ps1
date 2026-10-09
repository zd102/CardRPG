. "$PSScriptRoot\common.ps1"
$outputDirectory = Join-Path $ProjectRoot 'artifacts\MicroMaze'
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
Push-Location -LiteralPath $ProjectRoot
try {
    dotnet build 'game/CardRPG.csproj' --nologo --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    Invoke-Godot -Arguments @('--headless', '--path', ('"' + $GameRoot + '"'), '--import')
    Invoke-Godot -Arguments @('--headless', '--path', ('"' + $GameRoot + '"'), '--export-release', '"Windows Desktop"', ('"' + (Join-Path $outputDirectory 'CardRPG-MicroMaze.exe') + '"'))
    Write-Output "Demo exported to $outputDirectory"
} finally { Pop-Location }
