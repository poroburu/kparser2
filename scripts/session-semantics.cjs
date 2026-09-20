const fs = require('node:fs');
const crypto = require('node:crypto');
const read = p => JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const name = s => (s??'').replaceAll('_',' ').toLowerCase();
function provenance(v1,packetPath,chatPath,alignment) {
 const packets={};
 fs.readFileSync(packetPath,'utf8').split(/\r?\n/).forEach((line,index)=>{if(!line)return;const r=JSON.parse(line);if(!r.meta)return;
  const m=typeof r.meta==='string'?JSON.parse(r.meta):r.meta;
  packets[`${m.session_uuid}:${m.message_id}`]={packet_line:index+1,timestamp_ms:m.timestamp,blocked:m.metadata?.blocked===true};
 });
 const offset=alignment.local_utc_offset_minutes*60000;
 const day=new Date(Date.parse(alignment.start_utc)+offset).toISOString().slice(0,10);
 // Provenance lookup only: mirror legacy display controls without altering chat comparison.
 const compact=s=>s.replace(/[\x1e\x1f]./g,'').replace(/\x7f[1\xfb\xfc]/g,'').replace(/\x81\xa8/g,'→').replace(/\x81@/g,' ').replace(/\s/g,'');
 const rows=fs.readFileSync(chatPath,'utf8').split(/\r?\n/).map((line,index)=>{
  const m=/^(?:[^,]*,){21}\[(\d{2}:\d{2}:\d{2})\]\s*(.*)$/.exec(line);
  return m?{line:index+1,time:Date.parse(day+'T'+m[1]+'Z')-offset,text:compact(m[2])}:null;
 }).filter(Boolean);
 const messages={},used=new Set();
 (v1.messages??[]).forEach((m,index)=>{
  const expected=compact(m.text);if(!expected)return;
  for(let i=0;i<rows.length;i++){
   if(used.has(i)||!rows[i].text||!expected.startsWith(rows[i].text))continue;
   let combined='',parts=[];
   // Legacy can join a finish/additional effect across interleaved actors.
   // Consume only matching fragments, once each, within the correlation bound.
   for(let j=i;j<rows.length&&rows[j].time-rows[i].time<=10000&&parts.length<5;j++){
    if(used.has(j)||!rows[j].text||!expected.startsWith(combined+rows[j].text))continue;
    combined+=rows[j].text;parts.push(j);
    if(combined===expected){messages[index]={chat_lines:parts.map(k=>rows[k].line),timestamp_ms:rows[i].time};parts.forEach(k=>used.add(k));return;}
   }
  }
 });
 return {messages,packets};
}
function counted(left,right,key=JSON.stringify) {
  const remaining=right.map((row,index)=>({row,index,key:key(row)})), missing=[],matched=[];
  for(const row of left){const i=remaining.findIndex(r=>r.key===key(row));if(i<0)missing.push(row);else matched.push({left:row,right:remaining.splice(i,1)[0].row});}
  return {status:missing.length||remaining.length?'mismatch':'equal',left_count:left.length,right_count:right.length,matched_count:matched.length,missing,extra:remaining.map(r=>r.row)};
}
function compare(v1,v2,context={}) {
 const exclusions=[],left=[],right=[];
 const add=(list,source,actor,target,kind,amount,outcome,action=null)=>list.push({source,actor:name(actor)||null,target:name(target)||null,kind,amount,outcome,action});
 (v1.messages??[]).forEach((m,index)=>{
  const c=m.combat;if(!c)return;
  const source={side:'v1',message_index:index,text:m.text,...context.messages?.[index]};
  if(c.isPreparing){exclusions.push({source,classification:'kparser-only',reason:'Preparation is not a finished outcome'});return;}
  if(!c.targets?.length){exclusions.push({source,classification:'kparser-only',reason:'Activation without a represented target outcome'});return;}
  for(const t of c.targets){
   const defense=t.defenseType, failure=t.failedActionType;
   let kind=c.interactionType==='Death'?'death':c.interactionType==='Aid'?(c.aidType==='Recovery'||t.aidType==='Recovery'?'recovery':'enhance'):(c.harmType==='Enfeeble'||t.harmType==='Enfeeble')?'enfeeble':'damage';
   let outcome=kind==='death'?'death':defense==='Shadow'?'shadow-absorb':defense==='Parry'?'parry':defense==='Evasion'?'miss':failure==='NoEffect'?'no-effect':c.successLevel==='Unsuccessful'?'miss':'hit';
   if(failure==='OutOfRange'){kind='out-of-range';outcome='message';}
   if(m.text.includes(' is intimidated by ')){kind='intimidated';outcome='intimidated';}
   if(outcome==='no-effect')kind='no-effect';
   add(left,source,c.actorName,t.name,kind,['damage','recovery'].includes(kind)?t.amount:0,outcome,
       c.actionType==='Unknown'||kind!=='damage'?null:c.actionType.toLowerCase());
  }
 });
 for(const r of v2.Interactions??[]){
  const source={side:'v2',packet_id:r.SourcePacketId,interaction_id:r.Id,message_id:r.MessageId,command:r.CommandNo,...context.packets?.[r.SourcePacketId?.split(':').slice(0,2).join(':')]};
  if(r.CommandNo===0 && r.MessageId===206){exclusions.push({source,classification:'shared-chat',reason:'Status expiry compared in the chat projection'});continue;}
  if(r.CommandNo===0 && [8,565].includes(r.MessageId)){exclusions.push({source,classification:'shared-report',reason:r.MessageId===8?'XP compared in experience report':'Gil compared in loot report'});continue;}
  if(r.CommandNo===0 && r.MessageId===177 && source.blocked===true){exclusions.push({source,classification:'kparser2-extra',reason:'Blocked check packet replaced by the checker addon'});continue;}
  let kind=r.InteractionType==='Death'?'death':r.InteractionType==='Aid'?(r.AidType==='Recovery'?'recovery':'enhance'):r.HarmType==='Enfeeble'?'enfeeble':'damage';
  if(r.CommandNo===0 && r.MessageId===4)kind='out-of-range';
  if(r.InteractionType==='Unknown'&&kind!=='out-of-range')kind='unclassified';
  if(r.Success==='intimidated')kind='intimidated';
  if(r.Success==='no-effect')kind='no-effect';
  add(right,source,r.ActorName,r.TargetName,kind,['damage','recovery'].includes(kind)?r.Value:0,
      kind==='death'?'death':r.Success,kind==='damage'?(r.HarmType??'').toLowerCase():null);
 }
 const remaining=[...right],missing=[],matched=[],ambiguous=[];
 for(const l of left){
  const candidates=remaining.filter(r=>l.kind===r.kind&&l.amount===r.amount&&l.outcome===r.outcome&&
    (!l.actor||l.actor===r.actor)&&(!l.target||l.target===r.target)&&(!l.action||l.action===r.action)&&
    // Client animation delays displayed messages after packet receipt. This is
    // a bounded correlation window, not a claim of synchronized timestamps.
    (l.source.timestamp_ms==null||r.source.timestamp_ms==null||
      (l.source.timestamp_ms-r.source.timestamp_ms>=-1500 && l.source.timestamp_ms-r.source.timestamp_ms<=10000)));
  const identities=new Set(candidates.map(r=>JSON.stringify([r.actor,r.target,r.action])));
  if(identities.size>1 && l.actor && l.action){ambiguous.push(l);continue;}
  if(!candidates.length){missing.push(l);continue;}
  const r=candidates[0];remaining.splice(remaining.indexOf(r),1);matched.push({left:l,right:r,
    attribution:!l.actor||!l.action?'shared-fields-only':'complete'});
 }
 const chatLeft=[],chatExclusions=[];
 (v1.parity?.chat??[]).forEach((r,index)=>{
  // Exact observed addon forms only. Unknown empty-speaker messages remain failures.
  if(!r.speaker && ((r.mode==='Party'&&['Fight','Lamb Chop','Reward'].includes(r.message))||
    (r.mode==='Say'&&r.message.startsWith('\x1eQ[\x1e\x06checker\x1eQ]'))))
    chatExclusions.push({index,row:r,classification:'kparser-only',reason:'Observed local addon/command echo'});
  else chatLeft.push({speaker:r.speaker||'System',mode:r.mode,message:r.message});
 });
 const chatRight=(v2.ChatMessages??[]).filter(r=>r.Direction==='incoming').map(r=>({speaker:r.Speaker,mode:r.Mode,message:r.Message}));
 const chat=counted(chatLeft,chatRight);
 chat.inventory={legacy:v1.parity?.chat??[],packet:(v2.ChatMessages??[]).map(r=>({direction:r.Direction,mode:r.Mode,speaker:r.Speaker,body:r.Message})),
  outgoing_scope:'Outgoing records are retained in inventory; shared comparison uses received display messages'};
 chat.sequence_equal=JSON.stringify(chatLeft)===JSON.stringify(chatRight);chat.exclusions=chatExclusions;
 if(!chat.sequence_equal)chat.status='mismatch';
 const interactions={status:missing.length||remaining.length||ambiguous.length?'mismatch':'equal',left_count:left.length,right_count:right.length,
   matched_count:matched.length,missing,extra:remaining,ambiguous,exclusions,matched};
 return {schema_version:1,interactions,chat,human:{status:'unobserved'}};
}
if(require.main===module){try{const [a,b,out,packets,chat,alignment]=process.argv.slice(2);if(!out)throw Error('Usage: V1_JSON V2_JSON OUTPUT [PACKETS CHAT ALIGNMENT]');
 const v1=read(a);const result=compare(v1,read(b),alignment?provenance(v1,packets,chat,read(alignment)):{});fs.writeFileSync(out,JSON.stringify(result,null,2)+'\n');
 console.log(JSON.stringify({interactions:result.interactions.status,missing:result.interactions.missing.length,extra:result.interactions.extra.length,chat:result.chat.status}));
 process.exitCode=result.interactions.status==='equal'&&result.chat.status==='equal'?0:1;
}catch(e){console.error(e.message);process.exitCode=2;}}
module.exports={read,hash,counted,compare,provenance};
