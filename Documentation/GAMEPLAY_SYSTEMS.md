# GAMEPLAY SYSTEMS

How the game actually works, system by system, and — more importantly — **how the systems
constrain each other**. Read `PROJECT_STRUCTURE.md` first if you have not.

Every value here was read from the code. Paths are relative to `game/Assets/_Project/`.

---

## 1. The one idea everything is built on

> **Standing somewhere does the work. There is never a button to press.**

`Stations/StationBase.cs:40-41`:

> Base class for every "designated square" in the game. Standing in one does the work - there
> is never a button to press.

And the reason the same class serves everyone (`StationBase.cs:43-45`):

> The same class serves the player and hired workers. A worker is simply something with a
> `CarryStack` that walks into the trigger, which is why employees need no parallel gameplay
> logic anywhere in the project.

That is the whole architecture in two sentences. **If you remember one thing, remember this: a
station does not know whether it is serving the player or a worker.** It looks for a
`CarryStack` on whatever entered its trigger, and that is the entire test.

```csharp
// StationBase.cs:210
var carry = other.GetComponentInParent<CarryStack>();
if (carry == null) return;
bool isPlayer = carry.CompareTag("Player");
```

**Consequence:** any station type you add in future works with workers automatically, with zero
extra code. Do not write worker-specific branches in a station.

---

## 2. StationBase — the contract

`Stations/StationBase.cs` (341 lines). Read it before changing anything in `Stations/`.

### Two interaction modes

```csharp
public enum InteractionMode { Transfer, Task }   // StationBase.cs:23
```

| Mode | Meaning | Key field | Used by |
|---|---|---|---|
| `Transfer` (default) | moves **one unit at a time** while someone stands there | `tickInterval = 0.18f` | harvest, feed, collect, sell, discard |
| `Task` | a job with a duration: enter, fill a ring, get a result | `taskDuration = 1.2f` | repair |

Why `Transfer` is one unit at a time (`StationBase.cs:26-28`):

> This is the signature feel of the genre: the carried stack visibly grows item by item, and
> the player can walk away with a partial load.

### Tunable fields and real defaults

| Field | Default | Meaning |
|---|---|---|
| `label` | `""` | the word on the square |
| `zoneColor` | `(0.3, 0.8, 1, 0.55)` | the square's colour |
| `mode` | `Transfer` | see above |
| `tickInterval` | `0.18f` | seconds per unit moved |
| `taskDuration` | `1.2f` | seconds per task completion |
| `cooldown` | `0f` | wait before repeating |
| `playerCompatible` | `true` | |
| `workerCompatible` | `true` | |
| `playerPriority` | `true` | see §3 |

### Occupancy

Multiple actors can stand in one square at once. Each gets its own `Occupant` record with its
own `TransferTimer`, `TaskTimer` and `CooldownLeft` — so one actor's progress is never another's.

### Player priority — and why it exists

`StationBase.cs:74-79`, quoted in full because it is the clearest statement of the design:

> While the player is standing here, hired hands stop working and stand off. Without this the
> two compete for the same goods: a worker emptying the egg basket takes an egg every 0.18 s,
> so a player who walks up to collect finds the basket drained under them and has to wait for
> the worker to leave. The player is meant to be the fastest thing on the farm, not something
> the staff queue in front of.

It is enforced in **two independent places**:

1. `StationBase.Update()` zeroes non-player occupants' timers — *paused, not ejected*
   (`StationBase.cs:260-269`):

   > Paused, not ejected. The worker keeps its place, its carried stack and its timers; it
   > simply does nothing until the player is done. Nothing is created or destroyed here, so a
   > worker can neither lose goods nor duplicate them by being interrupted.

2. `WorkerAgent` physically walks away (§5).

The public surface is one property, deliberately generic:

```csharp
public bool ReservedForPlayer => playerPriority && HasPlayer;   // StationBase.cs:110
```

> Exposed as a plain property so it works for any station type, present or future, without a
> line of per-station code.

### What a square displays

Four virtuals, split apart deliberately (`StationBase.cs:156-176`):

| Member | Example | Purpose |
|---|---|---|
| `ActionLabel` | `FEED`, `FIX` | the verb/noun word (upper-cased by the UI) |
| `StatusValue` | `3/10`, `22%`, `$600` | the live number |
| `Icon` | `SquareIcon.Feed` | action icon |
| `IconItem` | the egg `ItemDefinition` | product icon — **wins over `Icon`** |

Why they are separate (`StationBase.cs:158-162`):

> These used to be a single composed string, which is why labels grew into things like "Hire
> Milk Cashier 2 $2.4K" and ran off the side of the square and behind buildings. [...] the icon
> carries the verb, so the text only has to carry the noun.

### Subclass hooks

```csharp
protected virtual bool TickWithPlayer();      // Transfer: move one unit. false = nothing happened
protected virtual bool CanPerformTask();      // Task: false pauses the ring
protected virtual void CompleteTask();        // Task: fires when the ring fills
protected virtual void TickAlways(float dt);  // runs whether or not anyone is present
```

### A trap already hit, that you will hit again

`LevelBuildKit.cs:436-441` — the collider ordering bug:

> Adding a StationBase in the editor fires its `Reset()`, which stamps the collider back to a
> default two metre cube. Setting the size first therefore did nothing: half the farm ended up
> with a 2x2 trigger under a 5.5x3.5 outline, so a player standing on most of a corn field was
> outside the square that told them to stand there.

**Always `AddComponent<T>()` first, then size the collider.** `SquareAudit` now checks this
invariant directly.

---

## 3. IStationUser — how workers stay on their route

```csharp
public interface IStationUser { bool WillUse(StationBase station); }   // StationBase.cs:17
```

`WorkerAgent` implements it as one line:

```csharp
public bool WillUse(StationBase station) => station == pickup || station == dropoff;
```

**The player implements nothing and may therefore use every square.** `StationBase.cs:15`:

> The player implements nothing and may use everything, which is the whole point of them.

Why it exists (`StationBase.cs:10-13`):

> Previously a worker walking past the corn field with its arms full of eggs was refused by
> type, which accidentally kept workers on their route. With a mixed stack there is nothing to
> refuse, so a seller would wander into the field and fill up with corn it has nowhere to take.

---

## 4. The player

