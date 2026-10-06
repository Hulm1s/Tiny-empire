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

### How workers get paid — a deadlock fix

```csharp
int delivered = _lastCarryCount - count;
if (delivered <= 0 || !_headingToDropoff || feePerDelivery <= 0d) return;
wallet.TrySpend(feePerDelivery * delivered);
```

> The station does the actual transfer, so the worker detects its own deliveries by watching its
> carried count drop while on the delivery leg. That keeps stations completely unaware that
> workers exist.

And why piece rates rather than wages:

> Paying by the minute looked reasonable but deadlocks the game: once the wallet empties the
> workers stop, and if a stopped worker was the one taking goods to the counter then nothing can
> ever earn money again. Charging per delivery means pay is always a cut of work that just
> happened, so the balance can never run away.

**Short pay never stops work.** That is the point.

### The eight workers

| Role / id | Pickup | Dropoff | Fee/unit | Real price |
|---|---|---|---|---|
| Farmer / `HarvesterA` | CornFieldA | CoopA.Feed | 0.5 | 250 |
| Cashier / `SellerA` | CoopA.Collect | CounterA | 0.8 | 400 |
| Farmer 2 / `HarvesterB` | CornFieldB | CoopB.Feed | 0.5 | 700 |
| Cashier 2 / `SellerB` | CoopB.Collect | CounterA | 0.8 | 800 |
| Hay Hand / `HayHandA` | HayFieldA | CowShedA.Feed | 0.6 | 1500 |
| Milk Run / `MilkRunA` | CowShedA.Collect | CounterB | 1.4 | 1700 |
| Hay Hand 2 / `HayHandB` | HayFieldB | CowShedB.Feed | 0.6 | 2200 |
| Milk Run 2 / `MilkRunB` | CowShedB.Collect | CounterB | 1.4 | 2400 |

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

- A machine loses `wearPerOutput` condition per unit produced (1.5 for coops, 2.0 for sheds).
- At zero it **jams**.
- Repair is a `Task`-mode station: stand in `FIX`, a ring fills, `+N%` popups rise, `FIXED`.
- `RepairStation.costPerPoint` — 0.25 for coops, 0.35 for sheds.

### The rule that governs this and everything else

> **No mechanic may require money to escape a state where you cannot earn money.**

Three things exist only because of it:

1. **Repair always works at $0.** `RepairStation.freeRepairFraction` guarantees a fraction of
   the repair rate for free. Money buys *speed*, not permission. **Never set it to zero.**
2. **Workers are paid per delivery, never per minute** (§10).
3. **The carry stack holds mixed goods**, and there are three bins (§5).

Anything you add that charges the player must be checked against this rule.

---

## 13. The four pressures that keep a finished farm interesting

1. **Wear** — machines jam at zero condition.
2. **Spoilage** — eggs rot in 90 s, milk in 120 s, so stockpiling loses money.
3. **Reputation** — timed-out customers thin the queue *and* cut the price of everything.
4. **Piece rates** — automation is a running cost that scales with throughput.

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
