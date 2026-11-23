import os, pathlib, shutil, hashlib, json, re
from pathlib import Path
root=Path(__file__).resolve().parents[1]
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
