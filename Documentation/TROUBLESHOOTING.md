# TROUBLESHOOTING

Symptom → what to check, in the order worth checking it. Everything here is grounded in how this
project actually behaves; several entries are bugs it has already had.

---

## Build and tooling

### "The build fails with a file lock error"

> the requested operation cannot be performed on a file with a user-mapped section open

**This is the #1 failure in this project.** A local test server is holding
`docs/Build/docs.loader.js` open.

```bash
netstat -ano | grep 8123
```

Stop that process and rebuild. Also: **close the Unity editor** before any headless run — only one
process can hold the project lock.

---

### "The build log says BUILD_OK but the game is broken"

Three failures in this pipeline are **soft** — they warn and carry on:

```bash
grep "Template .* not found"         D:/iosGame/build-web.log
grep "not found; URP may have"       D:/iosGame/build-web.log
grep "\[WebPlatformSetup\] SETUP_OK" D:/iosGame/build-web.log   # must be present
```

- **Missing template** → a Unity-default page with no PWA manifest, no service worker, no
  device-pixel-ratio cap and no touch handling. Technically a successful build, unusable on a
  phone.
- **URP field renamed** → `WebPlatformSetup` silently fails to disable HDR, which reintroduces the
  original black-screen bug. Check this after any URP upgrade.

---

### "The audit said AUDIT_OK but things overlap"

**`AUDIT_OK` and exit code 0 are printed unconditionally** (`SquareAudit.cs:129-132`). The real
pass condition is:

```bash
grep "\[Audit\] Nothing overlaps." D:/iosGame/build-audit.log
```

A failing run says `[Audit] <n> faults.` and **still** prints `AUDIT_OK`. Trigger/outline
mismatches are warnings only:

```bash
grep "\[Audit\] MISMATCH" D:/iosGame/build-audit.log
```

---

### "Unity opens to an empty grey viewport — the game has vanished"

The scene asset is fine; nothing is displaying it. A headless rebuild quits through
`EditorApplication.Exit`, which skips the shutdown that normally records the open scene. The
builder writes `Library/LastSceneManagerSetup.txt` to work around this, but it can fail silently.

**Fix:** open `Assets/_Project/Scenes/Farm.unity` by hand.

---

### "My editor changes disappeared"

The scene is **generated**. Running `Tycoon → Rebuild Farm Scene` throws away anything you dragged
around. Copy the numbers into `FarmSceneBuilder.cs`.

---

### "The price I changed had no effect"

`FarmSceneBuilder.cs:44` — `TestPriceOverride = 10d` replaces **every** purchase price in the
game. Set it to `0d`.

---

### "I edited Item_egg.asset and nothing changed"

Those four `.asset` files are **regenerated on every scene build** from
`FarmSceneBuilder.CreateItems()` (`FSB:125-149`). Change the values there.

---

## The game does not start

### Blank page / nothing loads

1. **Open the browser console.** `index.html` surfaces failures as a red banner, and sets the
   loading hint to `"Could not start: …"` or `"Could not download the game files."`
2. **Are you on `file://`?** The service worker will not register and the Build payloads are
   blocked by CORS. Serve it over HTTP.
3. **Check the four payload files exist:**
   ```bash
   ls -la D:/iosGame/docs/Build/
   ```
4. **Compression mismatch.** The build must be **Gzip + decompression fallback**. Brotli on a
   static host that cannot send `Content-Encoding` is a silent blank page — *"the single most
   common way a Unity Web build fails on a static host."* Both are forced in
   `TycoonBuild.cs:148-155`, so this only happens if that code was changed.

### Black screen with shader errors in the console

Two different causes:

1. **A stale mixed build.** The four `Build/docs.*` filenames never change, so a browser can serve
   a cached copy of one next to a fresh copy of another — *"the wasm demanding an import the older
   framework never defined."* Defended by the `?v=<bundleVersion>` query string and the
   version-scoped service worker, both of which need `bundleVersion` to have changed.
   **Fix:** hard-reload, or open a private window. Verify the three stamps match:
   ```bash
   grep -o 'productVersion: "[^"]*"' D:/iosGame/docs/index.html
   grep -n "CACHE_NAME" D:/iosGame/docs/ServiceWorker.js
   grep -n "bundleVersion" D:/iosGame/game/ProjectSettings/ProjectSettings.asset
   ```
2. **You reloaded the same tab too many times.** This degrades the browser's WebGL context and
   produces shader errors that look exactly like a broken build. **Always test in a fresh tab.**

### Nothing renders but the world-space text

This is the original URP failure `WebPlatformSetup.cs` exists to fix:

> URP's HDR path allocates float colour buffers that WebGL2 cannot sample the way the shaders
> expect, so every lit draw call was being dropped.

