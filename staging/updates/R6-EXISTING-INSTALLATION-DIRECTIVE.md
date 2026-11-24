# Navigation R6 — existing installation and automatic update directive

Approved continuation, 2026-11-24. Retain merged PRs #4–6 and their test evidence; do not restart. R6 follow-up is the native baseline. The R6 role-correction pilot remains unverified on an actual device. The separate native companion is a test component, NOT a second official app; final distribution must integrate it with the original installation/shortcut and identity.

## Source audit (production repositories, not device inventory)
| Source | Manifest path | id (as written) | start_url | scope |
|---|---|---|---|---|
| kzamora19/branchline main | manifest.webmanifest | ./ | ./apps/ | ./ |
| kzamora19/first-megasaver-ptg migration/github-pages | public/manifest.webmanifest | /first-megasaver-ptg/ | /first-megasaver-ptg/login/ | /first-megasaver-ptg/ |
| same PTG branch | public/workspace-mobile.webmanifest | ./workspace/mobile/ | ./workspace/mobile/ | ./workspace/mobile/ |
| kzamora19/megaframe-studio main | manifest.webmanifest | ./ | ./ | ./ |
| kzamora19/flyer-studio main | site/manifest.webmanifest | ./ | ./ | ./ |

All four production GitHub Pages paths use the same origin. Relative manifest IDs resolve against the start URL's origin, not its containing directory. The three literal "./" IDs therefore risk colliding at the origin root. The mobile Workspace ID differs. Verify installed IDs and start URLs on actual devices (Chrome/Edge web app internals, Windows shortcut target, native install path and hash). Do not change IDs, origins or scopes blindly; that may create another installed app. See https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Manifest/Reference/id and https://developer.chrome.com/docs/capabilities/pwa-manifest-id . Browser-installed and native Windows packages cannot be automatically merged by a web manifest. Existing native packages without a trusted remote updater need a one-time in-place bootstrap or managed distribution, not a separate permanent launcher or repeated ZIP releases. Existing duplicate shortcuts/installations must be individually identified before consolidation; never uninstall or delete a WebView2 profile automatically.

## New work from the directive
- Fix origin-wide cache deletion found in production PTG service worker, and restrict Sales Tracker cleanup and fallback lookup to its own shell. Staging assembly patch and fixture test; production source changes still required.
- Preserve installed PWA IDs; verify actual installed identities, choose canonical identity for future installs and provide legacy entry routes. Disable redundant install prompts only after verified migration and supported browser behavior. Do not widen scopes across unrelated Pages paths or change authentication.
- Release labels visible in Account/Settings/About, one central production release manifest mapping compatible web/native releases; retain signed native release sequence and checksums.
- Replace temporary staging notification with approved Update & Restart / Later UX, using existing editor save flows and fresh protected Studio entry tickets; do not blindly reload unsaved or ticket-protected pages.
- Establish versioned coherent app-shell assets, safe waiting/activation and offline-to-online behavior; test multiple tabs and cache isolation, avoid forced reload and update loops. Current cache isolation change alone does not prove atomic shell upgrades.
- Integrate updater into the original Windows installation, preserve existing shortcut target and WebView2 profile. Existing device pilot, trusted owner signing identity, full production app acceptance and verified production rollout remain gates. No separate companion shortcut as final product.
- Preserve all four current app features and original production/staging databases, roles, authentication, and user data. No new Supabase project.

Acceptance requires a real existing-install upgrade, remote subsequent update, no separate R6 installation, four-app functional checks, role correction, offline/online detection, save-safe activation, coherent caches, rollback, and production verification. Do not equate staging or a ZIP candidate with completion.