`Player/PlayerMotor.cs`, `Player/PlayerInputSource.cs`, `Player/CarryStack.cs`.

### Movement
`CharacterController`-based. **The player never touches the NavMesh.**

| Value | Where |
|---|---|
| `moveSpeed = 5.2f` | set by the builder, `FarmSceneBuilder.cs:820` (class default is 5.5) |
| `acceleration = 40f` | `PlayerMotor.cs`, not overridden |
| `turnSpeed = 900f` deg/s | `PlayerMotor.cs`, not overridden |
| controller `radius 0.38`, `height 1.5` | `FarmSceneBuilder.cs:799-803` |

Movement is **camera-relative** — "up" on the stick is up the screen whatever the camera yaw.

A recorded WebGL-specific fix, `PlayerMotor.Update()`:

> The first frame after a Web build finishes loading can be several seconds long. Feeding that
> straight into `Move()` would displace the character tens of metres in one step, which tunnels
> straight through the ground collider and drops them out of the world. Clamping the step is
> the fix.

```csharp
float dt = Mathf.Min(Time.deltaTime, 0.05f);
```

### Input
`PlayerInputSource` is a **static** class, not a component. It merges:
- `VirtualJoystick` (touch) → `PlayerInputSource.Joystick`
- Keyboard WASD **and** arrow keys, which **override** the joystick when pressed

> The keyboard path exists so the game can be driven and tested in a desktop browser during
> development; on the phone only the joystick ever writes here.

### Where the player starts
`FarmSceneBuilder.cs:67` — level-local `(EggTill, 0.2, -11)`, i.e. `(-10.5, 0.2, -11)`:

> Between the first coop's collect square and the egg till, so the opening shot shows a
> complete chain - birds above, counter below - without the player having to walk anywhere to
> find out what the game is.

---

## 5. CarryStack

`Player/CarryStack.cs`. **Both the player and every worker have one. Customers do not.**

| Who | Capacity | Set at |
|---|---|---|
| Player | **8** | `FarmSceneBuilder.cs:824` |
| Worker | **4** | `LevelBuildKit.BuildWorker` default parameter, `LevelBuildKit.cs:626` |
| Customer | — | has none, so a customer can never trigger a station |

### Mixed goods — a softlock fix, not a convenience

`CarryStack.cs:8-17`:

> The stack holds a MIXTURE of goods rather than one kind at a time. Restricting it to a single
> type created dead ends: carrying corn with a full feed hopper left the player unable to
> deposit it, unable to pick up eggs, and unable to sell it - stuck with no way to empty their
> hands. A mixed stack removes that whole class of problem, and it means the player can run a
> delivery and a collection on the same trip.

```csharp
public bool CanAccept(ItemDefinition candidate) => candidate != null && !IsFull;
// "Deliberately independent of what is already held - only total capacity matters now."
```

`TryRemove(expected)` takes the **topmost** unit of that type. Visual slots are recomputed every
frame so the stack closes up when a unit is taken from the middle.

**The escape hatch** when your arms hold something useless is the bin (§10). That plus mixed
stacks is what makes "no mechanic may require money to escape a state where you cannot earn
money" hold.

---

## 6. Buffers and machines

The three-noun model, from `README.md` and confirmed in code:

> Everything in the world is either a **buffer** (a pile of goods), a **machine** (turns one
> buffer into another over time), or a **station** (a square the player stands in). Stations
> and machines never talk to each other directly — they only touch buffers.

### `ItemBuffer`
A pile of one item type with a `capacity`. Owns **spoilage** — perishable goods rot in place.
Saveable, and catches up on offline rot when loaded.

### `ProducerMachine`
Consumes `input`, fills `output`, over `secondsPerOutput`, wearing `Durability` as it goes.
Holds the **livestock headcount** (`units`, up to `maxUnits`). Saveable, with deferred offline
catch-up.

It also implements `IProducer`, which is what registers it with `ProductRegistry` (§8).

---

## 7. The production chains as actually built

Four short columns side by side on one street. `FarmSceneBuilder.cs:168-173` draws it:

```
    corn      corn            meadow     meadow
    coop      coop            shed       shed
       egg till          bin        milk till
    =================== road ====================
```

### Chain 1 and 2 — eggs

```
CornFieldA / CornFieldB     (HarvestStation, 8 plots, regrow 2.2s)
        ↓  player or Farmer carries corn
CoopA.Feed                  (DepositStation → input ItemBuffer, cap 10)
        ↓
CoopA ProducerMachine       (6s per egg, per chicken; 1–3 chickens)
        ↓
CoopA output buffer         (cap 12)
        ↓
CoopA.Collect               (CollectStation)
        ↓  player or Cashier carries eggs
CounterA.Register           (RegisterStation, sells ONLY Egg)
        ↓
Customer                    →  money
```

### Chain 3 and 4 — milk

```
HayFieldA / HayFieldB       (HarvestStation, 8 plots, regrow 2.6s, Meadow style)
        ↓  player or Hay Hand carries hay
CowShedA.Feed               (input buffer, cap 10)
        ↓
CowShedA ProducerMachine    (10s per milk, per cow; 1–3 cows)
        ↓
CowShedA output buffer      (cap 10)
        ↓
CowShedA.Collect
        ↓  player or Milk Run carries milk
CounterB.Register           (RegisterStation, sells ONLY Milk)
        ↓
Customer                    →  money
```

### Why the layout is this shape

`FarmSceneBuilder.cs:160-183` — it used to be two long wings:

> Every single loop - cut the corn, feed the birds, take the eggs, sell them - was a thirty
> metre walk down the wing and a thirty metre walk back up it, and because the fields and the
> tills were at opposite ends of the map the player spent most of the game in transit rather
> than deciding anything.

And the spacing is not free choice (`FarmSceneBuilder.cs:185-190`):

> A column reaches 5.15 m WEST of its centre (the repair and buy squares) and, for a cow shed,
> 4.1 m EAST (the pasture) - just over nine metres of occupied ground. Anything under about
> 9.3 m of spacing overlaps its neighbour, which the layout audit now measures rather than
> leaving to the eye.

`ColumnSpacing = 10.5f`. Columns at x = −15.75, −5.25, +5.25, +15.75.

### The item definitions

`FarmSceneBuilder.cs:125-149`:

