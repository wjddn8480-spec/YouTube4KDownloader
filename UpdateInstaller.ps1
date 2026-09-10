$ErrorActionPreference = 'Stop'
$job = $env:YTD_UPDATE_JOB | ConvertFrom-Json
$target = [string]$job.Target
$stage = [string]$job.Stage
$source = Join-Path $stage 'YouTube4KDownloader.exe'
$backup = $target + '.update-backup'
$incoming = $target + '.update-incoming'
$replaced = $false
$next = $null
try {
    $parent = Get-Process -Id $job.ParentId -ErrorAction SilentlyContinue
    [IO.File]::WriteAllText((Join-Path $stage 'ready'), 'ready')
    if ($parent -and !$parent.WaitForExit(60000)) { throw 'Application did not exit; update cancelled.' }
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $job.Hash) { throw 'Staged EXE checksum mismatch.' }
    Copy-Item -LiteralPath $source -Destination $incoming -Force
    # Atomic replacement on the destination volume; the old executable is retained.
    [IO.File]::Replace($incoming, $target, $backup, $true)
    $replaced = $true
    $env:YTD_UPDATE_ACK = Join-Path $stage 'started'
    $next = Start-Process -FilePath $target -WorkingDirectory (Split-Path $target) -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while (!(Test-Path -LiteralPath $env:YTD_UPDATE_ACK)) {
        if ($next.HasExited) { throw 'Updated application exited before startup completed.' }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Updated application startup timed out.' }
        Start-Sleep -Milliseconds 200
    }
    Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
} catch {
    $message = $_.Exception.ToString()
    if ($replaced) {
        try {
            if ($next -and !$next.HasExited) { $next.Kill(); $next.WaitForExit() }
            [IO.File]::Copy($backup, $target, $true)
            Remove-Item Env:YTD_UPDATE_ACK -ErrorAction SilentlyContinue
            $env:YTD_UPDATE_FAILED = '1'
            Start-Process -FilePath $target -WorkingDirectory (Split-Path $target)
        } catch { $message += "`r`nRollback: " + $_.Exception.ToString() }
    } elseif (!(Get-Process -Id $job.ParentId -ErrorAction SilentlyContinue)) {
        Remove-Item Env:YTD_UPDATE_ACK -ErrorAction SilentlyContinue
        $env:YTD_UPDATE_FAILED = '1'
        Start-Process -FilePath $target -WorkingDirectory (Split-Path $target) -ErrorAction SilentlyContinue
    }
    [IO.File]::WriteAllText((Join-Path $stage 'error.log'), $message)
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show("Update failed. The previous EXE is preserved.`nLog: $stage\error.log", 'YouTube4KDownloader') | Out-Null
} finally {
    Remove-Item -LiteralPath $incoming -Force -ErrorAction SilentlyContinue
}
