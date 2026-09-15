// One offline entry point. All outputs remain private unless separately sanitized.
const fs=require('node:fs'),path=require('node:path'),cp=require('node:child_process'),crypto=require('node:crypto');
const {read,hash,counted,compare,provenance}=require('./session-semantics.cjs');
const repo=path.resolve(__dirname,'..'),root=path.dirname(repo);
function trailingPartial(right,reports){
 const battles=right.Battles??[],open=battles.filter(b=>b.EndMs==null&&!b.Killed);
 const last=[...battles].sort((a,b)=>a.StartMs-b.StartMs).at(-1);
 const extra=reports.fights.extra;
 return open.length===1&&open[0]===last&&extra.length===1&&reports.fights.missing.length===0&&
  !extra[0].killed&&extra[0].experience===0&&extra[0].chain===0&&!extra[0].killer&&
  extra[0].enemy.toLowerCase()===last.EnemyName.replaceAll('_',' ').toLowerCase();
}
function selectAnchors(matched){
 const unique=matched.filter(m=>m.attribution==='complete'&&m.left.source.chat_lines?.length&&m.right.source.packet_line);
 const key=m=>JSON.stringify([m.left.actor,m.left.target,m.left.kind,m.left.amount,m.left.outcome]);
 const counts=new Map();for(const m of unique)counts.set(key(m),(counts.get(key(m))??0)+1);
 const anchors=unique.filter(m=>counts.get(key(m))===1);
 if(anchors.length<3)throw Error('Insufficient unambiguous anchors; inspect alignment');
 const selected=[anchors[0],anchors[Math.floor(anchors.length/2)],anchors.at(-1)];
 if(new Set(selected.map(m=>m.right.source.packet_id)).size!==3)throw Error('Duplicate alignment anchors');
 return selected;
}
function command(exe,args,log){const r=cp.spawnSync(exe,args,{cwd:root,encoding:'utf8',windowsHide:true,maxBuffer:64*1024*1024});
 fs.writeFileSync(log,(r.stdout??'')+(r.stderr??''));if(r.error)throw r.error;return r.status;}