| Item | Base price | Perishable | Spoil | Stack height |
|---|---|---|---|---|
| Corn | 1 | no | — | 0.30 |
| Egg | **5** | **yes** | 90 s | 0.26 |
| Hay | 1 | no | — | 0.32 |
| Milk | **12** | **yes** | 120 s | 0.30 |

> Perishables rot, so hoarding output loses money. It is the gentlest of the upkeep pressures
> and the first one players notice.

> [Milk is] Slower to make and worth far more than an egg, so the dairy wing is a genuine step
> up rather than a reskin of the chickens.

---

## 8. ProductRegistry — what the farm can make

`Core/ProductRegistry.cs`. A static list of `IProducer`. `ProducerMachine` registers itself in
`OnEnable` and unregisters in `OnDisable`.

Because locked buildings are `SetActive(false)`, **a building that has not been bought announces
nothing.** That is what stops milk customers appearing before a cow shed exists:

> a counter that lists milk would start sending milk customers before the player owns a single
> cow - orders that can only ever time out, wrecking reputation for no reason.

`Capacity(item)` sums `units` across all matching producers — which is what customer demand is
pegged to, so **buying a chicken is felt at the counter immediately**, with no notification
plumbing anywhere.

---

## 9. Customers

`Customers/CustomerQueue.cs`, `Customers/CustomerAgent.cs`.

### Lifecycle

```
Spawn (at the far end of the street)
  ↓  Phase.Arriving — walks in at 2.7 m/s. Patience does NOT tick yet.
Reaches its queue slot
  ↓  Phase.Waiting — patience ticks down from 75s. Bubble shows icon + ×N + patience bar.
Served by RegisterStation, one unit per 0.18s
  ↓            (or patience hits zero)
Phase.Leaving — "THANKS!" (green) or "LEFT ANGRY" (red)
  ↓  walks to the exit at the opposite end
Destroy
```

### Real numbers

| Thing | Value | Where |
|---|---|---|
| Walk speed | `2.7f` m/s | `CustomerAgent.cs` |
| **Patience** | **`75f` s** | `CustomerAgent.cs`, not overridden anywhere |
| Order size | 1–4 | `CustomerQueue.minOrder/maxOrder` |
| Queue length | **3** per till | `AddCounter(queueLength: 3)` |
| Base spawn interval | `2.5f` s | `FarmSceneBuilder.cs:239` |
| Demand per capacity | `0.35f` | `FarmSceneBuilder.cs:240` |
| Max demand | `3f` | `FarmSceneBuilder.cs:241` |

Patience is generous on purpose:

> a queue that times out faster than the player can walk the length of the farm punishes them
> for playing it as designed.

### Demand scales with the farm

```csharp
DemandMultiplier = Clamp(1 + demandPerCapacity * max(0, capacity - 1), 1, maxDemand)
interval = spawnIntervalSeconds / (DemandMultiplier * max(0.2, reputation))
```

With one hen at full reputation: 2.5 s. At minimum reputation: 10 s. At max demand: ~0.83 s.

### Reputation

| Field | Value |
|---|---|
| `startingReputation` | 1.0 |
| `reputationPerHappy` | +0.05 |
| `reputationPerAngry` | **−0.14** |
| `minReputation` | 0.25 |
| `PriceMultiplier` | `Lerp(0.6, 1.15, reputation)` |

Angry customers cost nearly three times what a happy one gains. Reputation hits **both** the
spawn rate and the price everything sells for. This is the main reason hoarding stock is not a
winning strategy.

### One till per product — load-bearing

`FarmSceneBuilder.cs:256-263`:

> Both counters used to share a menu of everything the farm makes. Nothing carries milk to the
> egg counter [...] so a milk shopper sent to counter A could only ever be served by the player
> personally walking it across the farm. Worse, the queue only offers its head to the till, so
> that one stranded shopper blocked every egg customer behind them for a full 75 seconds. It
> read as the game randomly deciding to punish you.

```csharp
counterA = AddCounter(..., new[] { items.Egg }, ...);    // FarmSceneBuilder.cs:267
counterB = AddCounter(..., new[] { items.Milk }, ...);   // FarmSceneBuilder.cs:270
```

**⚠ The head-of-line blocking mechanism itself is still present.** `CustomerQueue.Front` returns
`null` rather than scanning past a customer who is not ready to be served. One product per till
means nothing can get stranded, so it never fires — but **giving any till a multi-item `sells`
array reopens the original bug in full.** Do not do that without also fixing `Front`.

A second, milder consequence remains: a front customer still walking in makes the till
momentarily unservable, and every departure re-stalls the new head while they step forward.

### Build-time safety net

`FarmSceneBuilder.WarnIfRouteBroken()` (`FarmSceneBuilder.cs:471`) logs a warning at scene-build
time if a worker carries goods to a till whose `sells` does not include them:

> This is the exact mistake that made milk shoppers unservable [...] It is invisible in the
> editor and only shows up as a customer standing at a counter forever.

---

## 10. Workers

`Upkeep/WorkerAgent.cs`. Eight of them, each one leg of one chain.

### The state machine — there isn't an enum

```csharp
if (_carry.IsFull) _headingToDropoff = true;
else if (_carry.IsEmpty) _headingToDropoff = false;
```

> Self-correcting: full means deliver, empty means go and fetch, anything in between means carry
> on with whatever leg is already underway.

| State | When | Behaviour |
|---|---|---|
| Fetch | stack empty | paths to `pickup`, stands in it, the station fills the stack |
| Deliver | stack full | paths to `dropoff`, stands in it, the station drains the stack |
| Standing off | target `ReservedForPlayer` | walks 2.9 m back down its own route, waits 0.8 s after the player leaves |
| Idle | target `!isActiveAndEnabled` | stands still (route half-unbought) |
| Off-navmesh fallback | not on NavMesh | beelines straight at the target |

### Movement
Real `NavMeshAgent`: `radius 0.3`, `height 1.3`, `acceleration 24`, `autoBraking false`,
`speed = moveSpeed = 3.1f`, `stoppingDistance = 0.35`.

> Sliding along a wall is not navigation - a worker pressed against the side of a coop just
> stays there. A NavMeshAgent actually routes around buildings, and handles workers avoiding
> each other for free.

### How workers get paid - they don't (hire price only)

Workers now cost nothing to run: every `feePerDelivery` is 0. The mechanism below is kept (inert at 0) and is why a per-minute wage was rejected.

