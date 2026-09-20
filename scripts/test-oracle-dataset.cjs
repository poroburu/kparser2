const {test} = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const {normalizeChat,exportPair} = require('./oracle-dataset.cjs');
const header = '28,00,00,80c08080,00000253,000002b1,004e,00,01,02,00,00fb0800,00000000,00000000,5d867978,00000000,1a127d88,000004f3,00af3f78,00000000,00,';
function temp(t) { const p=fs.mkdtempSync(path.join(os.tmpdir(),'oracle-test-')); t.after(()=>fs.rmSync(p,{recursive:true})); return p; }
test('normalization preserves header, body bytes, duplicates, CRLF and timestamp; is idempotent',t=>{
 const p=temp(t), input=path.join(p,'raw'), out=path.join(p,'out');
 const body='Actor hits Target for 42 points of damage.\x7f1';
 const raw='# capture\r\n'+(header+'\x1e\x01\x1e\x01\x1eQ[10:50:15]\x1e\x01 '+body+'\r\n').repeat(2);
 fs.writeFileSync(input,raw);
 assert.equal(normalizeChat(input,out).changed_lines,2);
 assert.equal(fs.readFileSync(input,'utf8'),raw);
 assert.equal(fs.readFileSync(out,'utf8'),'# capture\r\n'+(header+'[10:50:15] '+body+'\r\n').repeat(2));
 assert.equal(normalizeChat(out,path.join(p,'again')).changed_lines,0);
 assert.throws(()=>normalizeChat(input,input)); assert.throws(()=>normalizeChat(input,out));
});
test('unrecognized timestamps and body text remain untouched; malformed headers fail',t=>{
 const p=temp(t), input=path.join(p,'raw'),out=path.join(p,'out');
 const raw=header+'\x1eQ[99:99:99]\x1e\x01 Actor says [10:00:00], hi';
 fs.writeFileSync(input,raw); assert.equal(normalizeChat(input,out).changed_lines,0);
 assert.equal(fs.readFileSync(out,'utf8'),raw);
 fs.writeFileSync(input,'bad,header'); assert.throws(()=>normalizeChat(input,path.join(p,'bad')));
});
test('observed checker wrapping retains body and records inherited timestamp provenance',t=>{
 const p=temp(t),input=path.join(p,'raw'),out=path.join(p,'out');
 const checker='\x1eQ[\x1e\x06checker\x1eQ]\x1e\x01 Mob (Low Evasion, High ';
 const continuation='\x1e\x01\x81@\x1ejDefense\x1e\x01\x1eQ)\x1e\x01';
 const raw=header+'\x1ej\x1e\x01\x1eQ[10:00:00]\x1e\x01 '+checker+'\r\n'+header+continuation+'\r\n'+header+continuation+'\r\n';
 fs.writeFileSync(input,raw); assert.equal(normalizeChat(input,out).changed_lines,2);
 assert.equal(fs.readFileSync(out,'utf8'),header+'[10:00:00] '+checker+'\r\n'+header+'[10:00:00] '+continuation+'\r\n'+header+continuation+'\r\n');
 assert.equal(fs.readFileSync(input,'utf8'),raw);
 const meta=JSON.parse(fs.readFileSync(out+'.provenance.json','utf8'));
 assert.equal(meta.derived_timestamps.length,1);assert.equal(meta.derived_timestamps[0].line,2);assert.equal(meta.derived_timestamps[0].source_line,1);
 assert.equal(normalizeChat(out,path.join(p,'again')).changed_lines,0);
});
test('export keeps duplicate amounts and shared aliases, drops arbitrary payload and identifiers',t=>{
 const p=temp(t), a=path.join(p,'a'),b=path.join(p,'b'),out=path.join(p,'export');
 const row={actorName:'PrivatePlayer',targetName:'PrivatePet',amount:42,actionType:'Melee',success:'hit',sourcePacketId:'secret',text:'private chat'};
 fs.writeFileSync(a,JSON.stringify({parity:{interactions:[row,row]},secret:'private'}));
 fs.writeFileSync(b,JSON.stringify({interactions:[row]}));
 exportPair(a,b,out,'bst-pet');
 const clean=fs.readFileSync(path.join(out,'kparser-v1.json'),'utf8');
 assert.ok(!/Private|secret|private/.test(clean));
 const rows=JSON.parse(clean).parity.interactions; assert.equal(rows.length,2); assert.equal(rows[0].amount,42);
 assert.deepEqual(rows[0],JSON.parse(fs.readFileSync(path.join(out,'kparser2.json'))).parity.interactions[0]);
 row.success='PrivatePlayer'; fs.writeFileSync(b,JSON.stringify({interactions:[row]}));
 assert.throws(()=>exportPair(a,b,path.join(p,'rejected'),'bst-pet'));
 assert.ok(!fs.existsSync(path.join(p,'rejected')));
});