Console shows `glDrawElements: Mismatch between texture format and sampler type`.

**Fix:** confirm `[WebPlatformSetup] SETUP_OK` in the build log and that
`Assets/Settings/Mobile_RPAsset.asset` has `m_SupportsHDR: 0`. Run
`Tycoon → Apply Web-Safe Render Settings` if not.

---

## Things look wrong

### "A model / character / item is magenta"

**Something created a `Material` at runtime.** Runtime-created materials render magenta in a URP
player; runtime-created *sprites, textures and fonts* are fine.

Recorded in three places in this codebase, and it has already cost an hour once.

- For **items**: `CarryStack` uses `definition.carryMaterial` precisely because
  `CreatePrimitive` hands back Unity's built-in default material, which is not in a URP build.
- For **customers**: the template is kept inactive in the scene so its materials are real asset
  references — *"a customer built from scratch at runtime would render magenta."*
- For **characters**: delete the offending `.mat` from `Materials/Characters/` and run
  `Tycoon → Reimport Character Models`. The postprocessor recreates it correctly.

### "An icon looks broken — an arrow with no head, corn with no leaves"

That exact symptom was a real bug: **every triangle in every icon was invisible** because the
inside/outside winding test was inverted. `IconFactory.cs:403-410`:

> `Edge` returns a LEFT-positive distance, so a counter-clockwise triangle is the one that needs
> negating. With it the wrong way up the inside tested as outside and nothing drew at all.

```csharp
float flip = area < 0f ? 1f : -1f;   // correct
```

**Look at what you actually drew** — run `Tycoon → Export Icon Sheet` and open
`D:\iosGame\icon-sheet.png`. The icons are only ever seen at ~40 screen pixels in game, which is
far too small to tell a good drawing from a broken one.

### "A shape has a dark slash across the middle"

Two outlined triangles sharing a diagonal get that diagonal outlined twice. Use
`Painter.Quad(...)` — one shape, one outline, around the outside.

### "A square's label hangs off the side / disappears behind a building"

This was fixed by moving the text onto the card itself. If it has come back:
- Check the square is being built through `LevelBuildKit.Station<T>()`.
- Check the label is short. The square splits `ActionLabel` / `StatusValue` / icon precisely so
  labels stay short — do not re-compose them into one string.
- `InteractionSquare` clamps content to `min(card, ContentSize)`, so nothing should be able to
  leave the card.

### "Two squares overlap / I can stand in both at once"

Run the layout audit. It checks overlaps **at day one and fully built**, plus building footprints
and squares on buildings.

If you just added builder code: **add the station component before sizing the collider.**
`StationBase.Reset()` fires on `AddComponent` and stamps the collider back to a 2 m cube.

### "Grass pokes through an interaction square"

A tuft is up to 40 cm tall; a square is painted 9 cm off the ground. The whole built area is
excluded from ground cover by keep-clear rectangles (`FSB:628-631`). If you moved the farm, move
those too.

### "The capacity bubble shows the wrong number"

It reads `ItemBuffer.Count` / `ItemBuffer.capacity` directly, so the source is
`BuildWorkshop`'s `inputCapacity` / `outputCapacity`.

Note **it reports the *input* hopper, not the output basket** — `input` if set, else `output`.
That was a deliberate change: the collect square below the building already shows the output
count, whereas an empty hopper is invisible until the eggs stop.

### "A character has no animation / stands still"

In order:

1. **The avatar.** Three of the four role FBX `.meta` files currently have `avatarSetup: 0`
   (NoAvatar) committed on disk. Run `Tycoon → Reimport Character Models`, then
   `Tycoon → Verify Character Models` — every role must read
   `avatarSetup=CopyFromOther source=set`. See `CHARACTERS_AND_MODELS.md` §6.
2. **The clips.** `Tycoon → Diagnose Character Animation`. Every state must have a real motion,
   not `NULL`. If the `Rig|` prefix was not stripped, every state gets a null motion — and that
   hook is gated on the literal filename `Villager_Rig.fbx`.
3. **The log.** `[CharacterLibrary] Only {n}/4 states got a clip. Characters will stand still.`
4. **Bone names.** The clips bind by transform path (`Rig/Root/Hips/Thigh.L`). Any rename breaks
   binding **with no error**.
5. **`walkThreshold`** is 0.25 m/s — something moving slower than that stays in Idle.

### "A character's hat is floating / not following the head"

The hat is joined into the skinned mesh and weighted 100% to the vertex group **`"Head"`**, looked
up by exact string in `export_unity.py:139`. Rename the head bone and this silently breaks.

### "A worker's shirt colour won't change"

