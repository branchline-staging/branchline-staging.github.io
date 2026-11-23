# Branchline Unified Workspace — Real-World Release Acceptance

**Recorded:** 2026-11-23 (Asia/Manila)  
**Environment:** [Existing staging Pages site](https://branchline-staging.github.io/) and isolated Supabase project `rlfdvmiqkbitetatxkju`  
**Production verdict:** **HOLD** — live automated tests pass, real signed-in editing/Drive and physical printer evidence still required.

## Already verified (do not re-label simulations as live owner tests)

- [10/10 GitHub Actions hosted-browser tests passed](https://github.com/branchline-staging/branchline-staging.github.io/actions/runs/45139912356), including native PTG SAVE PDF and browser Letter print PDF, responsive four-application menus, both protected Studios denying anonymous editor entry, and test-script Drive read/write/restore against the *real hosted JavaScript* with **mock remote records**.
- Staging Pages deploy and rebuilt static site passed [GitHub Actions](https://github.com/branchline-staging/branchline-staging.github.io/actions/runs/45139912317).
- Read-only staging DB inspection confirms 27 prior successful authentication events (since Nov 20); 9 one-use tickets redeemed (6 Flyer, 3 Frame) and 9 corresponding proof sessions. This is evidence of previous **server-side** authentication and ticket exchange, **not** proof of a complete new end-to-end signed-in editor test in the current build.
- One server-backed **encrypted Studio cloud backup** exists. **This is NOT proof of a company Google Drive backup.**
- Staging database account roles currently: one active `BRANCH_USER` with `legacy_role=TEAM LEADER`; **zero `SYSTEM_ADMIN` accounts**. Do not silently promote the existing user for testing.
- Staging Edge functions `ptg-auth`, `ptg-studio-access`, `megaframe-editor-delivery`, and `flyer-editor-delivery` are ACTIVE.

## Four authentic acceptance gates (currently not verified)

**A. Real account sign-in and logout.** An authorized user signs in at https://branchline-staging.github.io/ and also https://branchline-staging.github.io/first-megasaver-ptg/login/. Confirm the dashboard/name and logout invalidates the session. Do **not** share the password, browser session, bearer ticket, JWT, or Google OAuth token in GitHub/ChatGPT.

**B. Protected Studio editing.** While logged in as an eligible user, open both Studios through the PTG authorized launcher; verify one-use ticket handoff, canvas rendering, creating a clearly fictional test project, saving, closing/reopening, and file-edit persistence. Check expired/replayed ticket denies access and logout revokes the protected editor. Screenshots should not show customer photos. Stage endpoint: https://branchline-staging.github.io/first-megasaver-ptg/studios/

**C. Company Google Drive *real* backup/restore.** Approved Google Drive owner grants one-time OAuth authorization to the protected backend **without sharing credentials**. In each authenticated Studio, create a synthetic project, click Company Drive Backup, then Refresh/List, retrieve/Restore the encrypted record and verify the original project content exactly matches. The `not_configured` response specifically means owner OAuth authorization is missing. Run the test under the authorized owner's Drive account; the ChatGPT Google Drive connector, if any, does not substitute for Studio's protected OAuth backend. Never test using actual customer photos.

**D. Physical printing.** Use https://branchline-staging.github.io/first-megasaver-ptg/demo/ to select a fictional item, print at 100%/actual size on **US Letter 8.5 x 11 in**, and inspect the paper for clipping, centering, font/image resolution and four-up alignment. If Windows WebView2 printing/installer parity is required, do a separate isolated installed-preview printer test; the Nov 10 production installed app has not been upgraded. Browser-generated PDFs already passed but are *not* physical paper evidence.

**Additional admin role gate:** The staging account inventory currently has no System Admin, so the Data Desk permission check cannot honestly pass in staging. Provision a dedicated staging System Admin through an approved management/bootstrap procedure without reassigning the Team Leader account. Verify Data Desk visible only to System Admin and server route denies other roles.

## Evidence to record

Store sanitized PASS/FAIL, time, browser/printer model and optionally a redacted screenshot/photo of **each** real-world gate. Never include credentials, bearer tokens, private files, personal customer data, authentication hashes, service keys, or full session cookies.

## Release restrictions

Keep all `kzamora19` production repositories and existing Windows installer releases unchanged until all real checks, Data Desk System Admin validation, and any required update/migration test pass. No workaround may waive access control for the sake of a green check.

## Security hardening advisory

The Supabase staging security advisor currently reports **Leaked Password Protection Disabled** (WARN). See [Supabase guidance](https://supabase.com/docs/guides/auth/password-security#password-strength-and-leaked-password-protection). RLS-enabled/no-policy notices on server-only tables can be intentional but should be reviewed against the server-only data access design before production. No policy/security changes were made by this audit.