```csharp
int delivered = _lastCarryCount - count;
if (delivered <= 0 || !_headingToDropoff || feePerDelivery <= 0d) return;
wallet.TrySpend(feePerDelivery * delivered);
```

> The station does the actual transfer, so the worker detects its own deliveries by watching its
> carried count drop while on the delivery leg. That keeps stations completely unaware that
> workers exist.

And why piece rates rather than wages (history - fees are now 0, so neither applies):

> Paying by the minute looked reasonable but deadlocks the game: once the wallet empties the
> workers stop, and if a stopped worker was the one taking goods to the counter then nothing can
> ever earn money again. Charging per delivery means pay is always a cut of work that just
> happened, so the balance can never run away.

**Short pay never stops work.** That is the point.

### The eight workers

| Role / id | Pickup | Dropoff | Fee/unit | Real price |
|---|---|---|---|---|
| Farmer / `HarvesterA` | CornFieldA | CoopA.Feed | 0 | 250 |
| Cashier / `SellerA` | CoopA.Collect | CounterA | 0 | 400 |
| Farmer 2 / `HarvesterB` | CornFieldB | CoopB.Feed | 0 | 700 |
| Cashier 2 / `SellerB` | CoopB.Collect | CounterA | 0 | 800 |
| Hay Hand / `HayHandA` | HayFieldA | CowShedA.Feed | 0 | 1500 |
| Milk Run / `MilkRunA` | CowShedA.Collect | CounterB | 0 | 1700 |
| Hay Hand 2 / `HayHandB` | HayFieldB | CowShedB.Feed | 0 | 2200 |
| Milk Run 2 / `MilkRunB` | CowShedB.Collect | CounterB | 0 | 2400 |

All defined in `FarmSceneBuilder.cs:370-410`. **All prices currently flattened to $10** — see
`PROJECT_STRUCTURE.md` §11.

### Where hire squares go, and why

Two helpers, both with recorded rationale:

- `NorthOfField()` (`FarmSceneBuilder.cs:509`) — behind the field, measured off the field's own
  trigger. *"standing in a hire square spends money, so anything on a route the player walks
  dozens of times a session is a tax on walking past."*
- `AtTill()` (`FarmSceneBuilder.cs:543`) — out beyond the columns. *"the first hire square sat
  directly underneath the first coop and quietly ate the takings on the way past."*

---

## 11. Progression and unlocks

`Stations/UnlockStation.cs`. **Hires are `UnlockStation`s too** — same type, different icon.

Payment is **partial and persistent**: you stand in the square, money drains at `payPerTick`, and
walking away keeps what you have paid.

### The gates, in intended order

| Gate | Label | Real price | Reveals |
|---|---|---|---|
| `farm.unlock.cornB` | `Field` | 600 | CornFieldB |
| `farm.unlock.coopB` | `Coop` | 900 | CoopB **+ HarvesterB + SellerB hires** |
| `farm.unlock.counterB` | `Dairy Till` | 1400 | CounterB |
| `farm.unlock.cowA` | `Dairy` | 3200 | CowShedA **+ HayFieldA + HayHandA + MilkRunA** |
| `farm.unlock.hayB` | `Meadow` | 4500 | HayFieldB |
| `farm.unlock.cowB` | `Cow Shed` | 6000 | CowShedB **+ HayHandB + MilkRunB** |

### The two rules the bundling encodes

**Rule 1 — a hire is only sold once its destination is standing.** `FarmSceneBuilder.cs:412-417`:

> A gate reveals its building and BOTH hire squares whose workers end their round trip at it. A
> worker delivering into a building that does not exist yet walks to where it will be, finds no
> trigger, and stands there full for ever - so a hire is only ever offered once its destination
> is standing.

**Rule 2 — the meadow is sold with the first cow shed, to close a softlock.**
`FarmSceneBuilder.cs:428-441`:

> Hay has exactly one place it can go - a cow's feed hopper - so a player who bought the field
> first could fill their arms with hay that had nowhere to be put down. A full stack cannot pick
> up eggs, eggs are the only income, and the shed costs money: no way out except starting over.

Note that the second meadow (`hayB`) *is* sold separately, because by then a shed that eats hay
already exists.

### How locked content is hidden

`Gate()` calls `go.SetActive(false)` on everything it will reveal. An inactive object does not
tick, does not save, does not register with `ProductRegistry`, and has no trigger to walk into.
One line, four correct behaviours.

---

## 12. Condition, repair and the no-dead-end rule

`Upkeep/Durability.cs`, `Stations/RepairStation.cs`, `Upkeep/AlertBeacon.cs`,
`UI/AttentionMarker.cs`.

- A machine loses `wearPerOutput` condition per unit produced (0.75 for coops, 1.0 for sheds - halved so a coop with 3 hens takes about 4.5 minutes to jam).
- At zero it **jams**.
- Repair is a `Task`-mode station: stand in `FIX`, a ring fills, `+N%` popups rise, `FIXED`.
- `RepairStation.costPerPoint` — 0.25 for coops, 0.35 for sheds.

### The rule that governs this and everything else

> **No mechanic may require money to escape a state where you cannot earn money.**

Three things exist only because of it:

1. **Repair always works at $0.** `RepairStation.freeRepairFraction` guarantees a fraction of
   the repair rate for free. Money buys *speed*, not permission. **Never set it to zero.**
2. **Workers have no running cost** (hire price only; never a per-minute wage - §10).
3. **The carry stack holds mixed goods**, and there are three bins (§5).

Anything you add that charges the player must be checked against this rule.

---

## 13. The four pressures that keep a finished farm interesting

1. **Wear** — machines jam at zero condition.
2. **Spoilage** — eggs rot in 90 s, milk in 120 s, so stockpiling loses money.
3. **Reputation** — timed-out customers thin the queue *and* cut the price of everything.
4. **Hire cost** - automation is paid for once, up front; there is no running cost.

All four apply **offline too**, clamped to 8 hours (`GameClock.MaxOfflineSeconds`).

---

## 14. The UI layer

Covered fully in `EDITING_GUIDE.md`; the architectural points:

- **There is not a single UI prefab or authored canvas in the project.** All UI is built in C# at
  runtime. A new scene needs no UI wiring at all.
