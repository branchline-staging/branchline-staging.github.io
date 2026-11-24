"use strict";
const assert=require("node:assert/strict"),fs=require("node:fs"),path=require("node:path"),vm=require("node:vm");
const root=process.argv[2];
if(!root)throw Error("Supply staging build directory");
async function run(app,own,old){
 const callbacks={},deleted=[],claims=[];
 const keys=[own,old,"branchline-user-preferences","megaframe-shell-fixture","flyer-shell-fixture","unrelated-cache",app==="sales-tracker"?"first-megasaver-ptg-shell-v3-r6-isolation":"branchline-shell-v200-r6-isolation"];
 const self={registration:{scope:"https://branchline-staging.github.io/"+app+"/"},location:{origin:"https://branchline-staging.github.io"},addEventListener:(event,cb)=>callbacks[event]=cb,clients:{claim:async()=>{claims.push("claimed");assert.deepEqual(deleted,[old]);}}};
 const caches={keys:async()=>keys,delete:async key=>{deleted.push(key);return true;},open:async()=>({match:async()=>undefined})};
 vm.runInNewContext(fs.readFileSync(path.join(root,app,"sw.js"),"utf8"),{self,caches,URL,Set,Promise,Response});
 let pending;
 callbacks.activate({waitUntil:p=>{pending=p}});
 await pending;
 assert.deepEqual(deleted,[old]);
 assert.equal(claims.length,1);
 console.log("PASS cache isolation",app,"preserved peer app and unrelated caches");
}
(async()=>{
 await run("sales-tracker","branchline-shell-v200-r6-isolation","branchline-shell-v199");
 await run("first-megasaver-ptg","first-megasaver-ptg-shell-v3-r6-isolation","first-megasaver-ptg-shell-v2");
 assert.equal(fs.existsSync(path.join(root,"first-megasaver-ptg/branchline-workspace-windows.zip")),false);
 console.log("PASS old unsigned staging installer not published");
})().catch(e=>{console.error(e);process.exitCode=1;});
