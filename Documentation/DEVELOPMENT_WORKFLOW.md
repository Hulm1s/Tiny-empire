# DEVELOPMENT WORKFLOW

How to go from "I want to change something" to "it is live", with the minimum number of deploys.

---

## 1. Opening the project

| Tool | What to open | Where |
|---|---|---|
| **Unity Hub** | the project **Tiny Empire** (Unity 6000.1.6f1) | `D:\iosGame\game` |
| **GitHub Desktop / Git** | the repository | `D:\iosGame` |
| An editor (Rider / VS / VS Code) | the repo | `D:\iosGame` |

**These are two different folders and people get this wrong constantly.** Unity opens `game/`;
git tracks everything from `D:\iosGame`.

The scene is `Assets/_Project/Scenes/Farm.unity`. It should open automatically — the scene builder
writes `Library/LastSceneManagerSetup.txt` so the editor remembers it after a headless rebuild.
(If Unity opens to an empty grey viewport, that file is missing; just open the scene by hand.)

### What you need installed
- Unity **6000.1.6f1** with the **WebGL Build Support** module
- Git Bash (for `publish.sh`)
- Python 3 **or** Node, for the local test server
- Blender **2.93.2** at `D:\Blender\blender.exe` — **only** if you are touching character models

Everything else is in the repo. No package restore step, no npm install, no CI setup.

---

## 2. The shape of the loop

```
EDIT  (almost always FarmSceneBuilder.cs)
  ↓
REBUILD THE SCENE        Tycoon → Rebuild Farm Scene        -> SCENE_OK
  ↓
AUDIT  (if anything moved/resized/was added)                -> "Nothing overlaps."
  ↓
PLAY IN THE EDITOR       press Play                          <- fastest feedback
  ↓
...repeat until the whole batch of changes is done...
  ↓
BUILD THE WEB PLAYER     Tycoon → Build Web                 -> BUILD_OK
  ↓
LOCAL BROWSER TEST       python -m http.server 8123
  ↓
FULL CHECK               TESTING_CHECKLIST.md
  ↓
COMMIT + PUSH
  ↓
DEPLOY                   ./publish.sh    <- requires "GO LIVE"
  ↓
SMOKE TEST THE LIVE SITE in a private window
```

**Press Play in the editor for everything you can.** It needs no web build and gives feedback in
seconds. Step down to a web build only when you need to check WebGL-specific behaviour, mobile
layout, or performance.

---

## 3. Editing

Nearly every gameplay change is one number in **`game/Assets/_Project/Editor/FarmSceneBuilder.cs`**.
See `EDITING_GUIDE.md` for the specific entry, or `CHANGE_MAP.md` to find it fast.

**The scene is generated.** From `TWEAKING.md`:

> If you rearrange things in the editor by dragging them, the next scene rebuild throws your
> changes away. [...] Dragging things around in the editor is still fine for *trying* something.
> Just copy the numbers back into `FarmSceneBuilder.cs` once you like them.

---

## 4. Rebuilding and auditing

In the editor: `Tycoon → Rebuild Farm Scene`, then `Tycoon → Audit Interaction Squares`.

Headless (close the editor first — only one process can hold the project lock):

```bash
"D:/unity/unity engine/6000.1.6f1/Editor/Unity.exe" -quit -batchmode -nographics -projectPath D:/iosGame/game -executeMethod Tycoon.EditorTools.FarmSceneBuilder.Build -logFile D:/iosGame/build-scene.log
```

```bash
"D:/unity/unity engine/6000.1.6f1/Editor/Unity.exe" -quit -batchmode -nographics -projectPath D:/iosGame/game -executeMethod Tycoon.EditorTools.SquareAudit.Run -logFile D:/iosGame/build-audit.log
```

### How to read the results

```bash
grep "SCENE_OK" D:/iosGame/build-scene.log
grep "error CS" D:/iosGame/build-scene.log              # expect nothing
grep "\[Audit\] Nothing overlaps." D:/iosGame/build-audit.log
grep "\[Audit\] MISMATCH" D:/iosGame/build-audit.log    # expect nothing
```

**⚠ `AUDIT_OK` is printed even when the audit finds faults.** The real pass condition is
`[Audit] Nothing overlaps.` A failing run says `[Audit] <n> faults.` and still prints `AUDIT_OK`
with exit code 0.

### Why the audit matters

From `CLAUDE.md`:

> The layout audit is the main safety net. It measures every interaction square and every building
> footprint in the level's own grid and reports overlaps at day one and fully built, whether a
> hire can be bought before its route exists, and whether the camera can ever see the edge of the
> ground. Run it after anything that moves, resizes or adds an object. **It has caught faults the
> eye missed repeatedly.**

---

## 5. Building the web player

```bash
"D:/unity/unity engine/6000.1.6f1/Editor/Unity.exe" -quit -batchmode -nographics -projectPath D:/iosGame/game -executeMethod Tycoon.EditorTools.TycoonBuild.BuildWeb -logFile D:/iosGame/build-web.log
```

Takes about **3.5 minutes**. Output goes to `D:\iosGame\docs` (gitignored — see
`BUILD_AND_DEPLOY.md`).

### ⚠ Stop the local server first

This is the single most common failure. A running server holds `docs/Build/docs.loader.js` open
and the build fails with:

> the requested operation cannot be performed on a file with a user-mapped section open

```bash
netstat -ano | grep 8123
```

### Verify
```bash
grep "\[TycoonBuild\] BUILD_OK"      D:/iosGame/build-web.log
grep "\[WebPlatformSetup\] SETUP_OK" D:/iosGame/build-web.log
grep "error CS"                      D:/iosGame/build-web.log   # expect nothing
grep "Template .* not found"         D:/iosGame/build-web.log   # expect nothing
grep "not found; URP may have"       D:/iosGame/build-web.log   # expect nothing
```

