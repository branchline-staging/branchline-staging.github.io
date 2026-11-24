import os, pathlib, shutil, hashlib, json, re
from pathlib import Path
root=Path(__file__).resolve().parents[1]
import subprocess
subprocess.run(["node","--test",str(root/"staging/tests/update-center.test.cjs")],check=True)
parent=root.parent
out=root/"staging-site-dist"
stage="rlfdvmiqkbitetatxkju"
key=os.environ["STAGING_SUPABASE_PUBLISHABLE_KEY"]
assert os.environ.get("BRANCHLINE_ISOLATED_STAGE")=="1"
assert key.startswith("sb_publishable_")
assert os.environ.get("GITHUB_REPOSITORY","branchline-staging/branchline-staging.github.io")=="branchline-staging/branchline-staging.github.io"
shutil.rmtree(out,ignore_errors=True);out.mkdir()
for name in ("index.html",".nojekyll"):shutil.copy2(root/name,out/name)
original=(root/"index.html").read_bytes()
assert b"Branchline staging sign-in" in original and b"rlfdvmiqkbitetatxkju" in original
assert (out/"index.html").read_bytes()==original
assert b"Open unified PTG sign-in" in original, "Official PTG entry is missing"
src=parent/"branchline"; dest=out/"sales-tracker";dest.mkdir()
allowed={".html",".js",".mjs",".css",".json",".webmanifest",".svg",".png",".jpg",".jpeg",".webp",".ico",".woff2",".wasm"}
for file in src.iterdir():
 if file.is_dir():
  if file.name in ("apps","icons","import-engine"):shutil.copytree(file,dest/file.name)
 elif file.suffix in allowed and not re.search(r"(^|[-_.])(test|spec)[.]",file.name):
  shutil.copy2(file,dest/file.name)
p=dest/"supabase-config.js";s=p.read_text()
assert "psoopubvdpwztzkdyifa" in s and "sb_publishable_e_6K64Wul2fJEq2KiuvnTA_NmuaKqNm" in s
p.write_text(s.replace("psoopubvdpwztzkdyifa",stage).replace("sb_publishable_e_6K64Wul2fJEq2KiuvnTA_NmuaKqNm",key))
for file in ("branchline-applications.js","apps/index.html"):
 p=dest/file;s=p.read_text();assert "kzamora19.github.io" in s
 p.write_text(s.replace("kzamora19.github.io","branchline-staging.github.io"))
p=dest/"index.html";p.write_text(p.read_text().replace("Branchline — Sales workspace","Branchline — Sales Tracker staging"))
# All absolute links in the staged Sales Tracker copy must stay on the
# separate staging origin; normalize former /branchline/ links as /sales-tracker/.
for item in dest.rglob("*"):
 if not item.is_file() or item.suffix not in (".html",".js",".mjs",".css",".json",".webmanifest",".svg"):continue
 body=item.read_text(errors="replace")
 revised=body.replace("https://kzamora19.github.io/branchline/","https://branchline-staging.github.io/sales-tracker/").replace("https://kzamora19.github.io","https://branchline-staging.github.io")
 for project_id in ("psoopubvdpwztzkdyifa","agknxlowackphvwyvxge","vmyoxoiygfcecgkbavgg","tzklsscoiwnmeantcefw"):revised=revised.replace(project_id,stage)
 if revised!=body:item.write_text(revised)
assert "branchline-staging.github.io/first-megasaver-ptg/studios/" in (dest/"branchline-applications.js").read_text()
# Both staging apps use the same Auth project. Keep the registered OAuth
# client intact, and serve a local fallback instead of a self-issued consent flow.
shutil.copy2(root/"staging/oauth-consent.html",dest/"oauth-consent.html")
ptg=parent/"first-megasaver-ptg"/"out"
assert (ptg/"studios/index.html").exists()
shutil.copytree(ptg,out/"first-megasaver-ptg")
# Never redistribute old unsigned Windows installers through the staging test site.
for item in (out/"first-megasaver-ptg").rglob("*"):
 if item.is_file() and item.suffix.lower() in (".exe",".msi",".msix",".appx"):
  item.unlink()
