# SAVE SYSTEM

How Tiny Empire persists progress, what is stored, and — most importantly — **what you must
never change** once players have saves.

Everything here was read from the live code. File references are exact.

---

## 1. Where it lives

| File | Role |
|---|---|
| `game/Assets/_Project/Scripts/Core/SaveSystem.cs` | The store. Load, save, register, wipe. |
| `game/Assets/_Project/Scripts/Core/ISaveable.cs` | The contract + `SaveKeys.For()` key builder. |
| `game/Assets/_Project/Scripts/Core/SaveIdentity.cs` | The stable per-object id component. |
| `game/Assets/_Project/Scripts/Core/GameRoot.cs` | Owns the Wallet, drives autosave, does the wipe. |
| `game/Assets/_Project/Scripts/Core/GameClock.cs` | The only time source. Offline catch-up lives on this. |
| `game/Assets/_Project/Editor/LevelBuildKit.cs:485` | `Identify()` — assigns save ids at build time. |
| `game/Assets/_Project/Editor/FarmSceneBuilder.cs` | Where every save id string is actually written. |

---

## 2. The storage format

There is no save *file*. Everything goes into **one Unity `PlayerPrefs` string**.

`SaveSystem.cs:16`

```csharp
private const string PrefsKey = "tycoon.save.v1";
```

On WebGL, `PlayerPrefs` is backed by the browser's **IndexedDB**, under the origin the game is
served from. That has three consequences you will meet in practice:

- A save made on `localhost:8123` is **not** the same save as one made on the GitHub Pages URL.
  They are different origins. This is the usual reason "my progress vanished" after a deploy.
- Clearing site data in the browser wipes progress. There is no cloud backup.
- `PlayerPrefs.Save()` is what actually flushes IndexedDB. It is called explicitly —
  `SaveSystem.cs:145`:

  ```csharp
  PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(blob));
  // Required on the Web platform: this is what flushes the browser's IndexedDB.
  PlayerPrefs.Save();
  ```

### The blob

`SaveSystem.cs:18-25`

```csharp
[Serializable]
private class Blob
{
    public double money;
    public double savedAtUnix;
    public List<string> keys = new List<string>();
    public List<string> values = new List<string>();
}
```

Two parallel lists, not a dictionary — `JsonUtility` cannot serialise a `Dictionary`. Each
`values[i]` is itself a JSON string produced by the component that owns `keys[i]`.

So the saved data is JSON containing JSON. A real entry looks roughly like:

```
key:   "farm.coopA.input#ItemBuffer"
value: "{\"count\":4,\"spoilAccumulator\":0.0,\"lastUnix\":1759762800.0}"
```

### Why nested JSON instead of one schema

`ISaveable.cs:8-11`

> State travels as a JSON string produced by JsonUtility so each component owns its own
> serializable struct and the save file never needs a central schema. That is what makes
> adding a new station type a one-file change.

This is the design decision that matters most for you: **there is no central save schema to
update.** A new station type that implements `ISaveable` just works.

---

## 3. Save keys — the fragile part

A key is built by `SaveKeys.For(component)` in `ISaveable.cs:38-55`:

```
<SaveIdentity.Id> + "#" + <ComponentTypeName>
```

e.g. `farm.coopA#ProducerMachine`.

> Note: the doc comment in `ISaveable.cs:31` gives the example as
> `"farm.coopA.machine#ProducerMachine"`. That is stale — the builder puts `ProducerMachine` on
> the workshop **root**, so the real key has no `.machine` segment. The comment is wrong, the
> code is right; see §4.

Two rules are baked in and both exist because of real bugs:

**Rule 1 — the identity is read from the same GameObject only.** `ISaveable.cs:40-41`:

> Same GameObject only, deliberately. Searching parents would give a coop's input and output
> buffers the same key, silently merging two different piles of goods.

**Rule 2 — the id is assigned by the level builder, never generated.** `SaveIdentity.cs:8-15`:

> Save keys used to be built from the hierarchy path, which meant renaming or reparenting an
> object silently wiped its saved progress. [...] The id is assigned by the level builder
> rather than generated, so it is identical every time the scene is regenerated - a random
> GUID would reset everyone's progress on every rebuild.

### The fallback, and the warning that tells you about it

If a saveable component has **no** `SaveIdentity`, the key falls back to the hierarchy path and
the game logs a warning once (`ISaveable.cs:50-52`):

