# Tiny Empire

A build-and-maintain business tycoon game for iPhone. You walk a little character around,
stand in painted squares to harvest, feed, collect, repair and sell, and spend the takings
unlocking more of the farm.

Built in **Unity 6000.1.6f1 (URP)** and shipped as a **Web build** that installs to the iPhone
Home Screen. No App Store, no developer account, no expiry.

---

## Running it

### Build the game

```bash
"D:\unity\unity engine\6000.1.6f1\Editor\Unity.exe" -quit -batchmode -nographics -projectPath D:\iosGame\game -executeMethod Tycoon.EditorTools.TycoonBuild.BuildWeb -logFile D:\iosGame\build-web.log
```

Output goes to `docs/`, which is also what GitHub Pages serves. **Stop the local test server
first** — while it is running it holds `docs/Build/docs.loader.js` open, and the build fails
with "the requested operation cannot be performed on a file with a user-mapped section open". In the editor the same thing
is on the menu: **Tycoon → Build Web** (`Ctrl+Shift+B`).

### Play it locally

```bash
npx serve D:\iosGame\docs
```

Then open the printed URL. A desktop browser works for testing — **WASD or arrow keys** move
the character, so you don't need a touchscreen.

### Regenerate the farm level

```bash
"D:\unity\unity engine\6000.1.6f1\Editor\Unity.exe" -quit -batchmode -nographics -projectPath D:\iosGame\game -executeMethod Tycoon.EditorTools.FarmSceneBuilder.Build -logFile D:\iosGame\build-scene.log
```

This rebuilds `Assets/_Project/Scenes/Farm.unity` from scratch. Editor menu: **Tycoon →
Rebuild Farm Scene**. Anything you hand-edited in the scene is lost, so once you start
polishing by hand, stop re-running it.

---

## How the game is put together

Everything in the world is either a **buffer** (a pile of goods), a **machine** (turns one
buffer into another over time), or a **station** (a square the player stands in). Stations and
machines never talk to each other directly — they only touch buffers. That is what keeps new
content cheap to add.

| Script | Role |
|---|---|
| `ItemBuffer` | A pile of one kind of goods, with a capacity. Owns spoilage. |
| `ProducerMachine` | Consumes an input buffer, fills an output buffer, wears out as it works. |
| `StationBase` | A trigger square. Subclasses move one unit per tick while you stand in it. |
| `HarvestStation` | Field that regrows and fills your arms. |
| `DepositStation` | Empties your arms into a buffer. |
| `CollectStation` | Fills your arms from a buffer. |
| `SellStation` | Turns carried goods into money. |
| `UnlockStation` | Drains money to reveal new content. Remembers partial payment. |
| `RepairStation` | Restores a machine's condition, for a price. |
| `Durability` | Condition that drops per unit produced; at zero the machine jams. |
| `CarryStack` | The stack of goods above the player's head. One kind at a time. |

Two things bootstrap themselves before the first scene loads, so **no scene needs any wiring**:

- `GameRoot` — wallet, saving, autosave
- `HudRoot` — canvas, money readout, safe area, floating joystick

Drop a ground plane and some stations into a brand new scene and it is immediately playable.

---

## Adding a new business

`Assets/_Project/Editor/LevelBuildKit.cs` has the parts. A complete production building is one
call:

```csharp
var bakery = LevelBuildKit.BuildWorkshop(
    "Bakery", root, new Vector3(6f, 0f, 2f),
    input: items.Egg, output: items.Bread,
    secondsPerOutput: 4f, inputCapacity: 8, outputCapacity: 10,
    bodyColor: new Color(0.8f, 0.7f, 0.45f), wearPerOutput: 2f);
```

That gives you the building, an input hopper, an output basket, a machine, a durability
component, and the three squares to feed it, empty it and repair it.

New goods are one more call:

```csharp
var bread = LevelBuildKit.Item("bread", "Bread", new Color(0.85f, 0.65f, 0.35f), price: 14d);
```

To put it behind a paywall, hand the building to an `UnlockStation` and **deactivate it** — a
locked plot must be inactive in the saved scene so nothing inside it ticks or saves before it
is bought:

```csharp
var gate = LevelBuildKit.Station<UnlockStation>("UnlockBakery", root, pos, size, "Bakery", colour);
gate.price = 600d;
gate.revealOnUnlock = new[] { bakery.Root };
bakery.Root.SetActive(false);
```

### Layout tip

The level root is rotated 45° so its **local +Z runs up the phone screen and local +X runs
right**. Author coordinates as if you were looking at the phone. The screen is tall and narrow,
so chain your businesses up and down the level's local Z rather than spreading them sideways.

---

## What the farm contains

Two production wings either side of a central walking corridor, with the market across the
south end. Two wings rather than one long column keeps the walk from the fields to the counter
reasonable; the camera follows the player, so only one wing is on screen at a time.

| Wing | Chain | Unlocks in order |
|---|---|---|
| Left | Corn field → coop → eggs | chickens, harvester, seller, 2nd field ($600), 2nd coop ($900) |
| Right | Hay field → cow shed → milk | hay field ($1,800), cow shed ($2,600), hay hand, milk run, 2nd shed ($6,000) |
| South | Two tills | 2nd till ($1,400) |

Coops and cow sheds are the same `BuildWorkshop` call with different items and livestock. Each
has a fenced pen on its east side — nest boxes for chickens, open pasture for cows — and buying
an animal makes one visibly appear in it.

