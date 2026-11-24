/* Staging-only, cross-platform web release notifications. No native downloads. */
(function(root){
  "use strict";
  const VERSION=/^[a-f0-9]{64}$/;
  function inspect(manifest, running){
    if(!VERSION.test(running)||!manifest||manifest.schema!==1||
       manifest.channel!=="staging"||!VERSION.test(manifest.webRevision||"")) return "invalid";
    return manifest.webRevision===running?"current":"available";
  }
  if(typeof module==="object"&&module.exports){module.exports={inspect};return;}
  const doc=root.document, script=doc.currentScript;
  if(root.location.origin!=="https://branchline-staging.github.io"||!script)return;
  const running=script.dataset.revision;
  if(!VERSION.test(running||""))return;
  function start(){
    if(doc.getElementById("bl-release-check"))return;
    const box=doc.createElement("section");box.id="bl-release-check";
    box.setAttribute("aria-label","Branchline staging software updates");
    box.className="bl-release-check";
    const status=doc.createElement("span");status.setAttribute("role","status");
    status.setAttribute("aria-live","polite");status.hidden=true;
    const check=doc.createElement("button");check.type="button";check.textContent="Check for updates";
    const dismiss=doc.createElement("button");dismiss.type="button";dismiss.textContent="Remind me later";dismiss.hidden=true;
    box.append(status,check,dismiss);doc.body.append(box);
    let checking=false, available=null, dismissed=null;
    async function refresh(manual){
      if(checking||(!manual&&doc.visibilityState==="hidden"))return;
      checking=true;check.disabled=true;
      try{
        const response=await root.fetch("/sales-tracker/updates/release.json",{cache:"no-store",credentials:"omit",redirect:"error"});
        if(!response.ok)throw Error("Release request failed");
        const manifest=await response.json();
        const result=inspect(manifest,running);
        if(result==="invalid")throw Error("Invalid release");
        if(result==="available"){
          available=manifest.webRevision;
          if(manual||dismissed!==available){
            status.textContent="Update available. Save your work, then close and reopen Branchline from your usual workspace to load the new version.";
            status.hidden=false;dismiss.hidden=false;box.classList.add("bl-update-available");
          }
        }else{
          available=null;dismissed=null;dismiss.hidden=true;box.classList.remove("bl-update-available");
          status.hidden=!manual;if(manual)status.textContent="This web workspace is up to date.";
        }
      }catch{
        if(manual){status.hidden=false;status.textContent="Could not check for updates. Try again when connected.";}
      }finally{checking=false;check.disabled=false;}
    }
    check.addEventListener("click",()=>refresh(true));
    dismiss.addEventListener("click",()=>{dismissed=available;status.hidden=true;dismiss.hidden=true;box.classList.remove("bl-update-available");});
    root.addEventListener("online",()=>refresh(false));
    doc.addEventListener("visibilitychange",()=>{if(doc.visibilityState==="visible")refresh(false);});
    root.setInterval(()=>refresh(false),15*60*1000);
    refresh(false);
  }
  if(doc.readyState==="complete")start();else root.addEventListener("load",start,{once:true});
})(typeof window!=="undefined"?window:globalThis);
