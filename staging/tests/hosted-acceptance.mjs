import assert from "node:assert/strict";
import {mkdirSync,writeFileSync,readFileSync} from "node:fs";
import {join} from "node:path";
import {chromium} from "playwright";

const origin="https://branchline-staging.github.io";
const folder="artifacts/hosted-acceptance";
mkdirSync(folder,{recursive:true});
const report={origin,date:new Date().toISOString(),mode:"Real hosted Pages + unauthenticated gate + demo printing + mocked encrypted Drive UI",checks:[],limitations:[
 "No staging login credentials were supplied; real account login, role session, issue/redeem ticket, private editor operations and account logout NOT proven.",
 "Google OAuth owner grant, actual company Google Drive read/write and restore NOT proven: synthetic records only.",
 "No physical printer tested: browser print handler and generated PDF are not hardware acceptance.",
 "Never entered real credentials, accessed customer data, or wrote Supabase/Drive records."
]};
const failures=[];
const pageErrors=[];
async function check(name,fn,{required=true}={}){
 const start=Date.now();
 try{
   const detail=await fn();
   const entry={name,status:"PASS",ms:Date.now()-start,detail:detail??null};
   report.checks.push(entry);console.log("PASS",name,detail??"");
 }catch(error){
   const reason=String(error?.stack||error).slice(0,2000);
   report.checks.push({name,status:"FAIL",ms:Date.now()-start,reason,required});
   if(required)failures.push({name,reason});
   const diag={url:page?.url()||"",title:await page?.title().catch(()=>""),body:(await page?.locator("body").innerText().catch(()=>"")).slice(0,1800),
     errors:pageErrors.slice(-8)};
   console.error("FAIL",name,reason,"DIAG",JSON.stringify(diag));
   try{await page?.screenshot({path:join(folder,"DIAG-"+name.replace(/[^A-Za-z0-9]+/g,"-").slice(0,48)+".png"),fullPage:true});}catch{}
   report.checks[report.checks.length-1].diagnostic=diag;
 }
}
function saveReport(){
 report.requiredFailures=failures;
 writeFileSync(join(folder,"acceptance-report.json"),JSON.stringify(report,null,2));
 writeFileSync(join(folder,"acceptance-summary.txt"),report.checks.map(c=>c.status+" "+c.name+(c.reason?" :: "+c.reason:"")).join("\n")+
 "\n\n"+report.limitations.join("\n")+"\n");
}
const browser=await chromium.launch({headless:true,args:["--disable-features=TranslateUI"]});
const context=await browser.newContext({viewport:{width:1440,height:900},acceptDownloads:true,ignoreHTTPSErrors:false});
const page=await context.newPage();
page.on("pageerror",err=>pageErrors.push(err.message));
page.setDefaultTimeout(13000);
const productionRequests=[];
context.on("request",req=>{
 if(/^https:\/\/(?:kzamora19[.]github[.]io|.*[.]vercel[.]app)(?:\/|$)/.test(req.url()))
  productionRequests.push(req.url().slice(0,200));
});
async function goto(path){
 const r=await page.goto(origin+path,{waitUntil:"domcontentloaded",timeout:40000});
 assert.equal(r?.status(),200,"HTTP status "+r?.status()+" for "+path);
 return r;
}
async function screenshot(label){await page.screenshot({path:join(folder,label+".png"),fullPage:true,animations:"disabled"});}
async function studio(studio){
 const path="/"+studio+"-studio/studio/";
 await goto(path);
 await page.locator(".bl-protected-app-rail").waitFor({state:"visible"});
 const roleLinks=await page.locator(".bl-protected-app-list-link").allTextContents();
 assert.deepEqual(roleLinks,["Price Tag Generator","Sales Tracker","MegaFrame Studio","MegaFlyer Studio"]);
 assert.equal(await page.locator("iframe#studio-editor-frame").count(),0,"No private iframe without ticket");
 const attrs=await page.locator(".bl-protected-app-list-link").evaluateAll(nodes=>
    nodes.map(n=>n.getAttribute("href")));
 for(const href of attrs)assert.ok(href?.startsWith(origin+"/")||href===origin+"/sales-tracker/",
   "Cross-origin staging app link: "+href);
 assert.equal(await page.locator("#drivePanel").isVisible(),false);
 assert.equal(await page.locator("#cloudPanel").isVisible(),false);
 await screenshot(studio+"-desktop");
 await page.locator(".bl-protected-brand").click();
 assert.equal(await page.locator("#bl-protected-app-drawer").isVisible(),true);
 await screenshot(studio+"-desktop-drawer");
 await page.keyboard.press("Escape");
 assert.equal(await page.locator("#bl-protected-app-drawer").isVisible(),false);
 await page.setViewportSize({width:390,height:844});
 const mobile=page.locator(".bl-protected-mobile-brand");
 await mobile.waitFor({state:"visible"});
 await mobile.click();
 assert.equal(await page.locator("#bl-protected-app-drawer").isVisible(),true);
 await screenshot(studio+"-mobile-drawer");
 await mobile.click();
 assert.equal(await page.locator("#bl-protected-app-drawer").isVisible(),false);
 assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+3),
   "No horizontal overflow at mobile width");
 await page.setViewportSize({width:1440,height:900});
 return {fourAppLinks:roleLinks,privateEditorAvailableWithoutTicket:false};
}
try{
 await check("Staging HTTPS root sign-in loads with blank credentials",async()=>{
  await goto("/");
  await page.getByRole("heading",{name:/Branchline staging sign-in/i}).waitFor();
  assert.equal(await page.locator("input[name='password']").inputValue(),"");
  assert.equal(await page.locator("#studios").isVisible(),false);
  await screenshot("staging-root-signin");
  return {scheme:new URL(page.url()).protocol,passwordEntered:false};
 });
 await check("Staged Sales Tracker app rail, independent workspace menu",async()=>{
  await goto("/sales-tracker/");
  await page.locator("#enter-demo").waitFor({state:"visible"});
  await page.locator("#enter-demo").click();
  await page.locator("#connected-shell .bl-app-rail").waitFor({state:"visible",timeout:20000});
  assert.equal(await page.locator("#connected-shell .bl-rail-link").count(),4);
  const logo=page.locator("#connected-shell .bl-rail-brand");
  await logo.click();
  assert.equal(await page.locator("#bl-app-drawer-connected").isVisible(),true);
  await screenshot("sales-tracker-desktop-drawer");
  await page.keyboard.press("Escape");
  assert.equal(await page.locator("#bl-app-drawer-connected").isVisible(),false);
  await page.setViewportSize({width:390,height:844});
  await page.locator("#connected-shell .bl-mobile-brand").click();
  assert.equal(await page.locator("#bl-app-drawer-connected").isVisible(),true);
  await screenshot("sales-tracker-mobile-drawer");
  await page.locator("#connected-menu").click();
  assert.equal(await page.locator("#bl-app-drawer-connected").isVisible(),false);
  await page.setViewportSize({width:1440,height:900});
  return {apps:4,workspaceHamburgerSeparate:true};
 });
 await check("Hosted PTG login is reachable",async()=>{
  await goto("/first-megasaver-ptg/login/");
  assert.ok((await page.locator("body").innerText()).match(/sign in|username|password|price tag/i));
  await screenshot("ptg-login-no-credentials");
  return {passwordEntered:false};
 });
 await check("Hosted PTG Creative Studios denies unauthenticated entry",async()=>{
  await goto("/first-megasaver-ptg/studios/");
  await page.waitForFunction(()=>location.pathname.includes("/login/")||
    /not authorized|Sign in to PTG|PTG Team Leader|unavailable/i.test(document.body?.innerText||""),{timeout:25000});
  const text=(await page.locator("body").innerText()).slice(0,1100);
  assert.equal(await page.getByRole("button",{name:/^Open MegaFrame Studio$|^Open MegaFlyer Studio$/}).count(),0);
  await screenshot("ptg-studios-unauthorized");
  return {denied:true,summary:text.slice(0,200)};
 });
 await check("Hosted MegaFrame protected launch without ticket and mobile navigation",async()=>studio("megaframe"));
 await check("Hosted MegaFlyer protected launch without ticket and mobile navigation",async()=>studio("flyer"));
 await check("PTG Demo actual price tags -> browser Letter PDF and app Print handler",async()=>{
  await goto("/first-megasaver-ptg/demo/");
  await page.locator("#legacy-product-search").waitFor({state:"visible",timeout:20000});
  await page.locator(".legacy-search-results-table tbody tr").filter({hasText:"DEMO-0001"}).first().waitFor({state:"visible"});
  await page.locator("#legacy-product-search").fill("DEMO-RC18");
  console.log("DEMO PRE SEARCH", (await page.locator("body").innerText()).slice(0,700));
  await page.locator(".legacy-search-form button[type='submit']").click();
  await page.locator(".legacy-search-results-table tbody tr").filter({hasText:"DEMO-0006"}).first().waitFor();
  const row=page.locator(".legacy-search-results-table tbody tr").filter({hasText:"DEMO-0006"}).first();
  await row.getByRole("checkbox").check();
  await page.locator(".ptg-print-preview .legacy-print-sheet").first().waitFor({state:"attached"});
  assert.match(await page.locator(".ptg-print-preview").innerText(),/DEMO-RC18|DEMO-0006/);
  await page.getByRole("button",{name:"2 X 2",exact:true}).click();
  const printSheetCount=await page.locator(".ptg-print-preview .legacy-print-sheet").count();
  assert.ok(printSheetCount>=1);
  await screenshot("ptg-demo-price-tag-preview");
  const pdf=await page.pdf({format:"Letter",printBackground:true,preferCSSPageSize:true});
  assert.ok(pdf.subarray(0,5).toString()==="%PDF-","Not a PDF");
  assert.ok(pdf.length>6000,"Empty print PDF");
  writeFileSync(join(folder,"ptg-demo-browser-print.pdf"),pdf);
  await page.evaluate(()=>{window.__qaPrintCalled=false;window.print=()=>{window.__qaPrintCalled=true;};});
  await page.locator(".print-tags").click();
  assert.equal(await page.evaluate(()=>window.__qaPrintCalled),true,"Actual print control must call window.print()");
  return {format:"2 X 2",sheets:printSheetCount,pdfBytes:pdf.length,printerDialogInvoked:true};
 });
 await check("PTG Demo native SAVE PDF download",async()=>{
   await goto("/first-megasaver-ptg/demo/");
   await page.locator("#legacy-product-search").waitFor({state:"visible",timeout:20000});
   await page.locator(".legacy-search-results-table tbody tr").filter({hasText:"DEMO-0001"}).first().waitFor({state:"visible"});
   await page.locator("#legacy-product-search").fill("DEMO-FAN16");
   await page.locator(".legacy-search-form button[type='submit']").click();
   const row=page.locator(".legacy-search-results-table tbody tr").filter({hasText:"DEMO-0007"}).first();
   await row.waitFor({state:"visible"});
   await row.getByRole("checkbox").check();
   const waitDownload=page.waitForEvent("download",{timeout:30000});
   await page.locator(".save-pdf").click();
   const download=await waitDownload;
   const path=join(folder,"ptg-demo-app-save.pdf");
   await download.saveAs(path);
   const data=readFileSync(path);
   assert.ok(data.subarray(0,5).toString()==="%PDF-","SAVE output is not PDF");
   assert.ok(data.length>5000,"PDF output unexpectedly empty");
   return {file:download.suggestedFilename(),bytes:data.length};
 },{required:false});
 await check("Actual hosted encrypted Drive UI: simulated write/read/restore, no Google credentials",async()=>{
  const summary=[];
  for(const kind of ["megaframe","flyer"]){
   await goto("/"+kind+"-studio/studio/");
   await page.waitForFunction(()=>!!window.BRANCHLINE_CENTRAL_DRIVE,{timeout:10000});
   const init=await page.evaluate(async kind=>{
    const calls=[],imports=[];
    window.confirm=()=>true;
    const record={iv:"staging-only-iv",encrypted:"staging-only-ciphertext",version:1};
    const vault=kind==="megaframe"?{
      "project-list":async()=>[{id:"qa-project",name:"Synthetic Test Project"}],
      "cloud-export":async key=>{calls.push("export:"+key);return record;},
      "project-read":async()=>null,
      "cloud-import":async(key,r)=>{calls.push("import:"+key);imports.push(r);}
    }:{
      cloudExport:async key=>{calls.push("export:"+key);return record;},
      read:async()=>null,
      cloudImport:async(key,r)=>{calls.push("import:"+key);imports.push(r);}
    };
    const request=async(action,fields)=>{
      calls.push(action);
      if(action==="list")return {status:"ok",items:[{fileId:"synthetic-file",recordKey:kind==="megaframe"?"qa-project":"p:current",modifiedTime:"2026-11-23T00:00:00Z"}]};
      if(action==="write"){
        if(fields.encrypted!==record.encrypted||fields.iv!==record.iv)throw Error("Backup encryption payload altered");
        return {status:"ok"};
      }
      if(action==="read")return {status:"ok",record};
      throw Error("Unexpected request "+action);
    };
    await window.BRANCHLINE_CENTRAL_DRIVE.setup({studio:kind,vault,request});
    window.__qaDriveState={calls,imports};
    return {drivePanelVisible:!document.querySelector("#drivePanel").hidden};
   },kind);
   assert.equal(init.drivePanelVisible,true);
   await page.locator("#driveRefresh").click();
   await page.waitForFunction(()=>window.__qaDriveState.calls.includes("list"));
   await page.locator("#driveBackup").click();
   await page.waitForFunction(()=>window.__qaDriveState.calls.includes("write"),null,{timeout:10000});
   await page.locator("#driveRestore").click();
   await page.waitForFunction(()=>window.__qaDriveState.calls.some(x=>x.startsWith("import:")),null,{timeout:10000});
   const output=await page.evaluate(()=>({calls:window.__qaDriveState.calls,imports:window.__qaDriveState.imports,message:document.querySelector("#driveMessage").textContent}));
   assert.equal(output.imports.length,1);
   assert.equal(output.imports[0].encrypted,"staging-only-ciphertext");
   assert.match(output.message,/retrieved/i);
   await screenshot(kind+"-drive-synthetic-restore");
   summary.push({studio:kind,operations:output.calls});
   await page.evaluate(()=>window.BRANCHLINE_CENTRAL_DRIVE.lock());
   assert.equal(await page.locator("#drivePanel").isVisible(),false);
  }
  return {simulatedOnly:true,studios:summary};
 });
 await check("Browser requested no production application URLs",async()=>{
  assert.deepEqual(productionRequests,[]);
  return {productionRequests:productionRequests.length};
 });
}finally{
 saveReport();
 await browser.close();
}
if(failures.length){console.error(JSON.stringify({criticalFailures:failures},null,2));process.exitCode=1;}
else console.log("Hosted pre-production gates PASS. Real credentials/OAuth/physical device limitations remain.");