# Compiled PTG chunks can also contain original app-directory origin constants.
# Rewrite them in the staging output ONLY, preserving fail-closed role gates.
for item in (out/"first-megasaver-ptg").rglob("*"):
 if not item.is_file() or item.suffix not in (".html",".js",".mjs",".css",".json",".svg",".webmanifest"):continue
 body=item.read_text(errors="replace")
 fixed=body.replace("https://kzamora19.github.io/branchline/","https://branchline-staging.github.io/sales-tracker/").replace("https://kzamora19.github.io","https://branchline-staging.github.io")
 for prod_id in ("psoopubvdpwztzkdyifa","agknxlowackphvwyvxge","vmyoxoiygfcecgkbavgg","tzklsscoiwnmeantcefw"):fixed=fixed.replace(prod_id,stage)
 for prod_key in ("sb_publishable_e_6K64Wul2fJEq2KiuvnTA_NmuaKqNm","sb_publishable_kv2q3dnNQt4ij0mgkOLbQQ_S2MO9FvG"):fixed=fixed.replace(prod_key,key)
 if fixed!=body:item.write_text(fixed)
assert (out/"first-megasaver-ptg/icons/official/data-desk.png").exists()
for app,legacy in (("megaframe-studio","vmyoxoiygfcecgkbavgg"),("flyer-studio","tzklsscoiwnmeantcefw")):
 origin=parent/app/"staging/stage-pages-cutover/studio"
 assert (origin/"index.html").exists()
 target=out/app/"studio";shutil.copytree(origin,target)
 p=target/"index.html"
 s=p.read_text().replace(legacy,stage).replace("kzamora19.github.io","branchline-staging.github.io")
 assert stage+".supabase.co/functions/v1/" in s and "Branchline applications" in s
 assert key in s
 assert "position:sticky;top:0" in s, "Missing sticky mobile logo on "+app
 assert not re.search(r'<canvas|PRIVATE (MEGAFRAME )?EDITOR BUNDLE|id="photosInput"|id="quickSaveProject"',s,re.I)
 p.write_text(s)
 # An expired or denied Studio ticket must return to the STAGING PTG portal,
 # never send a reviewer to production sign-in.
 for javascript in target.glob("*.js"):
  original=javascript.read_text()
  adjusted=original.replace("https://kzamora19.github.io/first-megasaver-ptg/studios/","https://branchline-staging.github.io/first-megasaver-ptg/studios/")
  if adjusted!=original:javascript.write_text(adjusted)
 if app=="flyer-studio":
  # The isolated project has no photo gateway. Deny only that feature and
  # leave the signed editor session and encrypted backup operations intact.
  launcher=target/"launch.js"
  js=launcher.read_text()
  photo_start=js.index("   if(message.action==='product-photo'){")
  photo_end=js.index("   if(!['read','write','delete']",photo_start)
  assert "flyer-product-photos" in js[photo_start:photo_end]
  launcher.write_text(js[:photo_start]+"   if(message.action==='product-photo'){\n    channel.port1.postMessage({id:message.id,ok:false,\n      error:'The product photo library is unavailable in staging.'});\n    return;\n   }\n"+js[photo_end:])
  page=p.read_text()
  assert 'src="./launch.js"' in page
  p.write_text(page.replace('src="./launch.js"','src="./launch.js?v=isolated-photo-v2"',1))

