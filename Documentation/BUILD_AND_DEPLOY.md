# BUILD AND DEPLOY

Exactly how to turn source into a playable build, and how that build reaches the live site.

**Hard rule from `CLAUDE.md`: never publish without the explicit phrase `GO LIVE`.** Steps 0–8
below are safe at any time. Step 9 is not.

---

## 1. What you need installed

| | |
|---|---|
| Unity | **6000.1.6f1** (revision `d64b1a599cad`) |
| Editor path | `D:\unity\unity engine\6000.1.6f1\Editor\Unity.exe` |
| Required module | **WebGL Build Support** (installed ✅) |
| Unity project | `D:\iosGame\game` ← this is what Unity Hub opens |
| Repo | `D:\iosGame` ← this is what Git/GitHub Desktop opens |
| Shell for publishing | **Git Bash** (`publish.sh` is a bash script) |
| Local server | Python 3 (`python -m http.server`) or Node (`npx serve`) |

**Note the space in `unity engine`.** Every command must quote the editor path.

There is **no iOS or Android module installed**. "An iPhone game" means a Web build added to the
Home Screen — no App Store, no developer account, no expiry.

Packages (`game/Packages/manifest.json`): URP 17.0.1, Input System 1.12.0, AI Navigation 2.0.0,
uGUI 2.0.0, Timeline 1.8.6, Test Framework 1.4.2, Visual Scripting 1.9.1.
**No TextMeshPro, deliberately** — it needs an interactive asset import that a headless build
cannot do.

---

## 2. The five entry points

All are menu items under `Tycoon/` in the editor **and** headless `-executeMethod` targets.

| Method | Menu | Prints on success | Writes |
|---|---|---|---|
| `Tycoon.EditorTools.TycoonBuild.Validate` | *(none)* | `[TycoonBuild] VALIDATE_OK` | nothing |
| `Tycoon.EditorTools.FarmSceneBuilder.Build` | `Tycoon → Rebuild Farm Scene` | `[FarmSceneBuilder] SCENE_OK` | `Farm.unity` |
| `Tycoon.EditorTools.SquareAudit.Run` | `Tycoon → Audit Interaction Squares` | `AUDIT_OK` ⚠ see §6 | nothing |
| `Tycoon.EditorTools.TycoonBuild.BuildWeb` | `Tycoon → Build Web` (`Ctrl+Shift+B`) | `[TycoonBuild] BUILD_OK` | `docs/` |
| `Tycoon.EditorTools.IconSheet.Export` | `Tycoon → Export Icon Sheet` | `ICONS_OK` | `icon-sheet.png` |
| `Tycoon.EditorTools.SoundExport.Run` (`-soundOut <dir>`) | `Tycoon → Export Sound Clips` | `SOUNDS_OK` | one WAV per sound and pitch, plus a level report in the log |

Two constraints that apply to all of them:

- **Close the Unity editor first.** Only one process can hold the project lock.
- **Stop any local server first.** See §6, gotcha 1 — this is the most common build failure.

### The commands

Preflight — does the editor open, do the scripts compile, is the Web module present:

```bash
"D:/unity/unity engine/6000.1.6f1/Editor/Unity.exe" -quit -batchmode -nographics -projectPath D:/iosGame/game -executeMethod Tycoon.EditorTools.TycoonBuild.Validate -logFile D:/iosGame/build-validate.log
```

Regenerate the level:

```bash
"D:/unity/unity engine/6000.1.6f1/Editor/Unity.exe" -quit -batchmode -nographics -projectPath D:/iosGame/game -executeMethod Tycoon.EditorTools.FarmSceneBuilder.Build -logFile D:/iosGame/build-scene.log
```

Layout audit — **the main safety net**:

```bash
"D:/unity/unity engine/6000.1.6f1/Editor/Unity.exe" -quit -batchmode -nographics -projectPath D:/iosGame/game -executeMethod Tycoon.EditorTools.SquareAudit.Run -logFile D:/iosGame/build-audit.log
```

Build the web player (~3.5 minutes):

