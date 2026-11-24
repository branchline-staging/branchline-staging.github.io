# Branchline Cross-Platform Update Center — active project

**Priority:** Continue this project only; no unrelated app features or production database changes.
**Checkpoint:** 2026-11-24. Staging PR #4, branch `feature/cross-platform-update-center-20261124`.

## Completed
- Initial web/PWA checker: startup, online/visibility, 15-minute polling, manual check, dismiss; isolated staging manifest, no forced reload. PR build passed: https://github.com/branchline-staging/branchline-staging.github.io/actions/runs/45307399048 (commit e9772e10d5b724fec18d5ca5ccdefcb8c1c65f93). That run did NOT publish Pages. Update checker assets now contribute to release identity; new CI required.
- Located actual legacy launcher source: https://github.com/kzamora19/first-megasaver-ptg/tree/fix/windows-installed-navigation-20261123/windows/production (R2 source; not current R6). Alternate `feature/workspace-installer-source-20261123` is a preview implementation, not a drop-in replacement for installed users.
- Identified nine legacy checksums in the read-only detector. Original/R1/R2 documented in R2 source; R4/R5 in R5 README; additional legacy digest is found in the R5 updater, revision label unverified. Actual R5, R5 border hotfix, R6 candidate and follow-up payloads were extracted from the user-provided/delivered updater archives at offset 10732, length 991232; SHA-256 and ZIP integrity checked. These are recognized candidates, not certification of runtime behavior.
- Added native verification module, read-only detector and Windows CI tests. No download/execution/install code connected to the launcher yet. A fresh signing key must be established securely; test RSA keys are temporary, never production trust anchors.

## Latest verification
- Web assembly at be0e1e930b passed: https://github.com/branchline-staging/branchline-staging.github.io/actions/runs/45307803101. No Pages publish.
- Native Windows policy CI initially failed due to forward-slash C# source paths; fixed at 70180482dd. All 19 checks passed: https://github.com/branchline-staging/branchline-staging.github.io/actions/runs/45308766235. No actual launcher was installed or replaced.
- Added desktop/mobile Chromium fixture at 86058d0945 to exercise startup, manual check, dismissal, new release, offline handling, wrong channel and unsaved edits without navigation. CI run https://github.com/branchline-staging/branchline-staging.github.io/actions/runs/45308815058 is pending/in progress at this checkpoint; verify its conclusion before claiming success.

## Outstanding work (in this order)
1. Verify latest web/native CI and the new browser fixture run; fix any failures. Review staging before merging. Validate real mobile/desktop PWAs and protected editor access after staging publish.
2. Recover reproducible **current R6** source and all exact assets, or reconstruct with verified parity and test. Never replace R6 with older R2 code. Outstanding R6 header role: get authenticated app-reported role through trusted same-origin UI; hide unresolved line, no inferred role or permission change. Retain all four app icons, account border fix and MegaFlyer/MegaFrame layout parity.
3. Establish owner-controlled signing identity and separate pinned staging/production keys, authenticated release approval, monotonic sequence and release channel. Implement native check + notification in actual launcher, download with redirects disabled, checksum verify and secure helper after save/close; backup and rollback. Require unique recognized installation for one-time bootstrap; unknown or multiple installs must remain untouched. Do not redistribute unsigned candidates or accept arbitrary executables.
4. Compile signed bootstrap and actual launcher, verify existing R1–R6 candidates, unknown versions, clean install, rollback, Windows runtime/WebView2, data/credential/session preservation. Explicitly identify other independent native installations; Linux/Android/iOS PWA updates via web, native store packages only via supported package channel if such installations exist.
5. Stage, test, and approve rollout before publishing production. Then distribute one-time compatible bootstrap and optional notifications across platforms. Device version reporting/administration is later in this same project, with privacy and role controls.

Do not claim automatic update availability or production release until verified. Native policy tests do not install, and no live users are modified.
