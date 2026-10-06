# RELEASE CHECKLIST

Short and practical. Pick the tier that matches what you changed — a UI colour tweak does not
deserve an hour of testing.

> **Publishing requires the explicit phrase `GO LIVE`.** Nothing in the tooling enforces it; it is
> a working agreement. The live site is force-pushed with no history, so a bad publish cannot be
> undone from the remote.

---

## Always, before anything else

```
[ ] Local server stopped          netstat -ano | grep 8123
[ ] Unity editor closed            (headless runs need the project lock)
[ ] FarmSceneBuilder.cs:44  ->  TestPriceOverride = 0d     ⚠ currently 10d
[ ] git status --short            (know what you are about to commit)
```

---

## SMALL CHANGE
*A colour, a label, a UI value, one number that does not move anything.*

```
[ ] Scene rebuild                 -> SCENE_OK, no "error CS"
[ ] Press Play in the editor — the change is actually there
[ ] Web build                     -> BUILD_OK
[ ] Fresh browser tab, game loads, no console errors
[ ] The change is visible in the built player
[ ] Commit
[ ] Push
[ ] ./publish.sh                  (GO LIVE)
[ ] Live URL in a private window — correct version, loads, playable
```

---

## MEDIUM CHANGE
*A price, a rate, a capacity, a station behaviour, a worker route, a square that moved.*

Everything in SMALL, plus — before the web build:

```
[ ] Layout audit                  -> "[Audit] Nothing overlaps."     ⚠ NOT "AUDIT_OK"
[ ] No "[Audit] MISMATCH" lines
[ ] "[Audit] progression: 0 hires that outrun their own route."
[ ] grep "which does not sell it" build-scene.log   -> nothing
[ ] Play the chain the change touches, start to finish
[ ] If it touched unlocks: check the locked AND unlocked state
[ ] Save, wait 15s, reload — the change persists correctly
```

And after the web build:

```
[ ] Mobile viewport 375x812 — nothing clipped, labels readable
[ ] ?debug=1 — frame rate still healthy
```

---

## MAJOR RELEASE
*Layout changes, new content, save-format changes, character models, or a long gap since the last
publish.*

Everything in MEDIUM, plus the full `TESTING_CHECKLIST.md` regression pass:

```
[ ] Full progression walk: Field -> Coop -> Dairy Till -> Dairy -> Meadow -> Cow Shed
[ ] Every hire bought; every worker completes a full round trip; none stuck
[ ] Counter A never shows milk; counter B never shows eggs
[ ] Counter B silent until a cow shed exists
[ ] Repair works at $0
[ ] Save -> reload -> everything survives
[ ] Delete save -> reload -> nothing comes back
[ ] No [SaveKeys] warnings in the console
[ ] Icon sheet exported and eyeballed
[ ] Walk to all four fences — ground edge never visible, no falling out of the world
[ ] Performance: median main-thread CPU still ~2.6 ms/frame
```

---

## Build verification

After the web build, before publishing:

```bash
grep "\[WebPlatformSetup\] SETUP_OK"  D:/iosGame/build-web.log    # must be present
grep "not found; URP may have"        D:/iosGame/build-web.log    # expect nothing
grep "Template .* not found"          D:/iosGame/build-web.log    # expect nothing
grep "\[TycoonBuild\] BUILD_OK"       D:/iosGame/build-web.log
grep "error CS"                       D:/iosGame/build-web.log    # expect nothing
```

The middle two matter because both failures are **soft** — the build still reports `BUILD_OK`
while producing something broken.

Version stamp — all three must match:

```bash
grep -n  "bundleVersion"               D:/iosGame/game/ProjectSettings/ProjectSettings.asset
grep -o  'productVersion: "[^"]*"'     D:/iosGame/docs/index.html
grep -n  "CACHE_NAME"                  D:/iosGame/docs/ServiceWorker.js
```

And:

```
[ ] ls -la D:/iosGame/docs/.nojekyll          ⚠ untracked; nothing regenerates it
[ ] ls -la D:/iosGame/docs/Build/             4 files, fresh timestamps
```

---

## Publish

```
[ ] Everything committed AND pushed       (publish.sh records HEAD as provenance)
[ ] Local server stopped
[ ] You have the phrase GO LIVE
```

```bash
cd /d/iosGame && ./publish.sh
```

```
[ ] git log --oneline -1 origin/gh-pages                  -> new "Publish <version>"
[ ] git log -1 --format="parents:[%P]" origin/gh-pages    -> parents:[]
[ ] Wait 1-2 minutes
[ ] Open the live URL in a PRIVATE window
[ ] curl the live index.html — productVersion matches what you built
[ ] Smoke test: loads, PLAY works, you can harvest -> feed -> collect -> sell
```

Live URL: **https://hulm1s.github.io/Tiny-empire/**

---

## Release blockers

Do not publish if any of these is true:

- `TestPriceOverride` is not `0d`
- The audit did not say `[Audit] Nothing overlaps.`
- Repair does not work with an empty wallet
- A worker can be hired before its route exists
- Any `[SaveKeys]` warning appears
- Red console errors in the built player
- A save does not survive a reload
- `docs/.nojekyll` is missing

---

## If it goes wrong

```bash
cd /d/iosGame && git reflog show gh-pages
```

**Run that first, before anything else** — the previous build exists only in the local reflog, and
`git gc` will eventually prune it. Full rollback procedure in `BUILD_AND_DEPLOY.md` §7.
