# Releasing kparser2

Steps to publish a versioned GitHub release with Windows binaries.

## Versioning

Use [Semantic Versioning](https://semver.org/):

- **MAJOR** — breaking report schema, wire contract, or CLI breaking changes
- **MINOR** — new decoders, analytics tabs, fixtures
- **PATCH** — bug fixes, lookup data updates

Update `CHANGELOG.md` under a new `## [x.y.z] - YYYY-MM-DD` section before tagging. Git semver is set in [`Directory.Build.props`](../Directory.Build.props). Keep tags aligned with [kpacket2](https://github.com/poroburu/kpacket2) — see [COMPATIBILITY.md](COMPATIBILITY.md).

For RC releases use tags like `v0.1.0-rc.1` and pass `--prerelease` to `gh release create`.

## Pre-release checklist

```powershell
cd C:\path\to\kparser2
dotnet build kparser2.sln -c Release
dotnet test kparser2.sln -c Release --no-build

# Decoder oracle
dotnet run -c Release --project kparser2.Cli/kparser2.Cli.fsproj -- decode fixtures/sessions/sample.ndjson
dotnet run -c Release --project kparser2.Cli/kparser2.Cli.fsproj -- analytics snapshot fixtures/sessions/combat_basic.ndjson --assert-combat
dotnet run -c Release --project kparser2.Cli/kparser2.Cli.fsproj -- analytics snapshot fixtures/sessions/bcmn30_petrifying_pair.ndjson --assert-combat --min-battles 1
```

Optional live smoke (game + kpacket2):

```powershell
dotnet run -c Release --project kparser2.Cli/kparser2.Cli.fsproj -- probe
dotnet run -c Release --project kparser2.Cli/kparser2.Cli.fsproj -- record smoke.ndjson --duration-ms 10000
dotnet run -c Release --project kparser2.Cli/kparser2.Cli.fsproj -- analytics snapshot smoke.ndjson
```

## Build publish artifacts

```powershell
$ver = "0.1.0-rc.1"
$out = "dist/v$ver"
New-Item -ItemType Directory -Force -Path $out | Out-Null

# Self-contained CLI (portable, no SDK required on target machine)
dotnet publish kparser2.Cli/kparser2.Cli.fsproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o "$out/kparser2-cli-win-x64"

# Framework-dependent CLI (smaller; requires .NET 8 runtime)
dotnet publish kparser2.Cli/kparser2.Cli.fsproj -c Release -r win-x64 --self-contained false `
  -o "$out/kparser2-cli-win-x64-fdd"

# WPF app (framework-dependent; Windows only)
dotnet publish kparser2/kparser2.csproj -c Release -r win-x64 --self-contained false `
  -o "$out/kparser2-win-x64"
```

Copy `data/` and `fixtures/sessions/` are included via project `None` items in the WPF publish output. For CLI-only zips, copy `data/` manually if lookups are needed at runtime.

Create a zip per artifact:

```powershell
Compress-Archive -Path "$out/kparser2-win-x64/*" -DestinationPath "$out/kparser2-v$ver-win-x64.zip" -Force
Compress-Archive -Path "$out/kparser2-cli-win-x64/*" -DestinationPath "$out/kparser2-cli-v$ver-win-x64.zip" -Force
```

## Tag and GitHub release

```powershell
git add CHANGELOG.md Directory.Build.props LICENSE README.md CONTRIBUTING.md docs/
git commit -m "Release v$ver"
git tag -a "v$ver" -m "kparser2 v$ver"
git push origin main
git push origin poro-old-mvp
git push origin "v$ver"
```

If `main` has not yet absorbed the MVP line, push and tag from `poro-old-mvp` until `main` is fast-forwarded.

Create the release (requires [GitHub CLI](https://cli.github.com/)). Tag **kpacket2** before kparser2 when releasing a paired RC:

```powershell
gh release create "v$ver" `
  "$out/kparser2-v$ver-win-x64.zip" `
  "$out/kparser2-cli-v$ver-win-x64.zip" `
  --title "kparser2 v$ver" `
  --prerelease `
  --notes "Requires [kpacket2 v$ver](https://github.com/poroburu/kpacket2/releases/tag/v$ver) for live capture. Wire: kpacket.v1."
```

Edit the generated release notes on GitHub to keep only the section for `v$ver` if `CHANGELOG.md` contains older entries.

## Release notes template

Use this structure in the GitHub release body:

```markdown
## kparser2 vX.Y.Z — packet-native FFXI parser

First-time users: see [README](https://github.com/poroburu/kparser2#background-kparser-and-why-kparser2-exists) for how this differs from classic KParser.

### Highlights
- …

### Requirements
- Windows 10/11
- .NET 8 Runtime (for framework-dependent builds) or use the self-contained CLI zip
- [kpacket2](https://github.com/poroburu/kpacket2) for live capture

### Quick start
1. Extract `kparser2-vX.Y.Z-win-x64.zip`
2. Run `kparser2.exe`
3. **Session → Open NDJSON…** and pick `fixtures/sessions/sample.ndjson` to try offline
4. For live play: load kpacket2 in Ashita, then **Session → Use Live Feed**

### CLI
`kparser2-cli.exe decode fixtures/sessions/sample.ndjson`

Full changelog: [CHANGELOG.md](https://github.com/poroburu/kparser2/blob/main/CHANGELOG.md)
```

## Post-release

- Open a tracking issue for the next milestone (opcode backlog, horizonxilogs upload, etc.)
- Promote any validated live captures into `fixtures/sessions/` for the next cycle
- Announce on HorizonXI / community channels with kpacket2 install link