```bash
"D:/unity/unity engine/6000.1.6f1/Editor/Unity.exe" -quit -batchmode -nographics -projectPath D:/iosGame/game -executeMethod Tycoon.EditorTools.TycoonBuild.BuildWeb -logFile D:/iosGame/build-web.log
```

Render every generated icon to one sheet, for visual inspection:

```bash
"D:/unity/unity engine/6000.1.6f1/Editor/Unity.exe" -quit -batchmode -nographics -projectPath D:/iosGame/game -executeMethod Tycoon.EditorTools.IconSheet.Export -logFile D:/iosGame/build-icons.log
```

`TycoonBuild.RunWebBuild` exit codes: **0** success, **1** build failed, **2** no Web module,
**3** no scenes.

---

## 3. What `BuildWeb` actually configures

Everything is set **in code**, not left in the Project Settings window. `TycoonBuild.cs` header:

> Everything about the Web build is configured here in code rather than left in the project
> settings window, so a build produced on any machine - or in CI later - is identical, and so
> the settings that actually matter for iPhone Safari are written down with the reasons attached.

### Output path

```csharp
private static string OutputPath =>
    Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs"));
```

→ **`D:\iosGame\docs`**. Unity names the payload after the folder, which is why the files are
`docs.loader.js`, `docs.wasm.unityweb`, `docs.data.unityweb`, `docs.framework.js.unityweb`.

`BuildOptions.None` — **no clean flag.** Unity overwrites `docs/` in place, it does not wipe it.

### The settings that matter, and why

| Setting | Value | Reason (from the code) |
|---|---|---|
| **Compression** | **Gzip** + **decompression fallback** | *"GitHub Pages is a plain static host and cannot send a Content-Encoding header. Gzip plus the decompression fallback is the combination that loads correctly there; Brotli without headers produces a silent blank page."* |
| `dataCaching` | on | keeps the build in the browser cache between visits |
| `exceptionSupport` | ExplicitlyThrownExceptionsOnly | *"Full exception support roughly doubles code size and costs frame time."* |
| Managed stripping | **High** | download size |
| IL2CPP config | Master | |
| Orientation | **Portrait only** | *"the whole HUD layout assumes a one-handed phone grip"* |
| Template | `PROJECT:MobilePWA` | the PWA shell |
| `bundleVersion` | **stamped to UTC `yyyy.MM.dd.HHmm` every build** | cache busting, see below |
| Memory | 32 MB initial, 2 GB max, geometric growth | |
| Colour space | Linear | |

### The version stamp — why it exists

```csharp
// The Build/ filenames never change between builds, so both Unity's own data cache
// and the service worker key on the product version instead. Stamping it per build
// is what stops a browser mixing a new .wasm with a stale .data - which shows up as
// a black screen and shader errors, and cost an hour to track down once already.
PlayerSettings.bundleVersion = System.DateTime.UtcNow.ToString("yyyy.MM.dd.HHmm");
```

**This rewrites a tracked file (`ProjectSettings.asset`) on every build.** Expect a dirty working
tree after building.

### `WebPlatformSetup.cs` — the black-screen fix

Runs **first**, before anything else in the build. Its header:

> This exists because of a concrete failure: with the stock template settings the Web build
> rendered nothing but world-space text, and the browser console was full of
> "glDrawElements: Mismatch between texture format and sampler type". URP's HDR path allocates
> float colour buffers that WebGL2 cannot sample the way the shaders expect, so every lit draw
> call was being dropped.

It writes these into **both** `Assets/Settings/Mobile_RPAsset.asset` and `PC_RPAsset.asset`:

`SupportsHDR=false` (*"The actual fix: no float colour buffer"*), `RequireDepthTexture=false`,
`RequireOpaqueTexture=false`, `MSAA=off`, `SoftShadows=false`, `ShadowCascadeCount=1`,
`ShadowDistance=22`, `MainLightShadowmapResolution=1024`, `AdditionalLights=Disabled`.

The shadow distance is a draw-call budget, not a look:

> The orthographic camera shows about 19 world units top to bottom, so anything past ~22 units
> is off screen and its shadow cannot be seen. [...] 40 was paying to shadow half the farm at
> once.

**The web build runs on `Mobile_RPAsset`** (quality level 0, named `Mobile`, which
`QualitySettings` maps to WebGL). Its render scale is **0.8**.

Also a side effect: this **modifies two tracked assets on every build**.

---

## 4. The web shell — `Assets/WebGLTemplates/MobilePWA/`

What the player sees, in order:

1. A sky-blue `#78b5dd` screen, the title **"Tiny Empire"**, and a progress bar that fills as the
   download proceeds.
2. On load: a dark translucent overlay with a yellow **PLAY** pill and the hint
   *"Add to Home Screen for fullscreen"* (hidden if already installed as a PWA).
3. Tapping PLAY hides the gate. **The game is already running behind it** — the gate exists only
   to supply the user gesture iOS requires before an AudioContext can start.

There is **no fullscreen button and no Fullscreen API call anywhere.** Fullscreen comes only from
iOS Add-to-Home-Screen. (Consistent with the no-HUD-buttons rule.)

### Key template mechanics

**Device pixel ratio cap** — described in the code as the most valuable perf line in the file:

```javascript
// An iPhone reports a device pixel ratio of 3. Rendering a 3D scene at native 3x on a
// phone GPU is the single easiest way to drop to 20fps, and on a screen this small the
// difference above 2x is invisible.
devicePixelRatio: Math.min(window.devicePixelRatio || 1, 2)
```

Combined with the 0.8 render scale, an iPhone renders at **1.6× CSS pixels**.

**Cache busting** — `?v=<bundleVersion>` is appended to the loader, data, framework and code URLs:

> The four build files keep the same names forever, so a browser is free to serve a cached copy
> of one alongside a freshly downloaded copy of another. That combination does not merely look
> stale - it fails outright, with the wasm demanding an import the older framework never defined.

**Viewport and the safe-area bridge** — `viewport-fit=cover` lets the game draw behind the notch
(and `black-translucent` the status bar). Unity's WebGL player cannot see the browser's safe
area: `Screen.safeArea` is always the whole screen there, so the HUD used to sit under the notch
on iPhone X and newer. The bridge that fixes it has three parts:

- `<div id="safe-area-probe">` in `index.html` - hidden, `position:fixed`, with
  `padding: env(safe-area-inset-top) env(safe-area-inset-right) env(safe-area-inset-bottom) env(safe-area-inset-left)`.
  Its computed padding *is* the insets, readable as numbers.
- `Assets/_Project/Plugins/WebGL/SafeArea.jslib` - `Tycoon_GetSafeInsets(float[4])` reads that
  padding and writes top/right/bottom/left in **canvas pixels**: CSS px x `canvas.width /
  canvas.clientWidth` (not `devicePixelRatio`, because the template caps the ratio at 2 and Unity
  may render below native). Returns 0 if the probe is missing.
- `UI/HudRoot.cs` `SafeRect()` - on `UNITY_WEBGL && !UNITY_EDITOR` builds the rect from those
  insets; everywhere else it is `Screen.safeArea`. Re-read every 0.5 s and whenever the screen
  size changes, so rotation and toolbar changes apply. The `?debug=1` readout shows the insets in
  use (`safe L.. B.. R.. T..`).

The money readout, alerts, pause button and panel, joystick and paint menu are all children of the
HUD's `SafeArea` rect, so they move together; `ScreenFade` is deliberately the full canvas. A
build whose jslib is missing fails at load with an unresolved import, not silently.

**Touch suppression** — rubber-band scrolling, double-tap zoom, text selection and tap highlights
are all disabled, *"to stop mobile Safari doing something helpful that ruins a game."*

**Resize nudges** at 60/400/1200 ms after load, plus on `visibilitychange`:

> Unity fixes its render target to whatever the canvas measured at start-up. If the viewport
> settled after that [...] the game is drawn into a corner of the canvas.

**Service worker** — a deliberate split strategy:

> index.html / manifest → network first, so republishing the game actually reaches players
> instead of being trapped behind a stale cache.
> Build + TemplateData → stale-while-revalidate, so launches are instant.
> Getting this wrong is the classic "I uploaded a new build and she still sees the old one" bug.

Its cache name is version-scoped (`"Home Made-Tiny Empire-<bundleVersion>"`), so a new build
evicts the previous build's cache wholesale.

**PWA manifest** — `display: standalone`, `orientation: portrait`, `scope: "./"` and a relative
`start_url`, which is what makes it work under a subpath like `/Tiny-empire/`.

---

## 5. Deployment — how `docs/` becomes the live site

### The repository layout

| Branch | What it is |
|---|---|
| `main` | source of truth. **No build output.** |
| `gh-pages` | **the live site.** One parentless commit, force-pushed by `publish.sh`. |
| feature branches | `layout-bin-priority-polish` (current), `perf-and-hire-placement`, `feat/farm-expansion` |

Remote: `https://github.com/Hulm1s/Tiny-empire.git`
Live URL: **`https://hulm1s.github.io/Tiny-empire/`**

**There is no GitHub Actions workflow. No CI. Deployment is entirely manual, from this machine.**

### Why `docs/` is gitignored

From `.gitignore`:

> Build output. Published to the gh-pages branch by publish.sh, never committed to main - a
> Unity web build is ~13 MB of binaries that would be added to history on every single publish.

And the commit that changed it (`71c5fa3`):

> nine builds in, 113 MB of the 135 MB repository was old build blobs, and the 1 GB soft limit
> was about seventy publishes away.

**So `docs/` existing on disk but not in git is correct.**

### What `publish.sh` does

```bash
./publish.sh      # from Git Bash, at D:\iosGame
```

Line by line:

1. **Guard** — refuses if `docs/index.html` is missing. **This is the only safety check**, and it
   only proves a build once existed, not that it is current or correct.
2. Reads `bundleVersion` out of `ProjectSettings.asset` for the commit message.
3. Records `git rev-parse --short HEAD` as the build's provenance.
4. `GIT_INDEX_FILE=<temp> git --work-tree=docs add -A` — stages `docs/` **as if it were the repo
   root**, into a throwaway index. **Your real index and working tree are never touched.**
   (`docs/` being gitignored does not matter here: with `--work-tree=docs`, git looks for a
   `.gitignore` *inside* `docs/`, and there is none. That is the whole trick.)
5. `git commit-tree` with **no `-p`** — a parentless commit, so the branch carries exactly one
   build and no history.
6. `git branch -f gh-pages <commit>` — **destructive, local.**
7. `git push -f origin gh-pages` — **destructive, remote, irrecoverable.**

### ⚠ There is no server-side history

Every `gh-pages` commit is parentless and every publish force-pushes. The moment you publish, the
previous build becomes unreachable on GitHub. **`gh-pages` is not a backup.** See §7.

---

## 6. Known gotchas, in order of how likely they are to bite

### 1. A running local server breaks the build
The single most common failure. A server serving `docs/` holds `docs/Build/docs.loader.js` open,
and Windows refuses to overwrite it:

> the requested operation cannot be performed on a file with a user-mapped section open

Find and stop it:

```bash
netstat -ano | grep 8123
```

### 2. ⚠ `AUDIT_OK` does not mean the audit passed
`SquareAudit.cs:129-132`:

```csharp
Debug.Log(clashes == 0 ? "[Audit] Nothing overlaps." : $"[Audit] {clashes} faults.");
Debug.Log("AUDIT_OK");
if (Application.isBatchMode) EditorApplication.Exit(0);
```

**The token and exit code 0 are unconditional.** The real pass condition is the line
`[Audit] Nothing overlaps.` Trigger/outline mismatches are `LogWarning` only and do not affect
either. `CLAUDE.md` is slightly wrong on this point — it is accurate for `SCENE_OK`, `BUILD_OK`
and `ICONS_OK`, but not for `AUDIT_OK`.

Check it properly:

```bash
grep "\[Audit\] Nothing overlaps." D:/iosGame/build-audit.log
grep "\[Audit\] MISMATCH" D:/iosGame/build-audit.log
```

A clean run (verified on the current scene) looks like:

```
[Audit] 39 squares, 13 building footprints
[Audit] day one: 0 overlapping pairs.
[Audit] fully built: 0 overlapping pairs.
[Audit] buildings: 0 overlapping pairs.
[Audit] squares on buildings: 0 overlapping pairs.
[Audit] world edge: never visible. Closest the camera gets to running out of ground is 23,4 m short of it
[Audit] progression: 0 hires that outrun their own route.
[Audit] Nothing overlaps.
AUDIT_OK
```

### 3. ⚠ `TestPriceOverride = 10d` is currently live
`FarmSceneBuilder.cs:44`. Every price in the game is $10 right now. **Nothing in the pipeline
warns about it.** Set it to `0d` and re-run the scene build before any release.

### 4. ⚠ `docs/.nojekyll` is untracked and nothing generates it
Verified: it is **not** in `HEAD`, not in `origin/main`, and no script or source file mentions it.
It survives only because `docs/` is never wiped. **On a fresh clone, or if anyone deletes `docs/`,
it is gone and `publish.sh` will silently publish without it** — at which point GitHub Pages'
Jekyll processing may mangle the build. Recreate it with:

```bash
touch D:/iosGame/docs/.nojekyll
```

Check for it before every publish.

### 5. Reloading the same browser tab produces fake shader errors
Always test in a **fresh tab**. Repeated reloads degrade the WebGL context and produce errors
that look like a broken build but are not.

### 6. A missing template fails soft
If `Assets/WebGLTemplates/MobilePWA` is renamed or missing, the build logs a warning, uses
Unity's default template, and **still prints `BUILD_OK`** — producing a page with no PWA
manifest, no service worker, no DPR cap and no touch handling. Grep for `Template .* not found`.

### 7. A URP field rename fails soft into the original black-screen bug
`WebPlatformSetup` logs `Field '<x>' not found; URP may have renamed it.` and carries on, still
printing `SETUP_OK`. After any URP upgrade, grep `build-web.log` for `not found; URP may have`.

### 8. Every build dirties tracked files
`ProjectSettings.asset` (bundleVersion), both `*_RPAsset.asset`, and `EditorBuildSettings.asset`.
This is normal.

### 9. Two builds in the same UTC minute defeat cache busting
The version stamp has minute resolution. At ~206 s per build this is unlikely, but it is a real
hole: identical `bundleVersion` means identical `?v=` and identical service-worker cache name.

### 10. `publish.sh` needs Git Bash
It uses `set -euo pipefail`, `trap`, `mktemp -u` and `GIT_INDEX_FILE`. PowerShell cannot run it.

### 11. The scene is generated — hand edits are lost
Running `FarmSceneBuilder.Build` discards anything you dragged around in the editor.

---

## 7. Rolling back a bad release

**`gh-pages` holds exactly one build and has no history.** Two options.

### Option A — republish from the local reflog (fast, only on this machine)

```bash
cd /d/iosGame && git reflog show gh-pages
```

Earlier publish commits remain reachable through the reflog until `git gc` prunes them (90 days
by default). Confirm you have the right one, then — **this is a force-push, `GO LIVE` territory**:

```bash
git log -1 --format="%B" <good-sha>
git branch -f gh-pages <good-sha>
git push -f origin gh-pages
```

**Run `git reflog show gh-pages` before doing anything else**, and write the SHA down somewhere
durable.

### Option B — rebuild from the last-good source commit (reliable)

Every publish commit records its source:

```bash
git log -1 --format="%B" origin/gh-pages      # read the "Built from <sha>" line
```

Then check out that commit, verify `TestPriceOverride` is `0d` at that revision, rebuild the
scene and the web player, and publish again.

### Option C — if the live site is broken and you cannot rebuild
Tell players to close the tab. The service worker serves `index.html` network-first, so the next
load after a fixed publish reaches them; it will not be trapped behind a stale cache.

---

## 8. The full release procedure

