"use strict";
const test=require("node:test"),assert=require("node:assert/strict");
const {inspect}=require("../updates/check.js");
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