# R6 single-installation directive: do not change existing manifest identities
# until the actual installed browser identities have been inventoried.
# Cache Storage is origin-wide, not scoped automatically by service workers.
ptg_worker=out/"first-megasaver-ptg/sw.js"
ptg_text=ptg_worker.read_text()
old='keys.filter((key) => key !== CACHE_NAME)'
assert old in ptg_text,"PTG cache cleanup changed; review before release"
ptg_text=ptg_text.replace(old,'keys.filter((key) => key.startsWith("first-megasaver-ptg-shell-") && key !== CACHE_NAME)')
ptg_text,n_ptg=re.subn(r'const CACHE_NAME = "first-megasaver-ptg-shell-v[^"]+";', 'const CACHE_NAME = "first-megasaver-ptg-shell-v3-r6-isolation";',ptg_text,count=1)
assert n_ptg==1
# Claim only after the app's own cache cleanup has completed.
ptg_text=ptg_text.replace('Promise.all(keys.filter((key) => key.startsWith("first-megasaver-ptg-shell-") && key !== CACHE_NAME).map((key) => caches.delete(key)))\n    )\n  );\n  self.clients.claim();','Promise.all(keys.filter((key) => key.startsWith("first-megasaver-ptg-shell-") && key !== CACHE_NAME).map((key) => caches.delete(key)))\n    ).then(() => self.clients.claim())\n  );')
assert '.then(() => self.clients.claim())' in ptg_text
ptg_worker.write_text(ptg_text)
sales_worker=out/"sales-tracker/sw.js"
sales_text=sales_worker.read_text()
assert 'key.startsWith("branchline-")' in sales_text
sales_text=sales_text.replace('key.startsWith("branchline-")','key.startsWith("branchline-shell-")')
assert 'await caches.match(request)' in sales_text and 'await caches.match(new URL("./index.html", self.registration.scope).href)' in sales_text
sales_text=sales_text.replace('await caches.match(request)','await (await caches.open(CACHE_NAME)).match(request)').replace('await caches.match(new URL("./index.html", self.registration.scope).href)','await (await caches.open(CACHE_NAME)).match(new URL("./index.html", self.registration.scope).href)')
sales_text,n_sales=re.subn(r'const CACHE_NAME = "branchline-shell-v[^"]+";', 'const CACHE_NAME = "branchline-shell-v200-r6-isolation";',sales_text,count=1)
assert n_sales==1
sales_worker.write_text(sales_text)
# The old staging build copied an unsigned Windows archive from PTG public/.
# Remove the known legacy installer rather than offering a parallel install.
legacy_zip=out/"first-megasaver-ptg/branchline-workspace-windows.zip"
if legacy_zip.exists():legacy_zip.unlink()
subprocess.run(["node",str(root/"staging/tests/cache-isolation.test.cjs"),str(out)],check=True)

# Web release notifications: same-origin staging-only manifest, never native installer.
# Hash the assembled web assets before injecting the notification script, so a
# page identifies its own release independently from the latest manifest.
release_hash=hashlib.sha256()
for item in sorted(out.rglob("*")):
 if item.is_file() and item.suffix.lower() in allowed:
  release_hash.update(str(item.relative_to(out)).encode("utf-8")+b"\0")
  release_hash.update(hashlib.sha256(item.read_bytes()).digest())
for name in ("check.js","check.css"):
 release_hash.update(("staging/updates/"+name).encode("utf-8")+b"\0")
 release_hash.update(hashlib.sha256((root/"staging"/"updates"/name).read_bytes()).digest())
revision=release_hash.hexdigest()
update_dir=out/"sales-tracker"/"updates"
update_dir.mkdir(exist_ok=True)
for name in ("check.js","check.css"):
 shutil.copy2(root/"staging"/"updates"/name,update_dir/name)
(update_dir/"release.json").write_text(json.dumps({"schema":1,"channel":"staging","webRevision":revision},indent=2))
for app in ("sales-tracker","first-megasaver-ptg","megaframe-studio","flyer-studio"):
 for page in (out/app).rglob("*.html"):
  body=page.read_text()
  if "</head>" not in body or "</body>" not in body:continue
  # Do not insert update UI into the offline notice, login or OAuth callbacks.
  if re.search(r"(^|/)(login|offline|oauth|callback|auth)([/. -]|$)",str(page.relative_to(out)),re.I):continue
  injection='<link rel="stylesheet" href="/sales-tracker/updates/check.css"><script defer src="/sales-tracker/updates/check.js" data-revision="'+revision+'"></script>'
  assert "data-revision="+chr(34)+revision not in body
  page.write_text(body.replace("</head>",injection+"</head>",1))

forbidden=["psoopubvdpwztzkdyifa","agknxlowackphvwyvxge","vmyoxoiygfcecgkbavgg","tzklsscoiwnmeantcefw","https://kzamora19.github.io","https://first-megasaver-ptg.vercel.app"]
n=0
for p in out.rglob("*"):
 if not p.is_file():continue
 n+=1
 if p.suffix not in (".html",".js",".mjs",".css",".json",".svg",".webmanifest"):continue
 data=p.read_text(errors="replace")
 for value in forbidden:assert value not in data, f"Production reference {value} in {p.relative_to(out)}"
paths=("index.html","sales-tracker/index.html","first-megasaver-ptg/studios/index.html","megaframe-studio/studio/index.html","flyer-studio/studio/index.html")
digest={p:hashlib.sha256((out/p).read_bytes()).hexdigest() for p in paths}
(out/"STAGING-RELEASE-MANIFEST.json").write_text(json.dumps({"host":"branchline-staging.github.io","supabase":stage,"files":n,"rootPreserved":True,"privateEditorsPublished":False,"sha256":digest},indent=2))
print("STAGING BUILD COMPLETE",n,digest)