The eight worker shirts are **Material objects embedded in `Farm.unity`**, not assets. Editing
`Materials/Characters/Farmer_Shirt.mat` does nothing. Change the `color:` argument in the
`BuildHire(...)` call and rebuild the scene.

---

## Gameplay problems

### "A worker is stuck"

In order of likelihood:

1. **Its route end is not unlocked.** A square that has not been bought has a position but no live
   trigger. The worker walks to empty grass and waits forever.
   > The level builder sells a hire with the building its worker delivers into, so this should
   > never fire in the farm as shipped.

   Check: is the hire in the right `Gate(...)` call? Run the audit —
   `[Audit] progression: 0 hires that outrun their own route.`
2. **Its dropoff cannot accept what it carries.** A till that does not `sells` the item never
   drains the stack, so the worker parks at the counter indefinitely. Caught at build time:
   ```bash
   grep "which does not sell it" D:/iosGame/build-scene.log
   ```
3. **Off the NavMesh.** It falls back to beelining straight at the target, so it is never stranded
   — but it walks through buildings. Add `?debug=1` and look for `OFF-NAVMESH`.
4. **`pickup` or `dropoff` is null.** The audit reports
   `ROUTE <name> has no pickup or no dropoff.`
5. **You are standing in its square.** Player priority makes it wait 2.9 m away, refreshed every
   frame you are there. Intended, but it looks like a bug.

### "A customer doesn't spawn"

1. **Nothing on that till's menu is producible yet.** `ProductRegistry.CanProduce` gates it. The
   dairy till is *designed* to stand empty until the first cow shed is unlocked.
2. **All 3 slots are full.** The spawn timer does not even accumulate.
3. **Low reputation.** At the 0.25 floor the interval quadruples — 2.5 s becomes 10 s with one
   hen.
4. **Wiring missing** — `customerTemplate`, `slots`, or `register.sells` null/empty.
   **This fails silently; nothing is logged.**

### "A milk customer is blocking the egg counter"

`CustomerQueue.Front` only ever offers the **head** of the queue, so one unservable customer
blocks everyone behind them for their full 75 seconds.

**The fix is one product per till** (`FSB:267-271`), already in place. If this has come back,
someone gave a till a multi-item `sells` array. That reopens the original bug in full.

### "The player can't interact with a square"

1. **The square is not unlocked** — `Gate()` deactivates everything behind a purchase.
2. **The trigger is smaller than the drawn outline.** The collider-ordering bug. Run the audit and
   look for `[Audit] MISMATCH`.
3. **The `Player` tag was lost.** `StationBase` identifies the player by
   `carry.CompareTag("Player")` on the GameObject holding the `CarryStack`. Without it, the player
   is treated as a worker: priority stops working and station filtering kicks in.
4. **No `CarryStack` reachable** via `GetComponentInParent` — the station ignores the collider
   entirely.
5. **`playerCompatible == false`** (nothing in the shipped farm sets this).

### "The player fell out of the world"

Two causes, both already fixed — if it recurs, check these:

1. **The long first frame.** `PlayerMotor` clamps `Time.deltaTime` to 0.05 s:
   > The first frame after a Web build finishes loading can be several seconds long. Feeding that
   > straight into `Move()` would displace the character tens of metres in one step, which tunnels
   > straight through the ground collider.
2. **Missing boundary walls.** `BuildBoundary` puts solid (non-trigger) colliders along the fence:
   > Found by holding a movement key for twenty seconds, which a bored player will absolutely do.

### "The joystick ring is stuck on screen"

A touch that ends outside the canvas, or is cancelled by the browser, never delivers
`OnPointerUp`. `VirtualJoystick.Update()` re-asserts the hidden state every frame when no pointer
is tracked. If this recurs, that guard was removed.

### "I'm softlocked — my arms are full of something useless"

You should not be. Three mechanisms exist to prevent it:
- **The carry stack holds mixed goods**, so a full stack of the wrong thing never blocks picking
  up something else.
- **There are three bins**, one always within a few seconds of anywhere.
- **Hay is sold with the cow shed**, so it can never exist before something that eats it.

If you hit a genuine dead end, that is a design bug — check it against the rule: *no mechanic may
require money to escape a state where you cannot earn money.*

### "A jammed machine and no money"

Repair must work at $0. `RepairStation.freeRepairFraction` (0.25) guarantees a quarter of the
repair rate for free. **If this is ever set to zero, the game can brick itself.**

---

## Save problems

### "My progress vanished"

1. **Different origin.** WebGL `PlayerPrefs` is per-origin IndexedDB. A save made on
   `localhost:8123` is not the save on the Pages URL. This is the usual answer.