### Step 0 — Preflight
```bash
netstat -ano | grep 8123          # stop any local server
cd /d/iosGame && git status --short
```
Close the Unity editor. **Check `FarmSceneBuilder.cs:44` says `TestPriceOverride = 0d`.**

### Step 1 — Rebuild the scene (if layout or economy changed)
Run the scene build. Verify `SCENE_OK`, and `grep "error CS"` finds nothing.

### Step 2 — Layout audit
Run the audit. Verify **`[Audit] Nothing overlaps.`** — not `AUDIT_OK`.

### Step 3 — Build the web player
Run `BuildWeb`. Verify:
```bash
grep "\[WebPlatformSetup\] SETUP_OK"  D:/iosGame/build-web.log
grep "not found; URP may have"        D:/iosGame/build-web.log   # expect nothing
grep "Template .* not found"          D:/iosGame/build-web.log   # expect nothing
grep "\[TycoonBuild\] BUILD_OK"       D:/iosGame/build-web.log
grep "error CS"                       D:/iosGame/build-web.log   # expect nothing
```

### Step 4 — Confirm the version stamped through
All three must show the same timestamp:
```bash
grep -n "bundleVersion" D:/iosGame/game/ProjectSettings/ProjectSettings.asset
grep -o 'productVersion: "[^"]*"' D:/iosGame/docs/index.html
grep -n "CACHE_NAME" D:/iosGame/docs/ServiceWorker.js
```

### Step 5 — Check `.nojekyll` survived
```bash
ls -la D:/iosGame/docs/.nojekyll
```

### Step 6 — Test locally
```bash
cd /d/iosGame/docs && python -m http.server 8123
```
Open `http://localhost:8123/` in a **fresh tab**. Work through `TESTING_CHECKLIST.md`.
Add `?debug=1` for an on-screen fps/position/carry readout.

> Do **not** open `docs/index.html` as a `file://` URL — the service worker will not register and
> the Build payloads are blocked by CORS.

### Step 7 — Stop the server again

### Step 8 — Commit and push the source
```bash
cd /d/iosGame
git status --short       # expect ProjectSettings.asset, maybe the RP assets and Farm.unity
git add -A
git commit -m "..."
git push origin <branch>
```
**Commit before publishing** — `publish.sh` records `HEAD` as the build's provenance, and
publishing from an unpushed state points the live commit at a hash nobody else can resolve.

### Step 9 — Publish (requires `GO LIVE`)
```bash
cd /d/iosGame && ./publish.sh
```
Then verify:
```bash
git log --oneline -1 origin/gh-pages
git log -1 --format="parents:[%P]" origin/gh-pages      # expect parents:[]
curl -s https://hulm1s.github.io/Tiny-empire/index.html | grep -o 'productVersion: "[^"]*"'
```
Wait a minute or two, then open the live URL **in a private/incognito window** — an ordinary
window has the old service worker installed.

---

## 9. Putting it on a phone

1. Open `https://hulm1s.github.io/Tiny-empire/` in Safari on the iPhone.
2. Share → **Add to Home Screen**.

It then launches fullscreen with its own icon, works offline, and keeps progress on the device.

**Progress is stored per origin.** A save made on `localhost:8123` is a different save from the
one on the Pages URL — see `SAVE_SYSTEM.md` §2.

---

## 10. Things that are UNKNOWN / NEED MANUAL VERIFICATION

- **The actual GitHub Pages source setting.** It must be branch `gh-pages`, folder `/ (root)`,
  because that is what is live and it is the only branch carrying a build — but this could not be
  confirmed from the machine (`gh` CLI is not installed). Check
  `https://github.com/Hulm1s/Tiny-empire/settings/pages`. **`README.md` still documents the old
  setting (branch `main`, folder `/docs`) and is wrong.**
- **Whether villager animation actually plays in the shipped build.** Three of the four role FBX
  `.meta` files have `avatarSetup: 0` (NoAvatar) committed on disk. See
  `CHARACTERS_AND_MODELS.md` §9 — this needs confirming in the running game.