```
[SaveKeys] 'Farm/CoopA/Machine#ProducerMachine' has no SaveIdentity and is keyed by
hierarchy path. Renaming or moving it will lose its saved state.
```

**If you ever see that warning in the browser console, you have introduced a saveable object
that the level builder did not call `Identify()` on.** Fix it by giving it an id — do not
ignore it, because the object will lose its progress the first time anything is renamed.

---

## 4. Every save id that currently exists

These strings are written in `game/Assets/_Project/Editor/FarmSceneBuilder.cs`. **Changing any
of them destroys that object's saved progress for every existing player.**

| Save id | Written at | What it carries |
|---|---|---|
| `farm.market` | `FarmSceneBuilder.cs:253` | the shopfront root |
| `farm.counterA` | `FarmSceneBuilder.cs:267` | egg till root |
| `farm.counterB` | `FarmSceneBuilder.cs:270` | milk till root |
| `farm.bin.west` | `FarmSceneBuilder.cs:294` | west bin |
| `farm.bin` | `FarmSceneBuilder.cs:297` | centre bin |
| `farm.bin.east` | `FarmSceneBuilder.cs:300` | east bin |
| `farm.cornA` | `FarmSceneBuilder.cs:304` | corn field A (crop growth) |
| `farm.cornB` | `FarmSceneBuilder.cs:308` | corn field B |
| `farm.coopA` | `FarmSceneBuilder.cs:313` | coop A (+ its buffers/machine/durability as suffixed children) |
| `farm.coopB` | `FarmSceneBuilder.cs:325` | coop B |
| `farm.hayA` | `FarmSceneBuilder.cs:341` | meadow A |
| `farm.hayB` | `FarmSceneBuilder.cs:346` | meadow B |
| `farm.cowA` | `FarmSceneBuilder.cs:352` | cow shed A |
| `farm.cowB` | `FarmSceneBuilder.cs:362` | cow shed B |
| `farm.unlock.cornB` | `FarmSceneBuilder.cs:418` | Field purchase progress |
| `farm.unlock.coopB` | `FarmSceneBuilder.cs:422` | Coop purchase progress |
| `farm.unlock.counterB` | `FarmSceneBuilder.cs:426` | Dairy Till purchase progress |
| `farm.unlock.cowA` | `FarmSceneBuilder.cs:442` | Dairy purchase progress |
| `farm.unlock.hayB` | `FarmSceneBuilder.cs:454` | Meadow purchase progress |
| `farm.unlock.cowB` | `FarmSceneBuilder.cs:458` | Cow Shed purchase progress |
| `farm.hire.<Id>` | `FarmSceneBuilder.cs:589` | one per worker: `farm.hire.HarvesterA`, `.SellerA`, `.HarvesterB`, `.SellerB`, `.HayHandA`, `.MilkRunA`, `.HayHandB`, `.MilkRunB` |


### The supermarket's ids (all `market.` or `farm.` additions)

Written in `MarketBuilder.cs` (and `FarmSceneBuilder.BuildMarketLink` for the two `farm.` ones).
**Same rule: never change them once anyone has progress.**

