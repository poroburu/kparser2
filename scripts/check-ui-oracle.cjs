const fs=require('node:fs'),path=require('node:path');
const {read,hash}=require('./session-semantics.cjs');
function check(v1Path,uiPath){
 const v1=read(v1Path),dir=path.dirname(uiPath),checks=[];
 const offense=new Map(),defense=new Map();
 for(const m of v1.messages??[]){const c=m.combat;if(!c||c.isPreparing||c.interactionType!=='Harm'||c.harmType!=='Damage')continue;
  for(const t of c.targets??[]){
   if(['Player','Pet','Fellow'].includes(c.actorEntityType))offense.set(c.actorName,(offense.get(c.actorName)??0)+t.amount);
   if(['Player','Pet','Fellow'].includes(t.entityType))defense.set(t.name,(defense.get(t.name)??0)+t.amount);
  }
 }
 for(const [surface,expected] of [['offense',offense],['defense',defense]]){
  const file=path.join(dir,surface+'-default.actual.txt'),text=fs.readFileSync(file,'utf8').split(/Damage(?: Taken)? Details/)[0];
  const actual=new Map();for(const line of text.split(/\r?\n/)){const m=/^\s*(\S+)\s+(\d+)\s+\d/.exec(line);if(m)actual.set(m[1],Number(m[2]));}
  for(const [actor,total] of expected)checks.push({surface,actor,expected:total,actual:actual.get(actor),passed:actual.get(actor)===total,actual_sha256:hash(file)});
  for(const [actor,total] of actual)if(!expected.has(actor))checks.push({surface,actor,expected:null,actual:total,passed:false,actual_sha256:hash(file)});
 }
 const xp=(v1.battles??[]).reduce((n,b)=>n+b.experiencePoints,0);
 const xpFile=path.join(dir,'experience-default.actual.txt');
 const match=/Total Experience\s*:\s*(\d+)/.exec(fs.readFileSync(xpFile,'utf8'));
 checks.push({surface:'experience',expected:xp,actual:match?Number(match[1]):null,passed:match!=null&&Number(match[1])===xp,actual_sha256:hash(xpFile)});
 return {schema_version:1,source:'independent-v1-messages-and-battles',oracle_sha256:hash(v1Path),ui_manifest_sha256:hash(uiPath),
  status:checks.every(c=>c.passed)?'passed':'failed',checks};
}
if(require.main===module){const [v,u,out]=process.argv.slice(2);const r=check(v,u);fs.writeFileSync(out,JSON.stringify(r,null,2)+'\n');process.exitCode=r.status==='passed'?0:1;}
module.exports={check};
