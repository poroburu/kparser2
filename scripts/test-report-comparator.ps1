$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("kparser2-report-test-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $temp | Out-Null

try {
    $legacyPath = Join-Path $temp "kparser.json"
    $packetPath = Join-Path $temp "kparser2.json"

    $legacy = @'
{
  "entities": [
    {"name": "Alice", "type": "Player"},
    {"name": "Crab", "type": "Mob"}
  ],
  "battles": [
    {"enemyName": "Crab", "killed": true, "killerName": "Alice", "experiencePoints": 100, "experienceChain": 1}
  ],
  "parity": {
    "interactions": [
      {"actorName": "Alice", "targetName": "Crab", "interactionType": "Harm", "actionType": "Melee", "harmType": "Damage", "amount": 128, "success": "hit"},
      {"actorName": "Alice", "targetName": "Crab", "interactionType": "Harm", "actionType": "Melee", "harmType": "Damage", "amount": 0, "success": "miss"}
    ]
  }
}
'@

    $packet = @'
{
  "Combatants": [
    {"Name": "Alice", "Kind": "Player"},
    {"Name": "Crab", "Kind": "Mob"}
  ],
  "Battles": [
    {"EnemyName": "Crab", "Killed": true, "KillerName": "Alice", "ExperiencePoints": 100, "ExperienceChain": 1}
  ],
  "Interactions": [
    {"ActorName": "Alice", "TargetName": "Crab", "InteractionType": "Harm", "HarmType": "Melee", "Category": "Melee", "Value": 128, "Success": "hit"},
    {"ActorName": "Alice", "TargetName": "Crab", "InteractionType": "Harm", "HarmType": "Melee", "Category": "Melee", "Value": 0, "Success": "miss"}
  ]
}
'@

    Set-Content -LiteralPath $legacyPath -Value $legacy -Encoding utf8
    Set-Content -LiteralPath $packetPath -Value $packet -Encoding utf8

    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "compare-reports.ps1") `
        -KparserJson $legacyPath -Kparser2Json $packetPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Expected equal report comparison to pass"
    }

    $packetMismatch = $packet -replace '"Value": 128', '"Value": 127'
    Set-Content -LiteralPath $packetPath -Value $packetMismatch -Encoding utf8
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "compare-reports.ps1") `
        -KparserJson $legacyPath -Kparser2Json $packetPath | Out-Null
    if ($LASTEXITCODE -ne 1) {
        throw "Expected report mismatch to fail"
    }

    Write-Output "report comparator self-test passed"
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