- **No TextMeshPro, deliberately.** TMP needs its "Essential Resources" imported interactively,
  which a headless build cannot do. Everything uses Unity's legacy `Text` with
  `LegacyRuntime.ttf`.
- **Because the legacy font has no emoji glyphs, every icon is drawn in code** by
  `UI/IconFactory.cs` — a small software rasteriser using signed distance fields.
- **Runtime-created *materials* render magenta in a URP web build. Runtime-created *sprites,
  textures and fonts* are fine.** This is recorded in three separate files and has cost real
  debugging time. Never create a `Material` at runtime.
- **World-space canvases are culled when off screen** (`UI/WorldUi.cs`). A full farm has ~70
  canvases and the camera shows a handful; a canvas that changes anything rebuilds its mesh on
  the main thread.
- The HUD carries **state readouts and the joystick only**. The one menu allowed is the pause
  panel, because sound and delete-save have no physical place in the world.
  The task card's two tabs (Problems / Goals) are the one other exception: they only choose which
  list the arrow follows.

### The task list and guide arrow

`Tasks/TaskBoard.cs` sweeps the world every 0.25 s into three lists; `UI/TaskPanel.cs` draws up to
three lines, `Tasks/GuideArrow.cs` draws one arrow. Nothing here changes game state except the
`tycoon.arrow` PlayerPrefs key (which tab is followed) and the tutorial step.

- **Tutorial (green):** harvest -> feed -> collect eggs -> sell -> bin, driven by
  `TutorialEvents.Raise(...)`, one line in each of the five stations (player only, workers ignored).
  Step saved by `TutorialProgress` as `game.tutorial`. Old saves with money or a purchase skip it.
  A step that needs goods the player is not holding points at where to get them.
- **Problems (red):** `AlertBeacon` flags (jammed / worn -> repair square, out of feed -> feed
  square, output full -> collect square), plus an empty market shelf -> its stocking square.
  Jams first, then nearest.
- **Goals (yellow):** the purchases and renovation steps that are live right now (a locked
  plot's squares are inactive until its gate is paid, so the live set is the next layer of the
  progression). Free steps first, then cheapest. The second is shown faded.
- **Arrow:** off-screen target -> pinned to the safe-area edge, avoiding the money readout,
  pause button and card; on-screen -> bounces above the square. A target in the other location
  is replaced by the travel square. Hidden while the player stands in the target.


---

## 15. Camera

`Core/IsometricCameraRig.cs`, configured at `FarmSceneBuilder.cs:768-792`.

| Setting | Value |
|---|---|
| Projection | **orthographic** |
| `pitchYaw` | **`(50, 75)`** |
| `orthographicSize` | `9.5` |
| `distance` | `30` (clipping only — orthographic) |
| `lookOffset` | `(0, 0, 0.8)` |
| `smoothTime` | `0.18` |

**Pitch is load-bearing for readability** (`FarmSceneBuilder.cs:781-785`):

> a ground square is squashed to sin(pitch) of its true height on screen. At 30 degrees that is
> half, which made the squares hard to read and let buildings hide the ones behind them. 50
> degrees keeps a clear view down onto every square while still showing the fronts of the
> buildings.

**Changing the pitch invalidates** `InteractionSquare.DefaultMinCard`, `ContentSize`, and every
font-size fraction in the square layout. Do not change it casually.

Yaw is flagged in the code as the tuning dial:

> Yaw is 30 degrees off the level grid (which sits at 45). That offset is what makes buildings
> show two faces instead of presenting flat-on, and it is the single value to change if the
> viewing angle needs tuning.

There are **no camera bounds.** Instead: a 160 × 160 ground plane, invisible boundary walls at
the fence line, and `SquareAudit` proves the camera can never see the ground's edge at aspect
ratios 0.46 / 1.0 / 1.8 / 2.4.

---

## 16. Navigation

**The NavMesh is baked at runtime, every launch.** `Core/NavigationBaker.cs` is 29 lines:
`[DefaultExecutionOrder(-5000)]`, `Awake()` calls `surface.BuildNavMesh()`.

> Baked at runtime rather than stored as an asset because the level itself is generated by
> script: a baked NavMeshData asset would have to be regenerated and committed every time the
> layout moved by a metre, and would silently go stale if anyone forgot. A farm-sized level
> bakes in a few milliseconds.

The surface uses **physics colliders**, not render meshes — so the ground and boundary walls
define the walkable area and buildings punch holes in it.

**Buildings unlocked later do not need a rebake**: each workshop body carries a carving
`NavMeshObstacle`.

| Agent | Navigation |
|---|---|
| Player | `CharacterController`. Never touches the NavMesh. |
| Worker | `NavMeshAgent`, with a straight-line fallback if off-mesh |
| Customer | **Custom steering** — writes `transform.position` directly. No agent, no collider. |

---

## 17. Characters and animation

One animator controller, `Animation/Villager.controller`, shared by everyone.
`Characters/CharacterVisual.cs` drives it:

```csharp
enum Motion { Idle = 0, Walk = 1, CarryIdle = 2, CarryWalk = 3 }   // → animator int "State"
```

It is **a watcher, not a controller** — it measures speed from transform movement rather than
reading any motor:

> That means the player, workers and customers all animate correctly without a single line
> changing in PlayerMotor, WorkerAgent or CustomerAgent.

Customers have no `CarryStack`, so they only ever reach `Idle` / `Walk`.

Two settings that exist because of real failures: `Animator.cullingMode = AlwaysAnimate` and
`SkinnedMeshRenderer.updateWhenOffscreen = true` — *"wrong bounds make it vanish at the edges"*
and *"a saving worth far less than a farm full of statues."*

See `CHARACTERS_AND_MODELS.md` for the model pipeline.

---

## 18. Dead and latent things worth knowing

- **`UI/InfoSign.cs` (174 lines) is dead code.** Nothing instantiates it. It is the only
  consumer of `StationBase.StatusText`. `LevelBuildKit.cs:454-456` explains why it was dropped:
  *"No floating sign here on purpose: the square itself now carries the label and the numbers."*
- **`Stations/SellStation.cs`** turns carried goods straight into money with no customer. It is
  not used in the current farm (the tills use `RegisterStation`), but it works.
- **`UI/OrderBubble` is the one world-canvas component without `hideFlags = DontSave` and
  without a stale-child sweep.** It gets away with it only because the customer template is
  inactive at build time. Making that template active would create orphaned geometry.