| Save id | Carries |
|---|---|
| `game.tutorial` | `TutorialProgress` ("Tutorial" object, `FarmSceneBuilder.BuildFarm`): one number, the walkthrough step (0-5; 5 = finished). A save with no `game.tutorial` entry but with money or any purchase starts finished |
| `farm.unlock.market` | the $10,000 supermarket purchase (`UnlockStation`) |
| `farm.travel.town` | the farm-side travel square (not saveable, but identified) |
| `market.travel.farm` | the market-side travel square (not saveable, but identified) |
| `market.trash.1` … `market.trash.4` | the four rubbish piles: an `ItemBuffer` of `Trash` (4 bags each); a half-cleared pile restores with the bags it had left. `ClearablePile` keeps no state |
| `market.trashcollect.1` … `.4` | the piles' collect squares (identified, not saveable) |
| `market.unlock.repair` | the repair step (`UnlockStation`) |
| `market.decor` | the shop's look (`MarketDecor`): `painted` flag + `DecorLook` (pattern index, three colours, floor kind, floor colour, wood shade). Pattern and floor-kind numbers are stored by index - new ones go on the end of `PatternFactory`'s lists |
| `market.paint` | the PAINT square (identified, not saveable) |
| *(removed)* `market.clean.1-4`, `market.unlock.paint` | replaced by the trash piles and the paint menu. Saves from the first market build keep these as dead entries; a player who had already paid for paint pays the first paint job again |
| `market.open` | the OPEN square (`ChoreStation`) |
| `market.storage.egg/milk/corn`, `market.storage.bread/apples/yogurt` | crate: `ItemBuffer` **and** `SupplyFeed` on one object (`#ItemBuffer`, `#SupplyFeed`). The bread/apples/yogurt crates are new (round 3) and are revealed by their orders |
| `market.shelf.egg/milk/corn`, `market.shelf.s21/s22/s23` | shelf `ItemBuffer` (s21 bread, s22 apples, s23 yogurt: named for their place on the plan, not the product) |
| `market.stock.<product>`, `market.collect.<product>` | the squares, all six products (identified, not saveable) |
| `market.checkout` | shopper queue: reputation (`CustomerQueue`), the shop's one reputation |
| `market.checkout.serve` | the checkout square (identified, not saveable) |
| `market.checkout2` | the second till's queue (`CustomerQueue`): `sharesReputationWith` the first, so it restores nothing of its own |
| `market.checkout2.serve` | the second checkout square (identified, not saveable) |
| `market.hire.cashier`, `market.hire.cashier2` | the two cashier hires (`UnlockStation`) |
| `market.hire.stock1a/stock1b` | sklad1's two stocker hires (`UnlockStation`) |
| `market.hire.stock2a/stock2b` | sklad2's two stocker hires (`UnlockStation`), on sale from the bread order |
| *(removed)* `market.hire.stocker.egg/milk/corn` | the three single-shelf stockers of the last build; a player who had hired one must hire again (approved) |
| `market.unlock.shelf.s21/s22/s23` | the three shelf purchases (`UnlockStation`) |
| `market.order.bread/apples/yogurt` | the three goods orders (`UnlockStation`) |
| `market.unlock.checkout2` | the second checkout purchase (`UnlockStation`) |
| `market.comingsoon` | the locked COMING SOON square (`LockedStation`; identified, nothing to save) |
| `market.bin`, `market.bin.discard` | the market bin |

`RevealWhenAll` has **no entry and needs none**: it stores nothing and re-derives its answer from
the saved components it watches on every load.

### Child ids derived from a parent

`LevelBuildKit` suffixes the parent's id for the parts of a building. For a workshop built with
id `X` (`LevelBuildKit.BuildWorkshop`, lines 719–783):

| Save key | Component | Created at |
|---|---|---|
| `X#ProducerMachine` | `ProducerMachine` | `LevelBuildKit.cs:731` |
| `X#Durability` | `Durability` | `LevelBuildKit.cs:728` |
| `X.input#ItemBuffer` | input hopper | `LevelBuildKit.cs:723` |
| `X.output#ItemBuffer` | output pile | `LevelBuildKit.cs:725` |
| `X.feed` | `DepositStation` | `LevelBuildKit.cs:767` |
| `X.collect` | `CollectStation` | `LevelBuildKit.cs:771` |
| `X.repair` | `RepairStation` | `LevelBuildKit.cs:778` |
| `X.upgrade#UpgradeStation` | buy-an-animal square | `LevelBuildKit.cs:783` |

So coop A's chicken count is stored under `farm.coopA#ProducerMachine` and its wear under
`farm.coopA#Durability` — **same GameObject, same id, different component type suffix.** That is
exactly what the `#TypeName` half of the key is for (`LevelBuildKit.cs:717-718`):

> The machine and its durability both live on the root; the type suffix in the save key keeps
> them apart.

For a counter with id `X`: `X.register` (`LevelBuildKit.cs:1067`). For a bin: `X.discard`
(`LevelBuildKit.cs:856`).

---

## 5. What each saveable component actually stores

Seven component types implement `ISaveable`. Each owns a private `State` struct — that struct's
**field names are the on-disk format**.

### `ItemBuffer` — `Stations/ItemBuffer.cs:105`
```csharp
private struct State
{
    public int count;                 // how many items are in the pile
    public double spoilAccumulator;   // fractional rot carried between ticks
    public double lastUnix;           // when this was captured
}
```
On restore it **catches up on rot that happened while the game was closed** (`ItemBuffer.cs:127-130`):
```csharp
double gap = GameClock.ClampOffline(GameClock.NowUnix - s.lastUnix);
if (gap > 0d) Spoil(gap);
```
`count` is clamped to `capacity` on load, so lowering a capacity in the builder is safe — the
excess is quietly dropped rather than corrupting anything.

