param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    throw "File not found: $Path"
}

if ([string]::IsNullOrWhiteSpace($Output)) {
    $dir = Split-Path -Parent $Path
    $name = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    $Output = Join-Path $dir "$name.complete.ndjson"
}

$kept = New-Object System.Collections.Generic.List[string]
$dropped = 0

$fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
try {
    $sr = New-Object System.IO.StreamReader($fs)
    try {
        while ($null -ne ($line = $sr.ReadLine())) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try {
                $null = $line | ConvertFrom-Json
                [void]$kept.Add($line)
            } catch {
                $dropped++
            }
        }
    } finally {
        $sr.Dispose()
    }
} finally {
    $fs.Dispose()
}

$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllLines($Output, $kept, $utf8)
Write-Host "reconcile $Path -> $Output  kept=$($kept.Count) dropped=$dropped"
Write-Host "Tail of a DC capture may include a magic start (0x28 cmd 8) without a finish (cmd 4); that is not a classifier miss."