- **Two stale comments:** `OrderBubble.cs:145` says the bar is 120 px (it is 148);
  `AttentionMarker.height` defaults to 4.05 but is always 4.35 in practice. Harmless.

---

## 19. The supermarket ("Tiny Market") — the second location

The first version of a second business. Everything below is built from parts that already
existed (stations, buffers, `CarryStack`, `WorkerAgent`, `CustomerQueue`); the new pieces are
listed at the end. It is generated by `Editor/MarketBuilder.cs`, written in the same style as
`FarmSceneBuilder.cs`: every layout and economy number is a commented constant at the top.

### Locations

`Core/Location.cs` (id, display name, `spawnPoint`) marks the root of a place. Two exist: the
`Farm` root and the `Market` root. They are separate root objects in the **one scene**, both
turned the same 45 degrees (so "up the screen" is local +Z in each), the market standing 75 m
down the farm's road axis (`FarmSceneBuilder.MarketOffsetX`) behind its own invisible walls and
fence. **The only way between them is the pair of travel squares.** `SquareAudit` iterates every
`Location`, so a third business is audited the moment its root has one.

The market root is saved **inactive**, exactly like a locked building: it does not tick, save or
exist until the farm gate is paid. Its walls therefore carry carving `NavMeshObstacle`s - the
runtime navmesh bake never sees an inactive root, and the obstacles cut the holes in the moment
it switches on (the same trick a coop uses). The visible ground is a second, much larger plane
(`GroundHorizon`, no collider) so the camera never sees the edge of the world from the market;
the walkable, baked ground is still the original 160 m plane.

### Travel

`Stations/TravelStation.cs` - a Task-mode square (1.2 s of standing, so brushing past never
fires it), player only. On completion `UI/ScreenFade` fades to black, the `CharacterController`
is switched off, the player is placed at the destination `Location.spawnPoint`, the controller
comes back, `IsometricCameraRig.SnapToTarget()` jumps the camera (it must not pan 75 m), and the
screen fades back. `ScreenFade` is a full-screen black `Image` on the HUD canvas - alpha only,
never blocks input, so it is state display rather than a HUD button.

The spawn point must be clear of every square (landing in the one that brought you would send
you straight back); `SquareAudit` checks that. The player's position is **not saved**: after a
reload you start at the farm. Accepted.

### The unlock

`Stations/RevealWhenAll.cs` is the "and" version of a gate: it reveals its targets once ALL of a
list of `UnlockStation`s are bought, `UpgradeStation`s are `IsMaxed`, `ChoreStation`s are
done, `ItemBuffer`s are `emptied` (the rubbish piles) and `PaintStation`s are `painted` (first job
confirmed). No state of its own (it re-derives the answer from saved components on load); polls once at
start and every 0.5 s. The supermarket gate `farm.unlock.market` is revealed by one watching
**all 6 farm gates, all 8 hire squares and all 4 BuyUnit stations**. Then it costs **$10,000**.
Paying it reveals the market root and the farm-side `TravelToTown` square, which sits on the
gate's own spot at the east end of the road (x = 23).

### Renovation (stages)

The shop is a derelict on arrival. Each stage reveals the next, through `RevealWhenAll`:

| Stage | What the player does | Cost | Reveals |
|---|---|---|---|
| 0 | **Carry the rubbish out.** 4 piles of 4 bags (`BagsPerPile`); each pile is an `ItemBuffer` of `Trash` with a COLLECT square (`market.trash.N`). The player carries 8, so at least two trips to the **bin outside on the forecourt** (`DiscardStation`) | free | Repair gate + PAINT square, when every pile is empty |
| 1 | **Repair** (`market.unlock.repair`) and the **first paint job** at the PAINT square (`market.paint`, a menu - see below) | $1,500 / $1,000 | Repair swaps the wrecked shelves and desk for working ones; the first confirmed paint job swaps the grubby coat for the painted one |
| 2 | **OPEN** (`market.open`, a `ChoreStation`, 1 ring of 2 s) | free | the whole working shop (stage 3) |
| 3 | The shop trades | - | sklad1's storage + supply, the first row's STOCK and COLLECT squares, checkout 1 and its queue, the cashier and both sklad1 stockers on sale, lit sign |
| 4 | **Growth** (below) | $1,000-$3,500 each | a second shelf row, three new products, a second checkout, COMING SOON |

`Stations/ClearablePile.cs` sits on each trash buffer: it hides litter pieces as the buffer drains
and switches the COLLECT square off when it is empty. No state of its own - a half-cleared pile is
just a buffer with fewer bags, which the buffer already saves. `Trash` is an `ItemDefinition`
(price 0, not perishable, in no sell list; `SellStation` also refuses any item worth nothing) with
an icon in `IconFactory`. `DepositStation` only ever accepts its own buffer's item, so trash cannot
be put on a shelf. Collision (walls) is built once and never swapped; only the *visual* sets blink.
Clearing is free, so a broke player can always start.

### Paint and decorate

`Stations/PaintStation.cs` (Task mode, ring 0.9 s, player only) opens `UI/PaintMenu.cs` - the one
exception, besides the pause panel, to "no HUD buttons": entry is still the physical square, the
world freezes and the joystick goes while it is open. The square re-arms only after the player
steps out. Tabs: **Colours** (three full pickers - saturation/value square plus hue strip - and a
cube preview), **Patterns** (ten: plain, stripes, checker, brick, subway, diamonds, waves, dots,
herringbone, two-band wainscot), **Floor** (tiles / wood / solid; tiles and solid get a picker, wood
a six-swatch run from light oak to walnut). Every change previews live on the real shop through
`Stations/MarketDecor.cs` (`Preview` / `CancelPreview` / `Commit`); money is only taken on Confirm.

Cost: the first confirmed job is **$1,000** (`FirstPaintPrice`, the renovation step), every later one
**$200** (`RepaintPrice`), both through `Price()`. If the player cannot afford it Confirm is greyed
and the menu says so; it is cosmetic, so this is not a money trap. Paint no longer has a gate; OPEN
needs Repair + the first paint job (`RevealWhenAll.painted`).

