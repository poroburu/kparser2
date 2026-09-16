const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const {check}=require('./check-ui-oracle.cjs');
test('independent totals reject wrong values and invented actors',t=>{
 const dir=fs.mkdtempSync(path.join(os.tmpdir(),'ui-oracle-'));t.after(()=>fs.rmSync(dir,{recursive:true}));
 const v=path.join(dir,'v1.json'),u=path.join(dir,'ui-run.json');
 fs.writeFileSync(v,JSON.stringify({messages:[{combat:{interactionType:'Harm',harmType:'Damage',actorName:'Alice',actorEntityType:'Player',targets:[{name:'Mob',entityType:'Mob',amount:42}]}}],battles:[{experiencePoints:10}]}));
 fs.writeFileSync(u,'{}');fs.writeFileSync(path.join(dir,'defense-default.actual.txt'),'');fs.writeFileSync(path.join(dir,'experience-default.actual.txt'),'Total Experience : 10');
 fs.writeFileSync(path.join(dir,'deaths-default.actual.txt'),'Player Deaths');
 const offense=path.join(dir,'offense-default.actual.txt');fs.writeFileSync(offense,'Alice 42 100.0%');
 assert.equal(check(v,u).status,'passed');
 fs.writeFileSync(offense,'Alice 41 100.0%');assert.equal(check(v,u).status,'failed');
 fs.writeFileSync(offense,'Alice 42 100.0%\nGhost 8 10.0%');assert.equal(check(v,u).status,'failed');
});
