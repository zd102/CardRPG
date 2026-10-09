param([switch]$Editor)
. "$PSScriptRoot\common.ps1"
Push-Location -LiteralPath $ProjectRoot
try {
    dotnet build 'game/CardRPG.csproj' --nologo --disable-build-servers
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    Invoke-Godot -Arguments @('--headless', '--path', ('"' + $GameRoot + '"'), '--import')
    $arguments = @('--path', ('"' + $GameRoot + '"'))
    if ($Editor) { $arguments += '--editor' }
    # This is the interactive app explicitly launched by the user via Play-Demo.cmd.
    Start-Process -FilePath (Find-Godot) -ArgumentList $arguments -WorkingDirectory $GameRoot | Out-Null
} finally { Pop-Location }
