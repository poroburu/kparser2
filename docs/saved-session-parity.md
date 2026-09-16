# Saved-session regression

Run from the child checkout. Keep inputs and outputs private. The legacy CLI is
read-only; the runner never builds or edits kparser v1.

```powershell
node scripts/align-oracle-window.cjs PACKETS NORMALIZED_CHAT NEW_ALIGNMENT_DIR START_UTC END_UTC LOCAL_OFFSET_MINUTES
node scripts/run-session-parity.cjs ALIGNED_PACKETS ALIGNED_CHAT ALIGNMENT_JSON NEW_OUTPUT_DIR
```

The second command builds an isolated current CLI, invokes both parsers, compares
reports and normalized interactions, checks counted chat content and sequence,
then builds and runs the WPF replay suite. It returns 0 for automated success,
1 for comparison failure, and 2 for invalid inputs or failed execution. Existing
output directories are rejected. An optional fifth argument supplies an existing
UI manifest; its capture hash and artifacts must validate. This is explicit
evidence reuse, not proof that a different renderer build behaves identically.

`session-qa.json` links state, chat, UI, source hashes, executable hashes, tool
hashes, commit and working diff. `anchors.json` records distinct beginning,
middle and end events with packet IDs, capture line numbers and ChatLine
positions. Chat display can trail packet receipt through animation; correlation
uses -1500 through +10000 ms and is approximate. Repeated anchor signatures are
not eligible as anchors. Missing source provenance fails the run.

Alignment retains earlier entity initialization packets and every packet inside
the declared half-open interval. Excluded capture tails are counted separately.
A session may start mid-fight. Its retained outcomes are still compared; earlier
damage is not reconstructed. Only a single final open fight with the matching
enemy and zero XP can explain v1 omitting that fight from its completed report.
Internal missing events and other fight differences still fail.

`semantics.json` retains counted matches, missing and extra rows, partial legacy
attribution, chat inventory and exclusions. The observed local addon commands,
blocked checker packet, preparation/activation rows, and events checked in other
reports remain visible. Unknown shared chat does not receive a blanket exclusion.
Control-code cleanup is used only to locate original ChatLines; shared chat
content remains an exact comparison. Echo merging consumes one outgoing record
only for a matching local incoming echo within 500 ms. Repeated incoming rows,
other speakers, channels, reverse timing and late echoes are preserved.

`ui-independent.json` compares displayed offense/defense actor totals and XP
against v1 messages and battles. The WPF suite additionally exercises populated
reports, filters, refresh/reset, graphs, raw packets and compact layouts.
Automated success never implies screenshot inspection or human acceptance.
Record inspected screenshot filenames and hashes in a separate private visual
review artifact. Unavailable views and empty scenarios remain in the UI manifest.

## Validation and coverage

```powershell
dotnet test kparser2.sln --filter 'Category!=Integration'
node --test scripts/test-session-semantics.cjs scripts/test-ui-oracle.cjs scripts/test-align-oracle-window.cjs scripts/test-oracle-dataset.cjs
powershell -File scripts/test-report-comparator.ps1
powershell -File scripts/test-parity-evidence.ps1
powershell -File scripts/test-ui-evidence.ps1
```

The saved BST overlap reconciles 571 XP, completed fight outcomes, player/pet
offense, defense, loot, and supported interactions. It includes nine shared chat
rows (eight status expirations and one emote); fourteen observed addon-only rows
are explicit exclusions. Ordinary incoming channel traffic is absent. Tests for
echoes, repeated messages, ordering, speakers, channels, control codes and
auto-translate are synthetic coverage, not observed live-channel parity.

Parameterized text supports the verified expiry effects and stare template only.
Unknown action IDs retain explicit namespace-qualified names. Other templates,
party/pet ownership transitions absent from this session, live connection
behavior and human acceptance remain coverage gaps. Do not call this universal
v1/v2 parity.

Keep the two focused Antican and Beetle windows as independent replay cases.
Use [oracle-datasets.md](oracle-datasets.md) for sanitization and the broader
scenario collection matrix. Raw NDJSON, raw ChatLines, player names, screenshots
and private replay output must not be committed. Synthetic tests and sanitized
semantic projections can be committed; the projection exporter does not produce
sanitized replayable packet bytes.
