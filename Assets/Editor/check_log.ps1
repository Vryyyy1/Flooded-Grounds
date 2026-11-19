$logPath = [System.IO.Path]::Combine($env:LOCALAPPDATA, 'Unity', 'Editor', 'Editor.log')
if (Test-Path $logPath) {
    $fs = New-Object System.IO.FileStream($logPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    $sr = New-Object System.IO.StreamReader($fs)
    $text = $sr.ReadToEnd()
    $sr.Close()
    $fs.Close()
    $lines = $text -split "`r?`n"
    $lastLines = $lines[[Math]::Max(0, $lines.Length - 150)..($lines.Length - 1)]
    $errors = $lastLines | Where-Object { $_ -match "error CS\d+" }
    if ($errors) {
        Write-Host "=== COMPILE ERRORS ==="
        $errors | ForEach-Object { Write-Host $_ }
    } else {
        Write-Host "No CS compile errors found. Showing last 30 log lines:"
        $lastLines[[Math]::Max(0, $lastLines.Length - 30)..($lastLines.Length - 1)] | ForEach-Object { Write-Host $_ }
    }
}

