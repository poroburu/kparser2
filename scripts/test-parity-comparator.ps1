$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("kparser2-parity-test-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $temp | Out-Null

try {
    $legacyPath = Join-Path $temp "kparser.json"
    $packetPath = Join-Path $temp "kparser2.json"

    $legacy = @'
{
  "parity": {
    "interactions": [
      {
        "actorName": "Alice",
        "targetName": "Crab",
        "interactionType": "Harm",
        "actionType": "Melee",
        "amount": 128,
        "success": "hit"
      }
    ],
    "chat": [
      {
        "speaker": "Alice",
        "mode": "Say",
        "message": "hello"
      }
    ]
  }
}
'@

    $packet = @'
{
  "interactions": [
    {
      "actorName": "Alice",
      "targetName": "Crab",
      "interactionType": "Harm",
      "actionType": "Melee",
      "harmType": "Melee",
      "aidType": "",
      "amount": 128,
      "success": "hit"
    }
  ],
  "chat": [
    {
      "speaker": "Alice",
      "mode": "Say",
      "message": "hello"
    }
  ]
}
'@

    Set-Content -LiteralPath $legacyPath -Value $legacy -Encoding utf8
    Set-Content -LiteralPath $packetPath -Value $packet -Encoding utf8

    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "compare-parity.ps1") `
        -KparserJson $legacyPath -Kparser2Json $packetPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Expected equal parity projections to pass"
    }

    $packetMismatch = $packet -replace '"amount": 128', '"amount": 127'
    Set-Content -LiteralPath $packetPath -Value $packetMismatch -Encoding utf8
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "compare-parity.ps1") `
        -KparserJson $legacyPath -Kparser2Json $packetPath | Out-Null
    if ($LASTEXITCODE -ne 1) {
        throw "Expected amount mismatch to fail"
    }

    Write-Output "parity comparator self-test passed"
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