Both tills take either product, but shoppers only ever ask for something the farm can actually
make (`ProductRegistry`), so milk starts appearing in orders by itself the moment the first cow
shed opens — and never before.

---

## Tuning the economy

Numbers live in ScriptableObjects under `Assets/_Project/Configs/`, or as inspector fields on
the stations, so balancing needs no code:

| Knob | Where | Current |
|---|---|---|
| Egg / milk price | `Item_egg.asset`, `Item_milk.asset` → `basePrice` | 5 / 12 |
| Spoil rate | `Item_*.asset` → `spoilSeconds` | 90s egg, 120s milk |
| Production speed | workshop → `ProducerMachine.secondsPerOutput` | 6s chicken, 10s cow |
| Animals per building | workshop → `ProducerMachine.maxUnits` | 3 |
| Next animal price | workshop → `UpgradeStation.basePrice` × `priceGrowth` | 120 × 2.2 |
| Wear per unit | workshop → `Durability.wearPerOutput` | 1.5 |
| Repair cost | workshop → `RepairStation.costPerPoint` | 0.25 |
| Shopper arrivals | counter → `CustomerQueue.spawnIntervalSeconds` | 5s |
| Shopper patience | `CustomerAgent.patienceSeconds` | 75s |
| Carry capacity | `Player` → `CarryStack.capacity` | 8, mixed goods |
| Worker pay | hire → `WorkerAgent.feePerDelivery` | 0.5–1.4 per unit delivered |

Production is paced against demand rather than picked arbitrarily: a till takes a shopper every
5s wanting 1–4 items, so one counter absorbs roughly 30 items a minute. One chicken at 6s an egg
makes 10 — visibly short, which is what makes the second chicken worth buying.

---

## Customers

Eggs are only worth money when somebody is at the counter asking for them. Shoppers walk in
along the road, queue at the stall, hold up a bubble showing what they want (`🥚 x3`) and a
patience bar, and leave — happy or not.

- `CustomerQueue` spawns them, manages queue slots and owns **reputation**.
- `CustomerAgent` walks the route, holds the order, runs down its patience.
- `RegisterStation` serves the front customer one unit at a time, paying as it goes.
- `OrderBubble` is the world-space bubble. Assign a sprite to `ItemDefinition.icon` and every
  bubble for that item uses your art instead of the placeholder dot.

This is what stops "pile up stock" being a winning strategy: production has to track demand.

---

## Why the business never "finishes"

Building everything out is not the end of the level. Four pressures keep a completed farm
demanding attention:

1. **Wear** — machines lose condition per unit produced and jam at zero.
2. **Spoilage** — perishable goods rot in buffers, so hoarding output loses money.
3. **Reputation** — shoppers who give up and walk out thin the queue *and* cut the price
   everything sells for (`CustomerQueue.PriceMultiplier`). It recovers only by serving people.
4. **Piece rates** — hired workers take a cut of every unit they deliver, so automation is a
   running cost that scales with throughput.

Progress is applied while the game is closed too: production accrues, and so does decay.
Offline time is capped at 8 hours in `GameClock.MaxOfflineSeconds`.

### Two deadlocks that are deliberately designed out

Both of these were real and both would have bricked a save permanently:

- **Workers are paid per delivery, never per minute.** Hourly wages drain the wallet to zero,
  at which point workers stop — and if the stopped worker was the one carrying goods to the
  counter, nothing can ever earn money again. A fee taken from work that already happened
  cannot run away.
- **Repair always works with an empty wallet.** A jammed machine plus no money would otherwise
  be terminal: no repair, no production, no income, no repair. Money buys speed
  (`RepairStation.freeRepairFraction` guarantees 25% of the rate for free). Never set it to zero.

The general rule for anything added later: **no mechanic may require money to escape a state
where you cannot earn money.**

---

## Putting it on a phone

1. Push this repo to GitHub.
2. Settings → Pages → Source: **Deploy from a branch**, branch `main`, folder `/docs`.
3. Open the `https://<user>.github.io/<repo>/` link in Safari on the iPhone.
4. Share → **Add to Home Screen**.

It then launches fullscreen with its own icon, works offline, and keeps progress on the device.

**Build settings that matter** (all set in code in `TycoonBuild.cs`):

- **Gzip compression + decompression fallback** — GitHub Pages cannot send a `Content-Encoding`
  header. Brotli without that header is a silent blank page. This is the single most common way
  a Unity Web build fails on a static host.
- **Device pixel ratio capped at 2** (in `index.html`) — an iPhone reports 3, and rendering 3D
  at native 3x on a phone GPU is the fastest way to 20fps.
- **Explicit-only exception support** and **High managed stripping** — keeps the download small.

---

## Known limitations

- Art is greybox primitives. `ItemDefinition.visualPrefab` and the materials under
  `Assets/_Project/Materials/` are the swap-in points; no game logic touches art.
- Text uses Unity's legacy `Text`/`TextMesh` rather than TextMeshPro, because TMP needs its
  "Essential Resources" imported interactively and that cannot be done from a headless build.
- `SaveKeys` are hierarchy paths, so renaming or reparenting a station resets that one
  object's saved state.
- Offline catch-up applies spoilage to buffers before machines produce, so a long absence is a
  slight approximation rather than an exact replay.
- No audio yet.
