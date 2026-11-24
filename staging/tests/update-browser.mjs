import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {chromium} from "playwright";

const origin="https://branchline-staging.github.io";
const revision="a".repeat(64), next="b".repeat(64), third="c".repeat(64);
const script=readFileSync(new URL("../updates/check.js",import.meta.url),"utf8");
const css=readFileSync(new URL("../updates/check.css",import.meta.url),"utf8");
const browser=await chromium.launch({headless:true});
try {
 for(const viewport of [{width:1280,height:800},{width:390,height:844}]){
  const context=await browser.newContext({viewport,serviceWorkers:"block"});
  let manifest={schema:1,channel:"staging",webRevision:revision};
  let fail=false, calls=0, navigations=0;
  await context.route("**/*",async route=>{
    const url=new URL(route.request().url());
    assert.equal(url.origin,origin,"No cross-origin requests");
    if(url.pathname==="/fixture/")return route.fulfill({contentType:"text/html",body:'<!doctype html><meta name="viewport" content="width=device-width,initial-scale=1"><link rel="stylesheet" href="/sales-tracker/updates/check.css"><textarea aria-label="Unsaved draft"></textarea><script defer src="/sales-tracker/updates/check.js" data-revision="'+revision+'"></script>'});
    if(url.pathname.endsWith("/check.js"))return route.fulfill({contentType:"application/javascript",body:script});
    if(url.pathname.endsWith("/check.css"))return route.fulfill({contentType:"text/css",body:css});
    if(url.pathname.endsWith("/release.json")){
      calls++;
      if(fail)return route.abort("internetdisconnected");
      return route.fulfill({json:manifest});
    }
    return route.fulfill({status:404,body:""});
  });
  const page=await context.newPage(), errors=[];
  page.on("pageerror",error=>errors.push(error.message));
  page.on("framenavigated",frame=>{if(frame===page.mainFrame())navigations++;});
  await page.goto(origin+"/fixture/");
  const panel=page.locator("#bl-release-check"), status=panel.getByRole("status",{includeHidden:true});
  const check=panel.getByRole("button",{name:"Check for updates"});
  const dismiss=panel.getByRole("button",{name:"Remind me later"});
  await panel.waitFor();
  await page.waitForFunction(()=>!document.querySelector("#bl-release-check button").disabled);
  assert.ok(calls>=1,"Automatic check on load");
  assert.equal(await status.isVisible(),false,"No update prompt when current");
  await page.getByRole("textbox",{name:"Unsaved draft"}).fill("Do not lose my unsaved draft");
  manifest.webRevision=next;
  await check.click();
  await status.waitFor({state:"visible"});
  assert.match(await status.textContent(),/Update available/);
  assert.equal(await dismiss.isVisible(),true);
  assert.equal(await page.getByRole("textbox").inputValue(),"Do not lose my unsaved draft");
  assert.equal(navigations,1,"No automatic refresh");
  const bounds=await panel.boundingBox();
  assert.ok(bounds.x>=0&&bounds.x+bounds.width<=viewport.width+1,"Fits viewport");
  await dismiss.click();
  await status.waitFor({state:"hidden"});
  const before=calls;
  await page.evaluate(()=>window.dispatchEvent(new Event("online")));
  await page.waitForFunction(()=>!document.querySelector("#bl-release-check button").disabled);
  assert.ok(calls>before,"Recheck when online");
  assert.equal(await status.isVisible(),false,"Dismissed revision stays dismissed");
  manifest.webRevision=third;
  await page.evaluate(()=>window.dispatchEvent(new Event("online")));
  await status.waitFor({state:"visible"});
  assert.match(await status.textContent(),/Update available/);
  await dismiss.click();
  fail=true;
  await check.click();
  await status.waitFor({state:"visible"});
  assert.match(await status.textContent(),/Could not check/);
  assert.equal(await page.getByRole("textbox").inputValue(),"Do not lose my unsaved draft");
  fail=false;manifest={schema:1,channel:"production",webRevision:third};
  await check.click();
  await page.waitForFunction(()=>!document.querySelector("#bl-release-check button").disabled);
  assert.match(await status.textContent(),/Could not check/);
  assert.equal(navigations,1);
  assert.deepEqual(errors,[]);
  console.log("PASS",viewport.width,"startup, update, dismiss, new revision, offline, wrong channel, unsaved draft and no reload");
  await context.close();
 }
}finally{await browser.close();}