How it is applied: the scene holds a grubby coat and a painted coat. `MarketDecor` shows the
painted one (previewing or once painted) and gives every wall piece, the skirting and the floor a
`MaterialPropertyBlock` - texture, tint, and tiling worked out from the piece's real size in metres
(`DecorSurface.length/height/origin`) so a brick is the same size everywhere and the pattern carries
on across pieces. **No Material is ever created at runtime.** `UI/PatternFactory.cs` draws each
pattern once into a map (weights of the three colours + a shade byte), recolours it on every
change, and software-renders the cube preview (no camera, no RenderTexture, so it behaves the same
in the web build). Export all of it to `pattern-sheet.png` with `Tycoon > Export Pattern Sheet`
(`PatternSheet.Export`) to inspect seams and scale. The default look before any paint job is the
derelict; the menu starts from cream walls, red skirting, pale tiles.

### The layout (round 3)

The shop is 24 x 21.0 m (x -12..12, z -5.4..15.6) and symmetric about the front door at x = 0:

```
                  north wall   [COMING SOON]
  [S11 corn] [S12 milk] [S13 egg]   | sklad1: egg/milk/corn crates, door 1,
     (stock squares, then aisle)    |         stocker hires A and B beside it
  [S21 bread][S22 apples][S23 yogurt]| sklad2: bread/apples/yogurt crates, door 2,
     (hall)                         |         stocker hires A and B beside it
  [counter][SERVE] queue  | door |  queue [SERVE][counter]      [PAINT]
```

Two rows of three shelves in three columns 4.8 m apart (`ShelfX`), both facing south: row 1
against the north wall (`Row1Z`), row 2 free-standing 5.3 m south of it (`Row2Z`). Shoppers
stand on the south side of either row. Two storage rooms up the east wall: **sklad1** (north)
and **sklad2** (south), each with a doorway in its west partition (`Door1From/To`,
`Door2From/To`, 2.6-2.8 m: the agent radius 0.3 is carved off each side, so 2.0 m remain).
The collect squares are in front of the crates, inside the room, and each room's two stocker
hire squares are **beside** its doorway on the room side - never in it, so the player's traffic
through the door does not walk across a hire. PAINT is in the south-east corner under sklad2.
The two checkouts stand side by side in the south-west, below the second shelf row, counters running
north-south. Each has its serving square on the counter's west side at the south end and its cashier
hire just north of that; shoppers queue southward along the counter's east side, pay, then turn east
along the bottom wall (z -4.7) to the door. COMING SOON sits just inside the door, east side.

### Storage and supply

Three crates in each room (`market.storage.<egg|milk|corn|bread|apples|yogurt>`, capacity 20),
each an `ItemBuffer` plus `Stations/SupplyFeed.cs`: **one unit every 4 s** (15 a minute) while
not full, taking nothing from the farm. A `CollectStation` square per crate - the player collects
exactly like a coop's COLLECT. Perishables rot in the crate and on the shelf like anywhere else
(existing `ItemBuffer` behaviour); that is the restock pressure. Offline: `SupplyFeed` saves
`clock` and `lastUnix` and on the first frame after a load adds `ClampOffline(gap) / 4` units
(capped by capacity). The buffer's own offline spoilage runs first, so a long absence tops the
crate up with fresh goods.

### Shelves

Six shelves: an `ItemBuffer` (capacity 12, `market.shelf.<corn|milk|egg|s21|s22|s23>`) and a
`DepositStation` "STOCK" square right up against it (`SquareIcon.Stock`, 1.8 m deep,
`StockOffset`; its "8 / 12" is the count). `UI/ShelfDisplay.cs` draws one cube per unit in the
item's own `carryMaterial` - the same greybox cube `CarryStack` uses, **never a runtime
material** - pooled once and toggled on `ItemBuffer.Changed`. The crates use the same component.

### Shoppers

`CustomerQueue` has an optional **browsing mode**: when `shelves` is set (a farm till leaves it
empty and behaves exactly as before) it spawns shoppers (`ShopperIntervalSeconds` = 6 s at full
reputation) with a list of 1-2 products x 1-3 units each, drawn only from **open** lines
(`StoreShelf.IsOpen`: the shelf stands and, for a new line, its crate has been ordered) - but not
limited to what is in stock; an empty shelf is the pressure. `CustomerAgent` has a `Shopping`
phase: walk `entryRoute`, then each stop in the order the shelves are listed (a tour of the shop),
standing at the shelf's stand point and taking a unit every 0.35 s; if the shelf is empty wait up
to **8 s**, then skip the item and cost the shop `reputationPerSkip` = 0.04. Then join a queue when
a slot is free. An empty basket leaves angry without queueing.

Shoppers still steer in straight lines, and the second row is free-standing, so the shelves carry
**aisle nodes** (`StoreShelf.via`): a hub in the hall, a lane up the middle gap, and chains along
each aisle. Shelves share nodes by sharing the same `Transform`, so the whole set is a tree and
`StoreShelf.Connect` works out a walk between two shelves by backing out to the last node they
share. The shopper and the layout audit call the same function. A shelf with no `via` is a direct
line, as before.

**Demand grows with the product lines** (`CustomerQueue.LineDemand`): the 6 s interval is tuned
for the opening three lines and each open line past that makes shoppers `demandPerExtraLine` =
25 % faster, capped at `maxLineDemand` = 2x. **Two checkouts:** the second till is a second
`CustomerQueue` listed in the first's `alternates` with `sharesReputationWith` set (reputation
and so prices are the shop's, saved once). A finished shopper picks the shorter queue, nearer on
a tie; `TryJoinQueue` returns the queue they joined and the shopper switches to it, so facing,
leaving and counting all use that till. The head-of-line rule (`CustomerQueue.Front`) is
unchanged and per queue. Without the second till the single queue fills and shoppers time out.

### The checkout

`Stations/CheckoutStation.cs`, the sibling of `RegisterStation`. It serves `queue.Front`,
scanning **one basket unit per tick** (`CustomerAgent.ScanNext`), paying
`basePrice x 1.5 x reputation`: **egg $7.50, milk $18, corn $1.50, bread $10.50, apples $6,
yogurt $15**. There is nothing to hand over - the goods are already in the basket. If the
occupant is a worker it would charge that worker's `feePerDelivery` per unit scanned (now 0; a cashier never
carries anything, so the usual piece rate in `WorkerAgent` would never fire).

### Workers

