# Security review and lint pass, 16 November 2026

Reviewed on main at cb2b04a: every script under `Assets/AfterHours`, the Android manifest, project settings, `.gitignore`,
the editor tools that sign, install and push, `Tools/` (make_audio.py, make_haptics.js, Unity.ps1) and the whole git history.

**Not compiled or run.** This review was done without Unity or a C# compiler. The changes are small, but they still need
After Hours → Build scene and the 12 test suites in Unity before merging. The Comfort suite has one new check.

## Fixed in this branch

| # | Severity | Where | Problem | Fix |
|---|----------|-------|---------|-----|
| 1 | Medium | `SaveBackup.cs` | The backup save (`Android/data/<package>/files/AfterHours-save.json`) is in shared storage that anyone with the headset on USB can edit. Its wardrobe entry carries the list of **bought** add-ons. On a fresh install, an edited file is restored, and if the store can't be reached at launch, those add-ons stay unlocked without being paid for. | Bought add-ons are now stripped from the wardrobe when it is restored from the backup. Real purchases come back from the Meta store (or its offline cache) through `AddOnShop.Restore`. New Comfort check: "Bought add-ons are never taken from the backup copy". |
| 2 | Low | `AddOns.cs` | `ForThisDevice()` gave every non-Android platform the free test store. A desktop player build would have handed out all 23 add-ons for nothing. | The test store is now for the editor only. Other non-Quest builds get `NoAddOnStore` (nothing on sale, nothing owned). |
| 3 | Low | `GitHubPush.cs` | The push window wrote `git remote get-url origin` to the Console and `Logs/AfterHours-git-push.txt`. If the remote ever holds a token (`https://user:token@github.com/...`), it would be logged. | The user name and token are removed from the URL before it is shown or logged. |
| 4 | Low | `ReleaseSigning.cs` | When the release key was created, keytool got the password on its command line, which other programs on the PC can read. | The password now goes through an environment variable (`-storepass:env`). This only matters if a key is ever created again. |

## Checked and fine

- **No secrets in git.** I searched every commit for keystores, the key password file, DevAgent settings, the Meta session id, GitHub/AWS tokens and private keys. None found. `UserSettings/` (the release key and its password) is ignored. `ProjectSettings.asset` has never held a keystore path or password, and `ReleaseSigning.Clear()` empties them after each build.
- **Release builds** clear the DevAgent LAN address and access token (`ReleaseBuildPrivacy`). `GitHubPush` refuses to commit those files.
- **Purchases:** `LaunchCheckoutFlow` only counts a purchase when the store returns that SKU. A refund is only believed when the online store says so, never from the offline cache. Prices and names come from the dashboard, not the game.
- **Saved game:** the checkpoint and wardrobe live in PlayerPrefs (app-private). Loading clamps the room index, ignores unknown outfit ids and falls back to defaults on bad JSON. The backup restore only accepts the game's own keys.
- **Manifest:** `allowBackup="false"`. Only the launcher activity is exported, as required. Permissions are just hand tracking (plus what the Meta SDK merges in). Supported devices: Quest 2, Pro, 3 and 3S.
- **Python and JS tools:** no shell calls, eval, pickle or network access. They only write their own output files. `ruff` (errors, bugbear, bandit rules) is clean on `Tools/make_audio.py`, and `node --check` passes on `Tools/make_haptics.js`.

## Worth knowing (not changed)

- **The repository is public.** Anyone can read the source and download `Builds/Quest/AfterHours-release.apk`. Since the game is free that's mostly fine, but it means outfit ownership can only be as strong as a client-side check. That's normal for Quest add-ons, because Meta's store is the authority.
- **Entitlement check:** if the Platform SDK can't start at all, the game keeps running instead of quitting (`StoreStart.Unavailable`). It only quits when the entitlement check itself fails. Meta's guidance is to quit on any failure. Because the game is free this is low risk, so I left it alone rather than risk closing the game for players who are offline.
- **`Tools/Unity.ps1`** reads the local Unity pipeline token from `Library/` (ignored by git) and only talks to 127.0.0.1.

## Lint

The last lint pass (cb0b72b) was followed only by the smoother-graphics change (#20) and the README update (#21). I read the #20 diff
(`Geometry`, `DeviceProfile`, `BlockTower`, `TowerBlock`, `Grabbable`, `VRPlayer`, the builder and validation changes) and found
nothing to fix. Normals are set per corner and `RecalculateNormals` is correctly dropped. `DeviceProfile.Undo` restores the
shadow-map resolution and the sun's shadow type. `Knock` guards `contactCount`.

Outside the repository, the store-art generator (`make_store_art.py` in the project files) had three unused variables, and they're now tidied.
