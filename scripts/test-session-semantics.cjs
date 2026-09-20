const test=require('node:test'),assert=require('node:assert/strict');
const {counted,compare}=require('./session-semantics.cjs');
const {trailingPartial,selectAnchors}=require('./run-session-parity.cjs');

test('UI execution failure preserves completed state evidence and cannot report success',t=>{
 const fs=require('fs'),os=require('os'),path=require('path');
 const {recordFailure}=require('./run-session-parity.cjs');
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'failure-test-'));t.after(()=>fs.rmSync(dir,{recursive:true}));
 const file=path.join(dir,'session-qa.json');
 recordFailure(dir,new Error('build failed'));assert.equal(JSON.parse(fs.readFileSync(file)).state.status,'incomplete');
 const state={status:'failed',reports:'reports.json'},sources={capture_sha256:'hash',commit:'commit'};
 fs.writeFileSync(file,JSON.stringify({state,sources,chat:{status:'equal'},ui:{status:'incomplete'}}));
 recordFailure(dir,new Error('ui failed'));const result=JSON.parse(fs.readFileSync(file));
 assert.equal(result.status,'failed');assert.equal(result.error,'ui failed');assert.deepEqual(result.state,state);
 assert.deepEqual(result.sources,sources);assert.equal(result.chat.status,'equal');assert.equal(result.ui.status,'incomplete');
});

test('provenance joins interleaved rows once without consuming intervening messages',t=>{
 const fs=require('fs'),os=require('os'),path=require('path');
 const {provenance}=require('./session-semantics.cjs');
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'trace-test-'));t.after(()=>fs.rmSync(dir,{recursive:true}));
 const packets=path.join(dir,'packets'),chat=path.join(dir,'chat'),header='00,'.repeat(21);
 fs.writeFileSync(packets,'');
 const rows=['[10:00:00] Pet hits Mob.','[10:00:00] Actor hits Mob.','[10:00:01] Additional effect: 7 damage.',
 '[10:00:02] Actor hits Mob.','[10:00:03] High','[10:00:03] \x81@Defense',
 '[10:00:04] Starts.','[10:00:15] Finishes.'];
 fs.writeFileSync(chat,rows.map(r=>header+r).join('\n'));
 const messages=['Pet hits Mob.Additional effect: 7 damage.','Actor hits Mob.','Actor hits Mob.',
 'Actor hits Mob.','HighDefense','Starts.Finishes.'].map(text=>({text}));
 const result=provenance({messages},packets,chat,{start_utc:'2026-09-19T14:00:00Z',local_utc_offset_minutes:-240});
 assert.deepEqual(result.messages[0].chat_lines,[1,3]);
 assert.deepEqual(result.messages[1].chat_lines,[2]);assert.deepEqual(result.messages[2].chat_lines,[4]);
 assert.equal(result.messages[3],undefined);assert.deepEqual(result.messages[4].chat_lines,[5,6]);
 assert.equal(result.messages[5],undefined);
});
test('anchors reject repeated signatures and duplicate source packets',()=>{
 const row=(amount,id)=>({attribution:'complete',left:{actor:'Alice',target:'Mob',kind:'damage',amount,outcome:'hit',source:{chat_lines:[amount]}},right:{source:{packet_line:amount,packet_id:id}}});
 const rows=[row(1,'a'),row(2,'b'),row(3,'c')];
 assert.equal(selectAnchors(rows).length,3);
 assert.throws(()=>selectAnchors([rows[0],rows[0],rows[2]]),/unambiguous/);
 assert.throws(()=>selectAnchors([row(1,'a'),row(2,'a'),row(3,'c')]),/Duplicate/);
});
test('only one trailing open fight with matching identity can explain report difference',()=>{
 const closed={StartMs:0,EndMs:10,Killed:true,EnemyName:'Other'};
 const open={StartMs:20,EndMs:null,Killed:false,EnemyName:'Test_Mob'};
 const reports={fights:{missing:[],extra:[{enemy:'Test Mob',killed:false,killer:'',experience:0,chain:0}]}};
 assert.equal(trailingPartial({Battles:[closed,open]},reports),true);
 assert.equal(trailingPartial({Battles:[{...open,StartMs:-1},closed]},reports),false);
 assert.equal(trailingPartial({Battles:[open,{...open,StartMs:30}]},reports),false);
 reports.fights.extra[0].enemy='Wrong Mob';assert.equal(trailingPartial({Battles:[closed,open]},reports),false);
});
const chat=(speaker,message,mode='Say')=>({speaker,message,mode});
function pair(rows){return [{parity:{chat:rows}},{ChatMessages:rows.map(r=>({Speaker:r.speaker,Message:r.message,Mode:r.mode,Direction:'incoming'}))}];}
test('counted comparison preserves repeated occurrences',()=>{
 assert.equal(counted(['a','a'],['a']).status,'mismatch');
 assert.equal(counted(['a','b'],['b','a']).status,'equal');
});
test('shared chat retains repetition control codes and auto translate bytes',()=>{
 const rows=[chat('Alice','hello\x1e\x01\xfd world'),chat('Alice','hello\x1e\x01\xfd world')];
 const [a,b]=pair(rows);assert.equal(compare(a,b).chat.status,'equal');
 b.ChatMessages.pop();assert.equal(compare(a,b).chat.status,'mismatch');
});
test('chat sequence is checked independently of counted content',()=>{
 const [a,b]=pair([chat('Alice','one'),chat('Bob','two')]);b.ChatMessages.reverse();
 const r=compare(a,b).chat;assert.equal(r.missing.length,0);assert.equal(r.sequence_equal,false);assert.equal(r.status,'mismatch');
});
test('only declared addon forms are excluded',()=>{
 assert.equal(compare({parity:{chat:[chat('','Fight','Party')]}},{}).chat.status,'equal');
 assert.equal(compare({parity:{chat:[chat('','something else','Party')]}},{}).chat.status,'mismatch');
 assert.equal(compare({parity:{chat:[chat('Alice','Fight','Party')]}},{}).chat.status,'mismatch');
});
test('different speakers and modified encoding remain differences',()=>{
 const [a,b]=pair([chat('Alice','hello\xfd')]);b.ChatMessages[0].Speaker='Bob';
 assert.equal(compare(a,b).chat.status,'mismatch');b.ChatMessages[0].Speaker='Alice';b.ChatMessages[0].Message='hello';
 assert.equal(compare(a,b).chat.status,'mismatch');
});