Existing `WorkerAgent`, no new AI. **Stockers, two per storage room** ($800 each, no running cost),
hired beside the room's doorway. A stocker serves its **whole room**: `WorkerAgent.routes` is a
list of (collect square, stocking square) pairs, and each time it is empty-handed it chooses the
usable pair whose shelf is **emptiest** - skipping a shelf that is not open yet, is full, whose
crate is empty, or that the other stocker has **claimed** (`WorkerAgent.Claims`, a small static
registry; released when the load is delivered, and cleared on every start). The chosen pair simply
becomes `pickup` / `dropoff`, so walking and standing off for the player are the
old code. With nothing usable the worker **idles** where it stands (empty-handed only) rather than
fetching goods for a shelf the other stocker is already filling. `WillUse` is deliberately only
the CURRENT pair: if it covered all routes a stocker walking past the apple crate would fill its
arms with apples. Sklad1's stockers only ever carry egg/milk/corn, sklad2's bread/apples/yogurt.
Workers with no `routes` (every farm hand and cashier) behave exactly as before.
**Cashiers** ($1,500 each, $0.50/item): pickup = dropoff = the checkout, so the arms never fill
and it stands scanning; one per checkout; it steps aside whenever the player serves.

### Growth

After the shop is staffed it grows one product at a time, every step an ordinary
`UnlockStation`, sequenced by `RevealWhenAll` and by each gate's reveal list (`BuildGrowth`):

```
cashier 1 + both sklad1 stockers hired                 (RevealWhenAll)
  -> S21 shelf $1,000 -> "Order Bread" $1,500
        ordering reveals: bread's crate, collect and stock squares,
        sklad2's two stockers, the SECOND CHECKOUT $3,000, and shelf S22
        checkout 2 reveals its desk, queue and cashier 2 ($1,500)
  -> S22 $1,500 -> "Order Apples" $2,500 -> S23 $2,000 -> "Order Yogurt" $3,500
all three shelves and all three orders bought          (RevealWhenAll)
  -> COMING SOON: a locked square (LockedStation) just inside the front door, east side, held for the butcher
```

A product is only ever offered once its shelf is standing, and only one order is ever on offer,
so nothing can be bought without a place to put it (the audit checks both). COMING SOON is not
for sale and does nothing. Nothing here needs money to escape a state with no income: the opening
three lines trade from the day the shop opens, and every growth step is optional.

### Tunables (all in `MarketBuilder.cs` unless noted)

| What | Constant | Value |
|---|---|---|
| Supermarket price | `FarmSceneBuilder.MarketPrice` | $10,000 |
| Repair | `RepairPrice` | $1,500 |
| First paint job / repaint | `FirstPaintPrice` / `RepaintPrice` | $1,000 / $200 |
| Rubbish | `TrashPositions` (4 piles) x `BagsPerPile` | 4 x 4 bags |
| Supply rate | `SupplySecondsPerUnit` | 4 s |
| Shelf / crate capacity | `ShelfCapacity` / `StorageCapacity` | 12 / 20 |
| Shopper interval | `ShopperIntervalSeconds` | 6 s at 3 lines |
| Demand per extra line / cap | `CustomerQueue.demandPerExtraLine` / `maxLineDemand` | +25 % / 2x |
| Queue slots (each) / max shoppers | `QueueSlots` / `MaxShoppers` | 3 / 8 |
| Checkout multiplier | `CheckoutStation.priceMultiplier` | 1.5 |
| Stocker | `StockerPrice` / `StockerFee` | $800 / $0 |
| Cashier | `CashierPrice` / `CashierFee` | $1,500 / $0 |
| Shelves S21 / S22 / S23 | `GrowthShelfPrices` | $1,000 / $1,500 / $2,000 |
| Orders bread / apples / yogurt | `GrowthOrderPrices` | $1,500 / $2,500 / $3,500 |
| Second checkout | `Checkout2Price` | $3,000 |
| Bread / apples / yogurt | `FarmSceneBuilder.CreateItems` | base 7 / 4 / 10 (x1.5 at the till); bread rots 150 s, yogurt 120 s |

All purchase prices go through `FarmSceneBuilder.Price()`, so `TestPriceOverride` flattens the
market too. **Not in this version:** the butcher itself (COMING SOON marks the bay), a roof.

### New pieces (and what was extended)

New: `Core/Location.cs`, `Stations/TravelStation.cs`, `RevealWhenAll.cs`, `ChoreStation.cs`,
`SupplyFeed.cs`, `CheckoutStation.cs`, `UI/ScreenFade.cs`, `UI/ShelfDisplay.cs`,
`Editor/MarketBuilder.cs`, `Stations/ClearablePile.cs`, `Stations/PaintStation.cs`,
`Stations/MarketDecor.cs`, `UI/PaintMenu.cs`, `UI/PatternFactory.cs`, `Editor/PatternSheet.cs`,
`Stations/LockedStation.cs` (round 3: the COMING SOON square).
Extended in round 3: `WorkerAgent` (`routes`, `StockRoute`, claims, idle), `CustomerQueue` (`alternates`,
`sharesReputationWith`, `LineDemand`, `StoreShelf.via/openWhen/IsOpen/Connect`), `CustomerAgent` (aisle
trail), `FarmSceneBuilder.Items` (Bread, Apples, Yogurt), `IconFactory`/`IconSheet` (three product icons),
`SquareAudit` (growth rules, aisle-aware shopper paths).
Extended: `RevealWhenAll` (`emptied`, `painted`), `SellStation` (refuses worthless items),
`LevelBuildKit.BuildBin` (`squareToSouth`), `HudRoot` (hosts `PaintMenu`), `IconFactory` (Trash, Paint).
Extended earlier: `CustomerAgent`/`CustomerQueue` (browsing mode),
`IsometricCameraRig.SnapToTarget`, `UpgradeStation.IsMaxed`, `DepositStation.icon`,
`CapacitySign.starvedMessage`, `HudRoot` (hosts `ScreenFade`), `IconFactory` (four icons), and in
the editor `LevelBuildKit` (boundary and fence by edges, grass/fence mesh names),
`FarmSceneBuilder` (market link; `Gate`, `BuildHire`, `Price`, `LevelLocal`, `Items` are now
`internal`), `SquareAudit` (see `EDITING_GUIDE.md` section 25).