function run(packets,chat,manifestPath,output,uiRun=null){
 for(const p of [packets,chat,manifestPath])if(!fs.statSync(p).isFile())throw Error('Missing input');
 const alignment=read(manifestPath);
 if(alignment.output_hashes?.packets!==hash(packets)||alignment.output_hashes?.chat!==hash(chat))throw Error('Alignment/input hash mismatch; regenerate with align-oracle-window');
 if(!Number.isFinite(Date.parse(alignment.start_utc))||Date.parse(alignment.end_utc_exclusive)<=Date.parse(alignment.start_utc))throw Error('Invalid alignment window');
 fs.mkdirSync(output);
 const save=(file,obj)=>fs.writeFileSync(path.join(output,file),JSON.stringify(obj,null,2)+'\n');
 const invoke=(exe,args,log,allowed=[0])=>{const code=command(exe,args,path.join(output,log));if(!allowed.includes(code))throw Error(`${log}: exit ${code}`);};
 const frozen=path.join(output,'capture.ndjson'),frozenChat=path.join(output,'oracle.chatlines.txt');
 fs.copyFileSync(packets,frozen);fs.copyFileSync(chat,frozenChat);save('alignment.json',alignment);
 const build=path.join(output,'build');
 invoke('dotnet',['build',path.join(repo,'kparser2.Cli/kparser2.Cli.fsproj'),'--artifacts-path',build,'-v','quiet'],'build.log');
 const cli=path.join(build,'bin/kparser2.Cli/debug/kparser2.Cli.exe');
 const oracle=path.join(root,'kparser/kparser.Cli/bin/x86/Debug/kparser.cli.exe');
 const leftPath=path.join(output,'v1.json'),rightPath=path.join(output,'v2.json');
 invoke(oracle,['snapshot',frozenChat,'--json','-o',leftPath],'oracle.log');
 invoke(cli,['analytics','snapshot',frozen,'--json','-o',rightPath],'replay.log');
 const reportPath=path.join(output,'reports.json');
 invoke('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File',path.join(__dirname,'compare-reports.ps1'),'-KparserJson',leftPath,'-Kparser2Json',rightPath,'-OutputPath',reportPath],'reports.log',[0,1]);
 const left=read(leftPath),right=read(rightPath),reports=read(reportPath);
 const context=provenance(left,frozen,frozenChat,alignment);
 const untraced=(left.messages??[]).map((m,i)=>m.text&&!context.messages[i]?i:null).filter(i=>i!==null);
 if(untraced.length)throw Error(`Missing original ChatLine provenance for messages: ${untraced.join(',')}`);
 const semantics=compare(left,right,context);
 if(semantics.interactions.matched.some(m=>!m.right.source.packet_line))throw Error('Missing packet provenance for shared interactions');
 // Distinct fully attributed event signatures, not whichever window minimizes a diff.
 const selected=selectAnchors(semantics.interactions.matched);
 save('anchors.json',{boundary_quality:alignment.boundary_quality,correlation_delay_ms:[-1500,10000],anchors:selected});
 save('semantics.json',semantics);
 // Partial capture-end fights remain visible; offense retains their events.
 const openIds=new Set(right.Battles.filter(b=>b.EndMs==null&&!b.Killed).map(b=>b.Id));
 const partial=right.Battles.filter(b=>openIds.has(b.Id));
 const partialExplained=trailingPartial(right,reports);
 // Won/lost item distribution uses the same FIFO pool lifecycle as LootResolution.
 const pools=new Map(),lootRight=[];
 for(const r of right.LootRecords??[]){
  if(r.EventType==='Found'&&r.ItemId>0){if(!pools.has(r.PoolSlot))pools.set(r.PoolSlot,[]);pools.get(r.PoolSlot).push(r.ItemName);}
  if(['Won','Lost','Floor'].includes(r.EventType)){
   const item=r.ItemName.startsWith('Pool slot ')?pools.get(r.PoolSlot)?.shift():r.ItemName;
   lootRight.push({item:canonicalItem(item),actor:(r.ActorName??'').toLowerCase(),gil:r.Gil,lost:r.EventType!=='Won'});
  }
 }
 const lootLeft=(left.loot??[]).map(r=>({item:canonicalItem(r.itemName),actor:(r.actorName??'').toLowerCase(),gil:r.gil,lost:r.lost}));
 const gilEvidence=[];
 (left.messages??[]).forEach((r,i)=>{if(r.parseSuccessful)return;const m=/^(.+) obtains (\d+) gil\.$/.exec(r.text);if(m){lootLeft.push({item:'gil',actor:m[1].toLowerCase(),gil:Number(m[2]),lost:false});gilEvidence.push({message_index:i,...context.messages[i],text:r.text,reason:'Raw ChatLine currency oracle; v1 parser did not classify it'});}});
 const loot=counted(lootLeft,lootRight);save('loot.json',{...loot,raw_currency_evidence:gilEvidence});
 // Defense outcomes are the same traced damage interactions with a player/pet target.
 const allies=new Set(right.Combatants.filter(c=>['Player','Pet','Fellow'].includes(c.Kind)).map(c=>c.Name.toLowerCase()));
 const defenseMissing=semantics.interactions.missing.filter(r=>allies.has(r.target)&&r.kind==='damage');
 const defenseExtra=semantics.interactions.extra.filter(r=>allies.has(r.target)&&r.kind==='damage');
 const defense={status:defenseMissing.length||defenseExtra.length?'mismatch':'equal',missing:defenseMissing,extra:defenseExtra,
  matched:semantics.interactions.matched.filter(r=>allies.has(r.left.target)&&r.left.kind==='damage').length};
 save('defense.json',defense);
 let uiPath=uiRun;
 if(!uiPath){const dir=path.join(output,'ui');invoke('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File',path.join(__dirname,'test-ui-replay.ps1'),'-CapturePath',frozen,'-OutputDir',dir],'ui.log');uiPath=path.join(dir,'ui-run.json');}
 const ui=read(uiPath);
 invoke('powershell',['-NoProfile','-ExecutionPolicy','Bypass','-File',path.join(__dirname,'validate-session-ui.ps1'),'-Manifest',uiPath,'-Capture',frozen],'ui-validation.log');
 if(ui.capture_sha256.toLowerCase()!==hash(frozen))throw Error('UI hash mismatch');
 const uiDir=path.dirname(uiPath);
 for(const c of ui.cases??[]){for(const field of ['screenshot','actual_text','expected_text'])if(c[field]&&!fs.existsSync(path.join(uiDir,c[field])))throw Error('Missing UI artifact');}
 const independent=require('./check-ui-oracle.cjs').check(leftPath,uiPath);save('ui-independent.json',independent);
 const contentPassed=ui.status==='passed'&&ui.failure_count===0&&ui.case_count===ui.cases.length&&ui.cases.length>0&&independent.status==='passed';
 const statePassed=(reports.fights.equal||partialExplained)&&reports.offense.equal&&reports.experience.equal&&loot.status==='equal'&&defense.status==='equal'&&semantics.interactions.status==='equal';
 const result={schema_version:1,status:statePassed&&semantics.chat.status==='equal'&&contentPassed?'automated-passed':'failed',
  sources:{capture_sha256:hash(frozen),chat_sha256:hash(frozenChat),alignment_sha256:hash(manifestPath),cli_sha256:hash(cli),oracle_sha256:hash(oracle),
   tools:Object.fromEntries(['run-session-parity.cjs','session-semantics.cjs','check-ui-oracle.cjs','validate-session-ui.ps1','compare-reports.ps1','parity-evidence.ps1'].map(f=>[f,hash(path.join(__dirname,f))])),
   commit:cp.spawnSync('git',['-C',repo,'rev-parse','HEAD'],{encoding:'utf8',windowsHide:true}).stdout.trim(),
   diff_sha256:crypto.createHash('sha256').update(cp.spawnSync('git',['-C',repo,'diff','HEAD'],{encoding:'utf8',windowsHide:true}).stdout).digest('hex')},
  state:{status:statePassed?'passed':'failed',reports:'reports.json',semantics:'semantics.json',loot:'loot.json',defense:'defense.json'},
  chat:{status:semantics.chat.status,artifact:'semantics.json'},
  ui:{status:contentPassed?'passed':'failed',manifest:path.resolve(uiPath),independent:'ui-independent.json',visual_review:'unobserved'},human:{status:'unobserved'},
  exclusions:{trailing_partial_fights:partial,partial_fight_difference_explained:partialExplained,unmatched_capture_tails:alignment.unmatched_capture_tails??'not recorded in older alignment',start_boundary:'May begin mid-fight; retained events are scored, earlier combat is unavailable'},
  coverage_gaps:['No live connection validation','No human acceptance','Ordinary channel/echo coverage requires synthetic fixtures','Unknown parameterized chat templates remain unsupported'],
  limitations:['Timestamp alignment is approximate','Legacy missing actor/action fields validate shared fields only','Screenshots require separate visual inspection']};
 save('session-qa.json',result);return result;
}
function canonicalItem(item){const s=(item??'').toLowerCase();return s==="beastmen's seal"?'beastmens seal':s;}
if(require.main===module){let failureOutput=null;try{const [p,c,m,o,...rest]=process.argv.slice(2);if(!o||rest.length>1)throw Error('Usage: PACKETS NORMALIZED_CHAT ALIGNMENT NEW_OUTPUT [UI_MANIFEST]');
 if(!fs.existsSync(path.resolve(o)))failureOutput=path.resolve(o);
 const result=run(...[p,c,m,o].map(x=>path.resolve(x)),rest[0]?path.resolve(rest[0]):null);console.log(JSON.stringify(result));process.exitCode=result.status==='automated-passed'?0:1;
}catch(e){if(failureOutput&&fs.existsSync(failureOutput))fs.writeFileSync(path.join(failureOutput,'session-qa.json'),JSON.stringify({schema_version:1,status:'failed',error:e.message,state:{status:'incomplete'},chat:{status:'incomplete'},ui:{status:'incomplete'},human:{status:'unobserved'}},null,2)+'\n');console.error(e.stack);process.exitCode=2;}}
module.exports={run,trailingPartial,selectAnchors};
