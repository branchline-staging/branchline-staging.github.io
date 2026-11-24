import assert from "node:assert/strict";
const origin="https://branchline-staging.github.io";
async function read(path){
 const response=await fetch(origin+path,{redirect:"error",cache:"no-store",headers:{"Cache-Control":"no-cache"}});
 assert.equal(response.status,200,path+" HTTP 200");
 return response;
}
const manifest=await (await read("/sales-tracker/updates/release.json")).json();
assert.equal(manifest.schema,1);
assert.equal(manifest.channel,"staging");
assert.match(manifest.webRevision,/^[a-f0-9]{64}$/);
for(const path of ["/sales-tracker/","/first-megasaver-ptg/workspace/mobile/","/first-megasaver-ptg/studios/","/megaframe-studio/studio/","/flyer-studio/studio/"]){
 const html=await (await read(path)).text();
 assert.ok(html.includes('src="/sales-tracker/updates/check.js"'),path+" checker loaded");
 assert.ok(html.includes('data-revision="'+manifest.webRevision+'"'),path+" revision stamp matches hosted manifest");
 console.log("PASS hosted",path);
}
const script=await (await read("/sales-tracker/updates/check.js")).text();
assert.ok(script.includes("cache:\"no-store\""));
assert.ok(script.includes("branchline-staging.github.io"));
assert.ok(!script.includes("location.reload("));
const css=await (await read("/sales-tracker/updates/check.css")).text();
assert.ok(css.includes(".bl-release-check"));
console.log("PASS hosted manifest, all app stamps, update checker and CSS");
