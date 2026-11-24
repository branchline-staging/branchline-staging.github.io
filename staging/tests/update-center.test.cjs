"use strict";
const test=require("node:test"),assert=require("node:assert/strict");
const {inspect,restartPath}=require("../updates/check.js");
const a="a".repeat(64),b="b".repeat(64);
test("current and new web revision",()=>{
 assert.equal(inspect({schema:1,channel:"staging",webRevision:a},a),"current");
 assert.equal(inspect({schema:1,channel:"staging",webRevision:b},a),"available");
});
test("fail closed on malformed or production manifest",()=>{
 for(const data of [null,{}, {schema:2,channel:"staging",webRevision:b},{schema:1,channel:"production",webRevision:b},{schema:1,channel:"staging",webRevision:"R7"}])
 assert.equal(inspect(data,a),"invalid");
 assert.equal(inspect({schema:1,channel:"staging",webRevision:b},""),"invalid");
});

test("restart targets are fixed app entries and do not replay Studio tickets",()=>{
 assert.equal(restartPath("/megaframe-studio/studio/"),"/first-megasaver-ptg/studios/");
 assert.equal(restartPath("/flyer-studio/studio/"),"/first-megasaver-ptg/studios/");
 assert.equal(restartPath("/sales-tracker/apps/"),"/sales-tracker/apps/");
 assert.equal(restartPath("/first-megasaver-ptg/workspace/mobile/"),"/first-megasaver-ptg/workspace/mobile/");
 assert.equal(restartPath("/unrelated/"),null);
});
