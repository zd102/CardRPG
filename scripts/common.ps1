$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$GameRoot = Join-Path $ProjectRoot 'game'

function Find-Godot {
    $candidates = @(
        $env:GODOT4,
        [Environment]::GetEnvironmentVariable('GODOT4', 'User'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Godot\4.7.2\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe')
    )
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) { return $candidate }
    }
    throw 'Godot .NET was not found. Set GODOT4 to the Godot 4.7.2 .NET executable.'
}

function Invoke-Godot {
    param([string[]]$Arguments)
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = Find-Godot
    $info.Arguments = $Arguments -join ' '
    $info.WorkingDirectory = $GameRoot
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($info)
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout = $stdoutTask.Result
    $stderr = $stderrTask.Result
    $exitCode = $process.ExitCode
    $process.Dispose()
    if ($stdout) { Write-Output $stdout }
    if ($stderr) { Write-Output $stderr }
    # Some export plugin errors are logged even when the engine returns zero.
    if ($exitCode -ne 0 -or $stderr -match '(?m)^ERROR:') { throw "Godot failed (exit code $exitCode). See the output above." }
}