The last two matter because both failures are **soft** — the build still says `BUILD_OK` while
producing something broken.

---

## 6. Testing in a browser

```bash
cd /d/iosGame/docs && python -m http.server 8123
```

Then open `http://localhost:8123/`. Or, as the README documents:

```bash
npx serve D:/iosGame/docs
```

### Rules
- **Always a fresh tab.** Reloading the same tab repeatedly degrades the WebGL context and
  produces shader errors that look like a broken build but are not.
- **Never `file://`.** The service worker will not register and the Build payloads are blocked by
  CORS.
- **A desktop browser is enough** — WASD and arrow keys move the character.
- **`?debug=1`** gives an on-screen readout of frame rate, position and what you are carrying. No
  rebuild needed.
- The pause menu (top right) has **delete save** for testing from scratch.
- Mobile layout: use DevTools device emulation at **375 × 812**. The device-pixel-ratio cap,
  real safe-area insets and Add-to-Home-Screen behaviour only show on an actual phone.

### ⚠ Saves are per-origin
A save made on `localhost:8123` is not the same save as the one on the live URL. They are
different browser origins. This is the usual reason "my progress vanished" after a deploy.

---

## 7. One deploy per feature

**This is the point of the whole workflow.** A Unity web build is ~14 MB and takes 3.5 minutes;
the live site is force-pushed with no history, so every deploy is irreversible from the remote's
point of view. Deploying per change is slow, risky and unnecessary.

### Do this

```
CHANGE A
CHANGE B          <- all tested in the editor with Play
CHANGE C
CHANGE D
   ↓
ONE scene rebuild + audit
   ↓
ONE web build
   ↓
ONE local browser pass against TESTING_CHECKLIST.md
   ↓
ONE commit (or a tidy few)
   ↓
ONE deploy
```

### Not this

```
change → build → deploy → change → build → deploy → ...
```

### What makes this safe

The editor's Play mode covers almost everything: gameplay, economy, progression, workers,
customers, save/load, UI layout. The things it **cannot** tell you are:

| Only visible in a web build | Why |
|---|---|
| Magenta materials | runtime-created materials break only in a URP **player** |
| Real frame rate on a phone | editor timings are not representative |
| Safe-area/notch handling | needs the real template and viewport |
| Service-worker and caching behaviour | needs a served origin |
| The long-first-frame movement bug class | only the web loader produces multi-second frames |
| Add-to-Home-Screen / PWA install | needs the real HTTPS origin |

So: batch everything, verify in the editor as you go, and spend the one web build on the list
above.

### Three test tiers

Pick by what you changed. Full detail in `TESTING_CHECKLIST.md`.

| Tier | When | Roughly |
|---|---|---|
| **Quick** | one value — a price, a colour, a label | 3 min |
| **Gameplay** | a mechanic, a station, workers, customers, economy | 15 min |
| **Full regression** | layout, save format, new content, **any release** | 40 min |

---

## 8. Branching

| Branch | Role |
|---|---|
| `main` | source of truth |
| `gh-pages` | **the live site.** Build output only, force-pushed, one parentless commit. Never work here. |
| feature branches | e.g. `layout-bin-priority-polish` — the current one |

From `TWEAKING.md`:

> Working on a branch first is safer — she is playing the live one.

**The live site can be older than `main`.** Right now `gh-pages` carries
`Publish 2026.09.15.2040`, which predates the four-column farm, the split tills and the three
bins.

---

## 9. Committing

A build dirties tracked files on purpose:
- `game/ProjectSettings/ProjectSettings.asset` — the `bundleVersion` timestamp
- `Assets/Settings/Mobile_RPAsset.asset` and `PC_RPAsset.asset` — `WebPlatformSetup`
- `Assets/_Project/Scenes/Farm.unity` — if you rebuilt the scene
- `EditorBuildSettings.asset` — if the scene list changed

All of that is expected. Review with `git status --short` before staging.

**Commit before publishing.** `publish.sh` records `git rev-parse --short HEAD` as the build's
provenance, so publishing from an uncommitted state points the live commit at a hash nobody can
resolve.

---

## 10. Deploying

```bash
cd /d/iosGame && ./publish.sh
```

**Requires the explicit phrase `GO LIVE`.** That is a working agreement, not a technical lock —
nothing in the script enforces it.

Full procedure and verification in `BUILD_AND_DEPLOY.md` §8, and the short form in
`RELEASE_CHECKLIST.md`.

---

## 11. Working on characters

Only if you are touching the villager models. See `CHARACTERS_AND_MODELS.md`.

```
edit models/pipeline/humanoid_animated.blend  (or animate.py / export_unity.py)
  ↓
blender --background --python export_unity.py -- <blend> <Art/Characters>
  ↓
Unity: Tycoon → Reimport Character Models
  ↓
Unity: Tycoon → Verify Character Models      <- every role must say CopyFromOther / source=set
  ↓
Rebuild the scene
```

**Blender must be 2.93.2.** The pipeline scripts will not run on 4.x.

---

## 12. The rules that govern everything

From `CLAUDE.md`, and worth re-reading before any non-trivial change:

- **Never publish without the exact phrase `GO LIVE`.**
- **Do not break the existing architecture.** StationBase, InteractionSquare, CarryStack, the
  shared player/worker interaction system, the customer system, SaveIdentity, save/load and the
  unlock progression all work. **Prefer a small extension of what exists over a new parallel
  system.**
- **No HUD action buttons.** If something can be a physical place in the world, it is an
  interaction square the player stands in.
- **No mechanic may require money to escape a state where you cannot earn money.**