2. **Browser site data was cleared.** There is no cloud backup.
3. **A save id or component type name changed.** The save key is `<SaveIdentity.Id>#<TypeName>`.
   Renaming either orphans that object's state. See `SAVE_SYSTEM.md` §11.
4. **You did not wait for the autosave.** It runs every 15 seconds, plus on pause/focus-loss/quit.
   Wait 15 s or switch tabs before reloading.

### "`[SaveKeys]` warnings in the console"

> `'…' has no SaveIdentity and is keyed by hierarchy path. Renaming or moving it will lose its
> saved state.`

A saveable object was created without `LevelBuildKit.Identify(go, "farm.…")`. **Fix it rather than
ignoring it** — that object will lose its progress the first time anything is renamed.

### "I deleted my save but my purchases came back"

That is a specific bug this code is built to prevent, via `SaveSystem._wiping`:

> Deleting the save clears the stored blob, but reloading the scene then destroys every saveable
> in the old one - and unregistering captures state on the way out, which put all the old progress
> straight back.

If it recurs, something is destroying saveables in bulk outside `GameRoot.ResetProgress()`'s
guarded sequence.

### "Offline progress didn't apply / applied too much"

Capped at **8 hours** (`GameClock.MaxOfflineSeconds`). It is one clamp for both production and
decay, so raising it increases offline income *and* offline spoilage.

Offline catch-up is per-component, from each component's own `lastUnix`. **There is no offline
money** — production accrues into buffers, but goods still have to be collected and sold.

---

## Deployment problems

### "GitHub Pages doesn't update"

1. **Did the push actually land?**
   ```bash
   git log --oneline -1 origin/gh-pages
   ```
2. **Wait a minute or two.** Pages takes a moment to rebuild.
3. **You are looking at a cached copy.** Open the live URL in a **private/incognito window** — an
   ordinary window has the previous service worker installed. `index.html` is served
   network-first specifically so this resolves itself on the next load, but your own browser may
   still hold the old shell.
4. **Confirm what is actually live:**
   ```bash
   curl -s https://hulm1s.github.io/Tiny-empire/index.html | grep -o 'productVersion: "[^"]*"'
   ```
5. **Check the Pages source setting.** It should be branch `gh-pages`, folder `/ (root)`.
   ⚠ `README.md` still documents the old setting (branch `main`, folder `/docs`) and is wrong.
   Verify at `https://github.com/Hulm1s/Tiny-empire/settings/pages`.

### "The site is live but Jekyll mangled it"

`docs/.nojekyll` is missing. It is **untracked and nothing generates it** — it survives only
because `docs/` is never wiped.

```bash
touch D:/iosGame/docs/.nojekyll
```

Then rebuild the tree and publish again. Check for it before every publish.

### "I published a bad build — how do I roll back?"

**There is no server-side history.** Every `gh-pages` commit is parentless and every publish
force-pushes.

```bash
cd /d/iosGame && git reflog show gh-pages
```

**Run this before doing anything else** — reflog entries expire and `git gc` prunes unreachable
objects. Full procedure in `BUILD_AND_DEPLOY.md` §7.

### "The game works in Unity but not in the browser"

Checklist, in order:

- [ ] A `Material` created at runtime? → magenta. Use a sprite/texture instead.
- [ ] A long first frame tunnelling the player through the floor? → clamp `Time.deltaTime`.
- [ ] TextMeshPro anywhere? → it is not in this project and cannot be added headlessly.
- [ ] `Time.time` used instead of `GameClock.NowUnix`? → it resets on every page load.
- [ ] Reloaded the same tab repeatedly? → use a fresh one.
- [ ] `[WebPlatformSetup] SETUP_OK` present in the build log?
- [ ] `grep "error CS"` in the build log?
- [ ] Browser console — any red errors at all?
- [ ] `?debug=1` for fps, position and carry state.

---

## Fast diagnostic reference

| Tool | Command / action | Tells you |
|---|---|---|
| Layout audit | `Tycoon → Audit Interaction Squares` | overlaps, route/progression faults, world-edge visibility |
| Icon sheet | `Tycoon → Export Icon Sheet` | what the icon code actually drew |
| Character verify | `Tycoon → Verify Character Models` | avatars, clips, tris, materials, bones |
| Animator diagnose | `Tycoon → Diagnose Character Animation` | controller states, clip curves, bone bindings |
| Build smoke test | `-executeMethod …TycoonBuild.Validate` | editor opens, scripts compile, Web module present |
| In-game readout | `?debug=1` in the URL | fps, dt, position, carried goods |
| Save wipe | pause menu → `DELETE SAVE` | a clean run |
| Build logs | `D:\iosGame\build-*.log` | everything the headless runs printed |
