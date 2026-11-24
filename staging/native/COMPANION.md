# Windows companion updater — source candidate

This independent companion preserves the installed launcher bytes and UI until an explicitly approved native update is installed. It is a **candidate, not a released app**. It is designed to be invoked from a new optional shortcut using `--launch` (launch the existing workspace and check for updates) or directly for manual checking. It never modifies existing shortcuts, never closes the workspace forcibly, and never launches an unverified unknown installation. Keep the user's original shortcut during pilot testing. A real in-header check command requires current R6 source; the older R2 C# source must not be substituted. Account role display is not changed by this companion.

The build embeds only an owner-provided RSA public key. Keep the private key offline or in an owner-controlled signing service. Do not paste private keys into chat or commit them. Build on Windows:
```powershell
.\staging\native\build-companion.ps1 -PublicKeyFile C:\secure\branchline-release-public.xml -OutputDirectory C:\release\candidate
```
The companion pins the **production** PTG Pages native-release directory; staging policy/transaction fixtures do not consume real production packages. No endpoint or signing identity has been published. Do not distribute CI builds (ephemeral test key). A production companion build needs an owner-controlled key, Authenticode signing and device verification.

Release format: raw UTF-8 `release.json` schema 1, binary RSA PKCS#1 SHA-256 signature `release.sig`, exact named EXE and SHA-256. Sequence starts at 7 (legacy floor 6). Publisher signs only approved, tested payloads with explicitly supported base hashes; never publish unsigned candidates. Future installations use a signed receipt matching the installed payload digest and sequence. Receipts are persisted only after verified replacement; previous executable and prior receipt backups retained. A corrupt/missing receipt for an unknown build fails closed. No auto-install without user confirmation and closed application.

Remaining: create owner-controlled trust identity, actual current R6 source/role correction, release signing/publishing pipeline and exact device acceptance with real R1–R6 and successive releases. Need explicit pilot validation before migrating users' shortcuts or enabling the native production channel. 
