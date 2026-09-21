// Local corpus preparation. Raw captures and name mappings never enter exports.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const read = p => JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
const write = (p, x) => fs.writeFileSync(p, JSON.stringify(x, null, 2) + '\n', {flag:'wx'});

function normalizeChat(input, output) {
  if (path.resolve(input) === path.resolve(output)) throw Error('Never overwrite raw evidence');
  let changed = 0, lineNumber = 0, checkerTimestamp = null;
  const derivedTimestamps = [];
  const lines = fs.readFileSync(input, 'utf8').split(/(\r?\n)/).map(line => {
    if (/^\r?\n$/.test(line)) { lineNumber++; return line; }
    if (!line || line.startsWith('#')) { checkerTimestamp = null; return line; }
    // Observed Ashita prefix only, immediately after the 21-field RAM header.
    // Retain the timestamp for the oracle's existing timestamp parser.
    const m = /^((?:[0-9a-fA-F]{2},){3}[0-9a-fA-F]{8},(?:[0-9a-fA-F]{8},){2}[0-9a-fA-F]{4},(?:[0-9a-fA-F]{2},){4}(?:[0-9a-fA-F]{8},){9}[0-9a-fA-F]{2},)(.*)$/.exec(line);
    if (!m) throw Error('Unsupported ChatLine header');
    // A checker line can carry its own color before the timestamp. Do not strip
    // arbitrary controls from message content or infer times for unknown rows.
    let body = m[2].replace(/^(?:\x1ej)?(?:\x1e\x01)*\x1eQ(\[(?:[01]\d|2[0-3]):[0-5]\d:[0-5]\d\])\x1e\x01 /, '$1 ');
    if (checkerTimestamp && /^\x1e\x01\x81@\x1ejDefense\x1e\x01\x1eQ\)\x1e\x01$/.test(body)) {
      derivedTimestamps.push({line:lineNumber+1, source_line:checkerTimestamp.line,
        timestamp:checkerTimestamp.time, reason:'Immediately preceding checker message wrapped to this RAM row'});
      body = checkerTimestamp.time + ' ' + body;
    }
    const checker = /^(\[\d{2}:\d{2}:\d{2}\]) \x1eQ\[\x1e\x06checker\x1eQ\].*(?:High|Low) $/.exec(body);
    checkerTimestamp = checker ? {time:checker[1],line:lineNumber+1} : null;
    if (body !== m[2]) changed++;
    return m[1] + body;
  });
  fs.writeFileSync(output, lines.join(''), {flag:'wx'});
  write(output + '.provenance.json', {schema_version:1, adapter:'ashita-timestamp-v1',
    source_sha256:hash(input), output_sha256:hash(output), changed_lines:changed,
    derived_timestamps:derivedTimestamps,
    privacy:'private-raw-derived', oracle_modified:false});
  return {changed_lines:changed};
}

const allowedLabels = new Set(['', 'None','Unknown','Other','Harm','Aid','Death','Melee','Ranged',
  'Spell','Ability','Weaponskill','WeaponSkill','JobAbility','Skillchain','Damage','Drain',
  'Enfeeble','Enhance','Recovery','Heal','Healing','StatusRemoval','RemoveStatus','Item',
  'hit','miss','parry','shadow-absorb','no-effect','message','critical','resist','evade',
  'counter','guard','block','unknown','success','failed','absorb','spikes','retaliation']);
function exportPair(v1, v2, output, scenario) {
  if (!/^[a-z0-9][a-z0-9-]{0,63}$/.test(scenario)) throw Error('Use a non-identifying scenario slug');
  const docs = [read(v1), read(v2)];
  const rows = docs.map(d => d.parity?.interactions ?? d.interactions);
  if (rows.some(r => !Array.isArray(r) || r.length === 0)) throw Error('Both projections need interactions');
  const names = new Map();
  function alias(value) {
    if (value === '' || value == null) return '';
    if (typeof value !== 'string') throw Error('Invalid entity name');
    const key = value.toLowerCase();
    if (!names.has(key)) names.set(key, `Entity${String(names.size + 1).padStart(4,'0')}`);
    return names.get(key);
  }
  const clean = rows.map(rs => ({interactions:rs.map(r => {
    const out = {actorName:alias(r.actorName), targetName:alias(r.targetName)};
    for (const field of ['interactionType','actionType','harmType','aidType','success']) {
      const value = r[field] ?? '';
      if (!allowedLabels.has(value)) throw Error(`Unreviewed categorical value in ${field}`);
      out[field] = value;
    }
    const amount = r.amount ?? r.value;
    if (!Number.isSafeInteger(amount)) throw Error('Invalid amount');
    out.amount = amount;
    return out;
  }), chat:[]}));
  // Validate everything before creating output; refuse an existing directory.
  fs.mkdirSync(output);
  write(path.join(output,'kparser-v1.json'), {parity:clean[0]});
  write(path.join(output,'kparser2.json'), {parity:clean[1]});
  write(path.join(output,'manifest.json'), {schema_version:1, scenario,
    dataset_kind:'sanitized-interaction-projections', sanitizer:'allowlist-v1',
    counts:clean.map(d=>d.interactions.length),
    files:['kparser-v1.json','kparser2.json'].map(file=>({file,sha256:hash(path.join(output,file))})),
    evidence:{state:'unreviewed',ui:'unobserved',human:'unobserved'},
    boundary_quality:'unverified',
    excluded:['raw packets','raw ChatLines','chat bodies','timestamps','source IDs','name map'],
    scope:'Comparator regression only; not wire decoder replay or approved expected truth.'});
  return {output, counts:clean.map(d=>d.interactions.length)};
}

if (require.main === module) {
  try {
    const [mode,...args] = process.argv.slice(2);
    const result = mode === 'normalize-chat' && args.length === 2 ? normalizeChat(...args)
      : mode === 'export-pair' && args.length === 4 ? exportPair(...args)
      : (()=>{throw Error('Usage: normalize-chat INPUT OUTPUT | export-pair V1_JSON V2_JSON NEW_DIR SCENARIO');})();
    console.log(JSON.stringify(result));
  } catch(e) { console.error(e.message); process.exitCode=1; }
}
module.exports = {normalizeChat, exportPair};