### `ProducerMachine` — `Stations/ProducerMachine.cs:166`
```csharp
private struct State
{
    public float progress;    // 0..1 toward the next unit
    public double lastUnix;
    public int units;         // livestock headcount (chickens / cows)
}
```
Note the forward-compatibility handling at `ProducerMachine.cs:188`:
```csharp
// Older saves have no headcount recorded; leave the scene's starting value alone.
if (s.units > 0) units = Mathf.Clamp(s.units, 1, maxUnits);
```
**This is the pattern to copy when you add a field.** A missing field deserialises as `0`, so
the code treats `0` as "not recorded" rather than as a real value.

Offline production is deferred rather than applied in `RestoreState` — it sets
`_awaitingCatchUp = true` and `_restoredAtUnix`, and the catch-up runs on the first tick.

### `HarvestStation` — `Stations/HarvestStation.cs:105`
```csharp
private struct State
{
    public int ready;          // ripe plots
    public float growthTimer;
    public double lastUnix;
}
```
Restores and then calls `Grow(gap)` for offline regrowth. `ready` is clamped to `plots`.

### `UnlockStation` — `Stations/UnlockStation.cs:133`
```csharp
private struct State
{
    public double paid;     // money sunk into this purchase so far
    public bool unlocked;
}
```
Purchases are **partial** — you pay into a gate by standing in it, and `paid` survives. On
restore, an already-unlocked gate re-applies silently (`_announceUnlock = false`) so old
purchases do not replay their celebration popup.

This covers both the land/building gates **and** every worker hire — hires are `UnlockStation`s
too, just with a different icon (`FarmSceneBuilder.cs:592`).

### `UpgradeStation` — `Stations/UpgradeStation.cs:121`
```csharp
private struct State
{
    public int purchases;   // how many extra animals bought
    public double paid;     // partial payment toward the next one
}
```

### `MarketDecor` — `Stations/MarketDecor.cs`
```csharp
private struct State
{
    public bool painted;     // false until the first paint job is confirmed
    public DecorLook look;   // pattern, primary/secondary/tertiary, floorKind, floorColor, woodShade
}
```
On restore it shows the painted coat and re-applies the look through MaterialPropertyBlocks.
The look is only saved when committed; a menu preview is never saved.

### `ChoreStation` — `Stations/ChoreStation.cs`
```csharp
private struct State
{
    public int completed;   // rings done so far
    public bool done;
}
```
A finished chore re-applies silently on restore (hides the rubbish, reveals what it unlocks), the
same pattern as `UnlockStation`.

### `SupplyFeed` — `Stations/SupplyFeed.cs`
```csharp
private struct State
{
    public float clock;      // seconds toward the next unit
    public double lastUnix;
}
```
On restore the offline gap (`GameClock.ClampOffline`) is held and turned into units on the first
`Start()`, after the crate's own `ItemBuffer` has restored (and rotted what it held), so the
buffer's load cannot overwrite it. Capped by the buffer's capacity.

### `Durability` — `Upkeep/Durability.cs:65`
```csharp
private struct State { public float current; }
```
Clamped to `max` on load.

### `CustomerQueue` — `Customers/CustomerQueue.cs`
```csharp
private struct State { public float reputation; }
```
Clamped to `[minReputation, 1]`. The supermarket's queue (`market.checkout`) uses the same
component and the same single field; browsing shoppers themselves are never saved. The second
till (`market.checkout2`) has `sharesReputationWith` set: it reads and moves the first till's
number and ignores its own saved value on restore. Stocker claims (`WorkerAgent`) and stocker
routes are runtime-only and never saved; a stocker re-chooses its shelf the moment it loads.

### Money
Money is **not** an `ISaveable`. It is a top-level field on the blob (`SaveSystem.cs:134`) and
lives on `Wallet` (`Core/Wallet.cs`), owned by `GameRoot`.

---

## 6. When saving happens

All in `Core/GameRoot.cs`:

| Trigger | Line | Note |
|---|---|---|
| Autosave every **15 s** | `GameRoot.cs:21,52-60` | `AutosaveInterval = 15f`, on `Time.unscaledDeltaTime` |
| `OnApplicationPause(true)` | `GameRoot.cs:67` | |
| `OnApplicationFocus(false)` | `GameRoot.cs:72` | |
| `OnApplicationQuit()` | `GameRoot.cs:77` | |
| A component unregistering | `SaveSystem.cs:111` | captures that one component's state |

The comment on the interval explains why there are four of them (`GameRoot.cs:19-20`):

> Mobile Safari can kill a backgrounded tab without firing any lifecycle event, so we never
> rely solely on pause/quit.

