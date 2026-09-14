// Preserve private setup evidence; score a declared half-open wall-clock window.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
function align(packets, chat, output, startUtc, endUtc, offsetMinutes) {
  const start = Date.parse(startUtc), end = Date.parse(endUtc), offset = Number(offsetMinutes)*60000;
  if (!/Z$/.test(startUtc) || !/Z$/.test(endUtc) || !Number.isFinite(start+end+offset) || end<=start)
    throw Error('Use increasing UTC timestamps ending in Z and an explicit local UTC offset in minutes');
  const day = new Date(start+offset).toISOString().slice(0,10);
  if (new Date(end-1+offset).toISOString().slice(0,10)!==day) throw Error('Split windows crossing local midnight');
  const setupIds = new Set([0xA,0xD,0xE,0xDD,0xDF,0x68]);
  let setup=0, scored=0, lines=0;
  const packetRows = fs.readFileSync(packets,'utf8').split(/\r?\n/).filter(Boolean).filter(line=>{
    const row=JSON.parse(line);
    if(row.type==='kparser2.session') return true;
    const meta=typeof row.meta==='string'?JSON.parse(row.meta):row.meta;
    if(!Number.isFinite(meta.timestamp)) throw Error('Packet lacks timestamp');
    if(meta.timestamp>=start && meta.timestamp<end) {scored++; return true;}
    if(meta.timestamp<start && setupIds.has(meta.packet_id) && meta.direction==='incoming') {setup++;return true;}
    return false;
  });
  const chatRows=fs.readFileSync(chat,'utf8').split(/\r?\n/).filter(Boolean).filter(line=>{
    if(line.startsWith('#')) return true;
    const m=/^(?:[^,]*,){21}\[(\d{2}:\d{2}:\d{2})\]/.exec(line);
    if(!m) throw Error('ChatLine lacks normalized timestamp');
    const time=Date.parse(day+'T'+m[1]+'Z')-offset;
    if(time>=start && time<end) {lines++;return true;} return false;
  });
  if(!scored || !lines) throw Error('Both sides must contain scored records');
  fs.mkdirSync(output);
  fs.writeFileSync(path.join(output,'packets.ndjson'),packetRows.join('\n')+'\n');
  fs.writeFileSync(path.join(output,'oracle.chatlines.txt'),chatRows.join('\n')+'\n');
  const manifest={schema_version:1,start_utc:startUtc,end_utc_exclusive:endUtc,local_utc_offset_minutes:Number(offsetMinutes),
    boundary_quality:'timestamp-aligned-not-exact',setup_packets:setup,scored_packets:scored,chatlines:lines,
    source_hashes:{packets:hash(packets),chat:hash(chat)},
    setup_opcodes:[...setupIds],scope:'Private candidate; confirm event anchors and complete fights before interpreting differences.'};
  fs.writeFileSync(path.join(output,'alignment.json'),JSON.stringify(manifest,null,2)+'\n');
  return manifest;
}
if(require.main===module) {
  try {if(process.argv.length!==8) throw Error('Usage: PACKETS NORMALIZED_CHAT NEW_DIR START_UTC END_UTC LOCAL_OFFSET_MINUTES');
    console.log(JSON.stringify(align(...process.argv.slice(2))));
  }catch(e){console.error(e.message);process.exitCode=1;}
}
module.exports={align};