**Practical consequence for testing:** after making progress in the browser, wait ~15 seconds
(or switch tabs) before reloading, or you will test against a stale save and think persistence
is broken when it is not.

---

## 7. Registration order — why it works without plumbing

Components call `SaveSystem.Register(this)` from `OnEnable`. `SaveSystem.cs:84-87`:

> Components call this from OnEnable. If saved state exists it is restored immediately, which
> means additively loaded levels restore themselves with no extra plumbing.

`GameRoot` has `[DefaultExecutionOrder(-10000)]` and bootstraps via
`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` (`GameRoot.cs:25-32`), so the save blob is
always loaded from disk **before** any scene object's `OnEnable` runs. You never have to think
about ordering.

A consequence worth knowing: **an object that is inactive in the scene never registers.** That
is deliberate — locked content (`Gate(...)` calls `SetActive(false)` at
`FarmSceneBuilder.cs:568`, and unhired workers at `FarmSceneBuilder.cs:601`) does not tick, does not save, and cannot be walked into before
it is bought.

---

## 8. The wipe, and the bug it was built around

`SaveSystem._wiping` exists because of a real and nasty failure. `SaveSystem.cs:33-41`:

> Deleting the save clears the stored blob, but reloading the scene then destroys every
> saveable in the old one - and unregistering captures state on the way out, which put all the
> old progress straight back. Purchases the player had partly paid off survived a wipe that
> had, as far as the player could tell, just happened.

The sequence in `GameRoot.ResetProgress()` (`GameRoot.cs:80-100`):

1. `SaveSystem.DeleteSave()` — clears the store, sets `_wiping = true`
2. Scene reloads; every old saveable is destroyed and unregisters — but `_wiping` suppresses
   the capture (`SaveSystem.cs:111`)
3. `OnWipedSceneLoaded` fires → `SaveSystem.FinishWipe()` → `_wiping = false`

**If you ever add another path that destroys saveables in bulk, it has to go through this same
guard**, or you will reintroduce the "wipe that did not wipe" bug.

---

## 9. Offline progress

`Core/GameClock.cs` is the only time source. `GameClock.cs:8-10`:

> Everything that produces, decays, spoils or pays wages asks this class for the time rather
> than using Time.time, because Time.time resets to zero on every page load and we need
> progression to survive the player closing Safari for two days.

```csharp
public const double MaxOfflineSeconds = 8 * 60 * 60;   // GameClock.cs:23
```

**8 hours.** Anything longer is clamped. `GameClock.cs:20-22`:

> Offline time is capped so that leaving the game for a month does not hand the player an
> unearned empire (and does not destroy one through decay either).

`SaveSystem.OfflineSeconds` is computed once on load (`SaveSystem.cs:67`) and exposed as a
property, but each component does its own catch-up from its own `lastUnix` — which is why a
component that was unloaded still catches up correctly.

**To change the offline cap:** `GameClock.cs:23`. Raising it makes offline income and offline
spoilage both larger — they are the same clamp.

---

## 10. Corruption handling

`SaveSystem.cs:71-77`:

```csharp
catch (Exception e)
{
    // A corrupt save must never brick the game on someone's phone.
    Debug.LogWarning($"[SaveSystem] Save file unreadable, starting fresh. {e.Message}");
    Stored.Clear();
    HasSave = false;
}
```

Per-component failures are also caught individually (`SaveSystem.cs:99-103` on restore,
`SaveSystem.cs:122-125` on capture), so one broken entry cannot take down the rest of the save.

---

## 11. WHAT YOU MUST NOT CHANGE

Read this before touching anything save-related.

### Never change, once anyone has progress

1. **`PrefsKey` — `SaveSystem.cs:16`** (`"tycoon.save.v1"`). Changing it orphans every existing
   save instantly. It is the one true version switch; see §12 if you actually want that.
2. **Any `SaveIdentity` id string in `FarmSceneBuilder.cs`** (the table in §4). The id is half
   the save key. `farm.coopA` → `farm.coop_a` loses that coop's chickens, feed, eggs, wear and
   purchase progress.
3. **The *type name* of any saveable component.** The key is `id + "#" + GetType().Name`.
   Renaming the class `ProducerMachine` → `AnimalPen` silently orphans every machine's state.
   If you must rename, you need a migration (§12).
4. **Existing field names inside a `State` struct.** `JsonUtility` matches by name. Renaming
   `count` → `amount` in `ItemBuffer.State` means every saved buffer loads as empty.
5. **The field *type* of an existing `State` field.** `int` → `float` will not deserialise
   cleanly.

### Safe to change freely

- **Adding** a new field to a `State` struct — old saves give it the type default. Use the
  `ProducerMachine.units` pattern (`ProducerMachine.cs:188`): treat the default as "absent".
- **Capacities, prices, rates, speeds.** `ItemBuffer` clamps `count` to `capacity`,
  `HarvestStation` clamps `ready` to `plots`, `Durability` clamps to `max`. Lowering a limit
  trims the save rather than breaking it.
- **Adding a new saveable object**, as long as you call `LevelBuildKit.Identify(go, "farm.…")`
  with a *new* id. Old saves simply have no entry, so it starts fresh.
- **Removing an object.** Its entry is preserved in the blob but never read. Dead weight, not
  a fault. (See §13 for why it is preserved.)
- **The GameObject's `name`** — as long as it has a `SaveIdentity`. That is the entire reason
  `SaveIdentity` exists.
- **Reparenting an object** — same reason.

---

## 12. How to make a breaking save change safely

If you genuinely need to rename an id or a component type, you have two options.

**Option A — accept the reset (what this project does today).**
This is a single-player, free, browser game with no accounts. Bumping
`SaveSystem.cs:16` to `"tycoon.save.v2"` wipes everyone cleanly and visibly, instead of
leaving half-restored saves that look like bugs. If the change is big, this is honest and
cheap.

**Option B — migrate on load.**
There is **no migration machinery in the codebase today.** If you add one, the right seam is
`SaveSystem.LoadFromDisk`, right after the blob is parsed (`SaveSystem.cs:63-64`), where
`Stored` is a plain `Dictionary<string,string>` and keys can be rewritten before anything
registers:

```csharp
// after the keys/values loop, before _loaded = true
if (Stored.TryGetValue("farm.coopA#ProducerMachine", out var v))
{
    Stored["farm.coopA#AnimalPen"] = v;
    Stored.Remove("farm.coopA#ProducerMachine");
}
```

Add a `schemaVersion` int to `Blob` at the same time (a missing one deserialises as `0`, which
means "pre-versioning") so later migrations can be conditional.

---

## 13. Why the blob is the master record, not the scene

`SaveSystem.cs:10-12`:

> The saved blob is the master record, not the loaded scene. Live components overwrite their
> own entries on save and every other entry is preserved untouched, so a level that is
> currently unloaded (the player is off running the bakery) keeps its state intact.

Today there is only one scene (`Farm.unity`), so this looks like over-engineering. It is not —
it is what lets a second level be added later without the first one's progress being erased
every time you save while standing in the second. Do not "simplify" it by rebuilding `Stored`
from scratch on save.

---

## 14. Testing save/load — the exact procedure

The quickest reliable loop, because WebGL `PlayerPrefs` is per-origin:

1. Build and serve locally (see `BUILD_AND_DEPLOY.md`), always at the **same** port.
2. Play: earn money, part-pay an unlock, collect some eggs into a buffer.
3. **Wait 15+ seconds** (autosave) or switch to another browser tab and back.
4. Reload the page.
5. Verify: money, the part-paid unlock's progress, buffer contents, chicken headcount,
   durability, which gates are open.
6. Check the console for `[SaveKeys]` warnings — any means a missing `SaveIdentity`.

**To test offline catch-up:** save, close the tab, change your system clock forward a few
minutes (the game reads wall-clock time, `GameClock.NowUnix`), reopen. Fields should have
regrown and perishables should have rotted. Anything over 8 hours is clamped.

**To test a wipe:** use the reset in the pause menu (`UI/PauseMenu.cs`), then reload and confirm
nothing came back — this is the exact bug §8 describes, and it is worth re-checking after any
change to scene loading.

---

## 15. Quick reference

| I want to… | Go to |
|---|---|
| Change the autosave interval | `Core/GameRoot.cs:21` |
| Change the offline cap | `Core/GameClock.cs:23` |
| Reset everyone's progress deliberately | `Core/SaveSystem.cs:16` — bump `v1` → `v2` |
| Add a saved field to a station | that station's `State` struct + `CaptureState`/`RestoreState` |
| Make a new object saveable | implement `ISaveable`, then `LevelBuildKit.Identify(go, "farm.newthing")` |
| Find why progress vanished | browser console → `[SaveKeys]` / `[SaveSystem]` warnings |
| Understand a save key | `Core/ISaveable.cs:38` |
