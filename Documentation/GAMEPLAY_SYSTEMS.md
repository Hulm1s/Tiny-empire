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

Occupants are added by `OnTriggerEnter` and removed by `OnTriggerExit`, and Unity delivers neither to an inactive
object. An actor standing in a square when it is switched off therefore stays on its list, and if the square is
switched back on later it keeps working on that actor from wherever they are. `ClearOccupants()` drops the list;
call it before switching off a square that will come back (`HairMess` does, for the SWEEP square). Squares that are
switched off for good never notice.

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
Holds the **livestock headcount** (`units`, up to `maxUnits` = 5 on all four farm buildings; hen 7 s, cow 8 s per
unit; buffers coop 20 feed / 24 eggs, shed 20 / 20). Saveable, with deferred offline catch-up.
Livestock purchases (`UpgradeStation`): base price coop A 120 / coop B 180 / cow A 450 / cow B 600, `priceGrowth` 1.6
(4 purchases per building), drain per tick `max(payPerTick = 5, CurrentPrice / 40)` like the gates. Workers carry 6
(`LevelBuildKit.BuildWorker`).

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
CornFieldA2 / CornFieldB2   (second field of each line, north of the first - bought, see "Second fields")
        ↓  player or Farmer carries corn
CoopA.Feed                  (DepositStation → input ItemBuffer, cap 20)
        ↓
CoopA ProducerMachine       (7s per egg, per chicken; 1–5 chickens)
        ↓
CoopA output buffer         (cap 24)
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

### Seated waiting room and service chairs (the salon)

A till serves the head of its queue where they stand. A salon instead **calls the head out to a
free chair** and serves them there, several chairs at once, while the waiting room moves up and
refills. This is an extension of the same queue and the same customer — not a second customer
system. A queue with no `chairs` and `seatedWaiting` off (every till before the salon) is
unchanged; the play-mode regression over all six queues in `Farm.unity` confirmed it.

```
Spawn → entryRoute → a waiting seat (CustomerQueue.slots, seatedWaiting = true)
  ↓  Phase.Waiting, SITTING (faces the slot's forward, Sit clip), patience ticks as at a till
NEXT square / receptionist → CustomerQueue.DispatchFront(chair)      (chair claimed here, synchronously)
  ↓  Phase.ToChair — stands up, walks ServiceChair.approach then the seat; everyone behind moves up a seat
Reaches the seat
  ↓  Phase.AtChair — sits, patience restarts at full, bubble as at a till
ChairServiceStation (the STYLE square) serves them: Settle per unit → the pile it names
  ↓            (or patience hits zero)
Phase.Leaving — chair.Release(served) fires ServiceChair.Released, then the queue's OnCustomerLeft
  ↓  walks ServiceChair.exitRoute, then CustomerQueue.exitRoute, then exitPoint
Destroy
```

| Piece | File | What it is |
|---|---|---|
| `CustomerQueue.seatedWaiting` | `Customers/CustomerQueue.cs` | Each slot is a chair; waiting customers sit on it. They still shuffle up a seat when the head leaves (stand, walk, sit), so slot 0 is always next. A customer already in the right seat is never stood up. |
| `CustomerQueue.chairs` | same | The `ServiceChair`s the head can be sent to. `FreeChair` offers them in list order. |
| `DispatchFront(chair)` / `DispatchFront()` / `CanDispatch` | same | Send `Front` to that chair (or the first free one). False and no change if nobody is ready or the chair is not `IsFree`. |
| `WaitingCount`, `AtChairCount` | same | For square labels and the receptionist. The spawn cap is still `slots.Length` people *waiting*, so the room refills while the chairs work. |
| `ServiceChair` | `Customers/ServiceChair.cs` | Seat transform, `approach` and `exitRoute` waypoints, `dirtyWhen` (a GameObject; while it is active the chair is not clean), `Occupant`, `ReadyOccupant`, `IsClean`, `IsFree`, and the `Released(chair, customer, served)` event. |
| `ChairServiceStation` | `Stations/ChairServiceStation.cs` | A `RegisterStation` that serves its chair's occupant instead of the queue's head: stand here `serviceSeconds` and the whole order is settled. `ServeWhole(customer)` is the hook a minigame subclass finishes through. Charges a hired server's `feePerDelivery` per unit, as `CheckoutStation` does. |
| `CallNextStation` | `Stations/CallNextStation.cs` | The NEXT square: Task mode, one `DispatchFront()` per ring, the ring only runs while a call would succeed. Free, so it never needs money. |
| `RegisterStation.Serving` | `Stations/RegisterStation.cs` | New protected virtual: who this square serves. Default `queue.Front`, so every till is unchanged; the chair station overrides only this. |

**Payment is the till's payment.** A chair station is a `RegisterStation`, so the price is
`item.basePrice × priceMultiplier × queue.PriceMultiplier`, paid through `Settle` → `Pay` →
`Takings(pile, accountWhenUnlocked, accountShare, …)`. Point every chair's `pile` at the one
reception `CashPile` and every service lands there. **Set each chair station's `queue`** — with no
queue, `UnitPrice` is 0 and the haircut is free.

**The minigame (player only)** follows `BouquetCounter`: subclass `ChairServiceStation`, override
`TickWithPlayer`, and when `Carry.CompareTag("Player")` open the minigame for `ReadyOccupant`
instead of counting `serviceSeconds`; when it finishes call `ServeWhole(customer)` (pays the base
price, unit by unit, then the customer leaves happy) and `Pay(tip)`. Workers fall through to the
base class and are served at the base price after `serviceSeconds`.

**Hair on the floor.** Subscribe to `ServiceChair.Released`; when `served` is true, switch the hair
heap on and make it the chair's `dirtyWhen`. Nobody is sent to a dirty chair. Sweeping it away
(switching it off) frees the chair. A walk-out (`served == false`) drops nothing.

**Seat height.** The seat transform is where the seated customer's origin goes: the middle of the
seat in plan, forward = the way they face. The Sit clip puts the underside of the body **0.16 m**
above the origin, so place the seat transform at *(seat top − 0.16 m)*. On a seat higher than that
(the phase-1 salon chair's top is ~0.575 m, the waiting chair's ~0.445 m) the customer is lifted onto it and
walks back down to the height they came in at when they get up.

**Nothing to get stuck.** Customers are not saved (a reload starts with an empty shop), the
chair's occupant is read through Unity's null (a destroyed customer frees it at once) and must
still be heading for or sitting in that chair, an unserved customer times out and frees it, and
every location stays live while the player is elsewhere. The one persistent thing is `dirtyWhen`,
which is the salon's own saved object, and clearing it is free.

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
| Farmer / `HarvesterA` | CornFieldA **or CornFieldA2** | CoopA.Feed | 0 | 250 |
| Cashier / `SellerA` | CoopA.Collect | CounterA | 0 | 400 |
| Farmer 2 / `HarvesterB` | CornFieldB **or CornFieldB2** | CoopB.Feed | 0 | 700 |
| Cashier 2 / `SellerB` | CoopB.Collect | CounterA | 0 | 800 |
| Hay Hand / `HayHandA` | HayFieldA **or HayFieldA2** | CowShedA.Feed | 0 | 1500 |
| Milk Run / `MilkRunA` | CowShedA.Collect | CounterB | 0 | 1700 |
| Hay Hand 2 / `HayHandB` | HayFieldB **or HayFieldB2** | CowShedB.Feed | 0 | 2200 |
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
| `farm.unlock.cornA2` | `Field` | 350 | CornFieldA2 (on sale from day one) |
| `farm.unlock.cornB` | `Field` | 400 | CornFieldB |
| `farm.unlock.coopB` | `Coop` | 500 | CoopB **+ HarvesterB + SellerB hires + the `cornB2` gate** |
| `farm.unlock.cornB2` | `Field` | 700 | CornFieldB2 (offered once Coop B is bought) |
| `farm.unlock.cowA` | `Dairy` | 3000 | CowShedA **+ HayFieldA + HayHandA + MilkRunA + CounterB (the milk till) + the `hayA2` gate** |
| `farm.unlock.hayA2` | `Meadow` | 1800 | HayFieldA2 (offered once the Dairy is bought) |
| `farm.unlock.cowB` | `Cow Shed` | 4500 | CowShedB **+ HayFieldB + HayHandB + MilkRunB + the `hayB2` gate** |
| `farm.unlock.hayB2` | `Meadow` | 2800 | HayFieldB2 (offered once Cow Shed B is bought) |

(The old stand-alone `farm.unlock.hayB` Meadow gate is gone: meadow B is sold with Cow Shed B.
Prices in the Field/Coop rows of older notes - 600/900 - were already stale.)

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

The second meadow follows the same rule now: `hayB` is sold with Cow Shed B (one purchase, $4,500)
instead of as a separate gate. The *second fields* (`hayA2`, `hayB2`) are separate purchases, but
their gates are only revealed by the shed's own gate, so hay still never exists before a cow.

### Second fields (farm expansion)

Each production line has a bought second field north of the first
(`FieldRow2 = FieldRow + FieldSize.y + 0.6 = 3.05`, same x column, same 4.5 x 3.5 size and crop,
saved as `farm.cornA2 / cornB2 / hayA2 / hayB2`). The line's hire carries `WorkerAgent.routes`
= [field 1 -> feed, field 2 -> feed]; while the arms are empty `WorkerAgent.ChooseField` sends the
worker to the field with **most crop ready** (inactive = not bought = skipped; the current field
is kept unless the other has at least 2 more ready or the current one is empty; with every
field empty the worker waits at the current one). `pickup`/`dropoff` stay equal to route 0, so
the audit's route checks still hold and `SquareAudit.Progression` additionally checks every field
route (crop matches the feed square, both ends switchable).

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
enum Motion { Idle = 0, Walk = 1, CarryIdle = 2, CarryWalk = 3, Sit = 4 }   // → animator int "State"
```

`Sit` is the one state it cannot measure (a seated villager is standing still, like an idle one),
so a character that can sit says so through `ISeatedPose.IsSeated` — only `CustomerAgent`
implements it, true while seated in a waiting room or a service chair. It beats every other state.

It is **a watcher, not a controller** — it measures speed from transform movement rather than
reading any motor:

> That means the player, workers and customers all animate correctly without a single line
> changing in PlayerMotor, WorkerAgent or CustomerAgent.

Customers have no `CarryStack`, so they only ever reach `Idle` / `Walk` — and `Sit` in a seated
waiting room or a service chair.

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
**all 10 farm gates (incl. the four second fields), all 8 hire squares and all 4 BuyUnit stations** (each building at 5 animals). Then it costs **$7,000**.
Paying it reveals the market root and the farm-side `TravelToTown` square, which sits on the
gate's own spot at the east end of the road (x = 23).

### Renovation (stages)

The shop is a derelict on arrival. Each stage reveals the next, through `RevealWhenAll`:

| Stage | What the player does | Cost | Reveals |
|---|---|---|---|
| 0 | **Carry the rubbish out.** 4 piles of 4 bags (`BagsPerPile`); each pile is an `ItemBuffer` of `Trash` with a COLLECT square (`market.trash.N`). The player carries 8, so at least two trips to the **bin outside on the forecourt** (`DiscardStation`) | free | Repair gate + PAINT square, when every pile is empty |
| 1 | **Repair** (`market.unlock.repair`) and the **first paint job** at the PAINT square (`market.paint`, a menu - see below) | $3,000 / $2,000 | Repair swaps the wrecked shelves and desk for working ones; the first confirmed paint job swaps the grubby coat for the painted one |
| 2 | **OPEN** (`market.open`, a `ChoreStation`, 1 ring of 2 s) | free | the whole working shop (stage 3) |
| 3 | The shop trades | - | sklad1's storage + supply, the first row's STOCK and COLLECT squares, checkout 1 and its queue, the cashier and both sklad1 stockers on sale, lit sign |
| 4 | **Growth** (below) | $3,000-$5,500 each | a second shelf row, three new products, a second checkout, then the butcher extension gate (stage 5) |

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
                  north wall
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
along the bottom wall (z -4.7) to the door. The **ATM is outside** on the street frontage (`AtmPosition`
(-9.8, -6.2), west of the door, squares 1.65 m in front of it on the forecourt); shoppers walk the road
to x = 0 first (`EntryRoute[0]`) so they never cut across its squares.

### The butcher extension (shell only; contents come later)

Filip's sketch: the butcher is an **extension on the shop's west side**, entered from the street.
`MarketBuilder.BuildButcher` builds an inactive `Butcher` object: front room x -21..-12, z -5.4..4.6
(customers' gap in the south wall x -18..-15), back room x -17.5..-12, z 4.6..11.6 (gap into it from the
staff strip x -15.5..-13, tall north wall), walls solid and carving the navmesh like the shop's, floors,
and the meat van's **driveway** (4 m wide: up the west side x -27..-23 from the street, then east along
z 7..11 to the back room's van door z 7.4..10.6 in its west wall; flat tarmac, no collider). The market's
west wall has the **staff door** (z 2.0..4.2), closed by a *plug* (three wall pieces + collider) until the
extension is bought; orange posts and lintel mark it. Customers never use it.

The market's world edge moved: `EdgeWest` -17 -> -30 (fence, boundary, road, grass keep-clear, forecourt
follow). The extension is bought at the **butcher gate** (`market.unlock.butcher`, $5,500 via `Price()`,
`UnlockButcher`, at (-16.5, -2.0) on the lot): it is offered when all six growth purchases are done
(replaces COMING SOON), reveals `Butcher` and hides the plug. Empty slots reserved for the contents
(`MarketBuilder.Butcher*`, market-local): drop square (-15.4, 9.8) 2.2x2.2; CUT square (-15.1, 6.6) 2.4x2.4;
cutting table (-12.9, 8.2) 1.2 x 4.4 along the market wall; display counter y=1.2, x -20.8..-12.4; cash
pile (-19.8, 3.0); butcher hire (-17.9, 3.0); queue front (-16.6, 0.1); customers' door (-16.5, -5.4).
`SquareAudit.ButcherShell` checks the shell is saved inactive, the gate hides the plug, and with the plug
gone every walk the butcher needs (street to counter, staff door to strip to back room to table to drop
square, driveway to the van door) clears every wall by 0.35 m.

### Storage and supply

Three crates in each room (`market.storage.<egg|milk|corn|bread|apples|yogurt>`, capacity 20),
each an `ItemBuffer` plus `Stations/SupplyFeed.cs`: **one unit every 6 s** (10 a minute) while
not full, taking nothing from the farm. A `CollectStation` square per crate - the player collects
exactly like a coop's COLLECT. Perishables rot in the crate and on the shelf like anywhere else
(existing `ItemBuffer` behaviour); that is the restock pressure. Offline: `SupplyFeed` saves
`clock` and `lastUnix` and on the first frame after a load adds `ClampOffline(gap) / 6` units
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
**Cashiers** ($3,500 each, $0.50/item): pickup = dropoff = the checkout, so the arms never fill
and it stands scanning; one per checkout; it steps aside whenever the player serves.

### Growth

After the shop is staffed it grows one product at a time, every step an ordinary
`UnlockStation`, sequenced by `RevealWhenAll` and by each gate's reveal list (`BuildGrowth`):

```
cashier 1 + both sklad1 stockers hired                 (RevealWhenAll)
  -> S21 shelf $3,000 -> "Order Bread" $4,000
        ordering reveals: bread's crate, collect and stock squares,
        sklad2's two stockers ($2,500 each), the SECOND CHECKOUT $4,500, and shelf S22
        checkout 2 reveals its desk, queue and cashier 2 ($3,500)
  -> S22 $3,500 -> "Order Apples" $5,000 -> S23 $4,000 -> "Order Yogurt" $5,500
all three shelves and all three orders bought          (RevealWhenAll)
  -> the butcher extension gate $5,500 (market.unlock.butcher, on the lot west of the shop): reveals the empty butcher shell
```

A product is only ever offered once its shelf is standing, and only one order is ever on offer,
so nothing can be bought without a place to put it (the audit checks both). The butcher gate is
the only thing offered after it; its contents are a later step. Nothing here needs money to escape a state with no income: the opening
three lines trade from the day the shop opens, and every growth step is optional.

### Tunables (all in `MarketBuilder.cs` unless noted)

| What | Constant | Value |
|---|---|---|
| Supermarket price | `FarmSceneBuilder.MarketPrice` | $7,000 |
| Repair | `RepairPrice` | $3,000 |
| First paint job / repaint | `FirstPaintPrice` / `RepaintPrice` | $2,000 / $200 |
| Rubbish | `TrashPositions` (4 piles) x `BagsPerPile` | 4 x 4 bags |
| Supply rate | `SupplySecondsPerUnit` | 6 s |
| Shelf / crate capacity | `ShelfCapacity` / `StorageCapacity` | 12 / 20 |
| Shopper interval | `ShopperIntervalSeconds` | 6 s at 3 lines |
| Demand per extra line / cap | `CustomerQueue.demandPerExtraLine` / `maxLineDemand` | +25 % / 2x |
| Queue slots (each) / max shoppers | `QueueSlots` / `MaxShoppers` | 3 / 8 |
| Checkout multiplier | `CheckoutStation.priceMultiplier` | 1.5 |
| Stocker | `StockerPrice` / `StockerFee` | $2,500 / $0 |
| Cashier | `CashierPrice` / `CashierFee` | $3,500 / $0 |
| Shelves S21 / S22 / S23 | `GrowthShelfPrices` | $3,000 / $3,500 / $4,000 |
| Orders bread / apples / yogurt | `GrowthOrderPrices` | $4,000 / $5,000 / $5,500 |
| Second checkout | `Checkout2Price` | $4,500 |
| Bread / apples / yogurt | `FarmSceneBuilder.CreateItems` | base 7 / 4 / 10 (x1.5 at the till); bread rots 150 s, yogurt 120 s |

All purchase prices go through `FarmSceneBuilder.Price()`, so `TestPriceOverride` flattens the
market too. **Not in this version:** the butcher's contents (only its empty shell and gate exist), a roof.

### New pieces (and what was extended)

New: `Core/Location.cs`, `Stations/TravelStation.cs`, `RevealWhenAll.cs`, `ChoreStation.cs`,
`SupplyFeed.cs`, `CheckoutStation.cs`, `UI/ScreenFade.cs`, `UI/ShelfDisplay.cs`,
`Editor/MarketBuilder.cs`, `Stations/ClearablePile.cs`, `Stations/PaintStation.cs`,
`Stations/MarketDecor.cs`, `UI/PaintMenu.cs`, `UI/PatternFactory.cs`, `Editor/PatternSheet.cs`,
`Stations/LockedStation.cs` (round 3: the COMING SOON square; now the town's placeholder).
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


---

## The town (`Editor/TownBuilder.cs`): third location, the street of businesses

`Location` id `town`, a root beside the farm turned the same 45 degrees, saved INACTIVE. Its first business is the florist (next section);
what else it holds comes later. Built from existing parts (Location, TravelStation, UnlockStation, RevealWhenAll,
`LevelBuildKit.BuildBoundary/BuildFenceAround/BuildGrass`).

- **Place:** farm-grid offset (0, -56): south of the farm, 18 m clear of its fence (the audit logs the gaps). Not along the road axis: the market already
  reaches world x 78 and the walkable ground plane is 160 m (+-80), so a third location cannot go further down it.
- **Inside** (town grid): fence/boundary x -16..16, z -12..13; road stub through both fence gaps at z -3; paved square x -12..12,
  z -1.4..9.5; arrival point (-6, -3); a "Back to the supermarket" `TravelStation` at (-12, -3); one `LockedStation` "Coming Soon" at (0, 4.5) with a plain sign board.
  Nothing else, nothing buyable, no state.
- **The way in:** in the SUPERMARKET, on the forecourt east of the shop front at (14.2, -3.8): gate `town.unlock` (3.0 x 2.4, **$12,000**,
  through `Price()`) with the travel square `market.travel.town` on it. The gate is itself hidden until a `RevealWhenAll`
  ("TownUnlock", on the market root) sees the butcher complete: the extension gate, every butcher purchase (pig farm, table, steak counter, corn
  field 2, the butcher hire, sausages) and the pigs at their maximum. Paying it reveals the town root and the travel square.
- **Price:** the sim (`plans/econ/market_sim.py`, proposal B) has the butcher's last step leaving ~$2,050/min with steps costing 2-4.5 min
  each; a location is a bigger milestone, so about six minutes of income = $12,000.
- **Back:** `town.travel.market` lands the player on the market's arrival point (8, -6.3), the same spot the farm trip uses
  (`Location` has one spawn point). There is no town-to-farm square.
- **Audit:** the town is audited like any location; `ButcherChain`'s "the supermarket must not ask for the pig farm" rule exempts the "TownUnlock" watcher on purpose.

---

## The florist (`Editor/FloristBuilder.cs`): the first business on the town street

The town is now a street of businesses (florist -> hairdresser -> cafe -> restaurant); the florist is
built, the COMING SOON square east of it (town (7, 4.5)) stands for the next one. Town grid, north +z,
the street at z -3. The town's north edge moved 13 -> 19.5 for the meadow; the plaza is now only the
part east of the florist (x 1..13).

```
 z 17.2  ---------------- meadow fence ----------------
         tulip  tulip    [gardener]    hydr.  hydr.         beds 1.8 x 2.4 (FloristModels.BedSize)
                            WELL
         rose   rose    [ WATER ]      sunfl. sunfl.        well square (-7, 10)
 z 8.2   ======== north wall ===== back door ===========
         [cash] [SERVE]                 [florist hire]     serve (-7.9, 6.0), back door x -6.9..-5.1
                ======= counter =======                    counter (-7, 4.4), 3.36 x 1.0
          queue  slot2  slot1  slot0                       z 3.1
         stand           CENTRE STAND          stand       centre stand (-7, 1.2)
 z -0.8  == entrance x -11..-9 ======== exit x -5..-3 ==
                         street (z -3)
```
Shop x -13..-1, z -0.8..8.2; low walls (0.7 m) with 1.2 m invisible collision that carves the navmesh;
wall stands on both side walls, angled corner stands in all four corners, the square stand in the middle.

**The town starts derelict, and the florist is renovated like the supermarket** (`FloristBuilder.BuildRenovation`,
the same parts as `MarketBuilder.BuildRenovation`): three litter heaps (`ItemBuffer` of 4 trash + `CollectStation`
`town.florist.trashcollect.N` + `ClearablePile`) carried to the bin west of the street door (`town.florist.bin`,
`BinAt` (-14.4, 1.9), `squareToSouth`), all free; when every heap is empty `RevealWhenAll` offers REPAIR
(`town.florist.unlock.repair`, an `UnlockStation`: reveals the `Working` group - counter and stands - and hides the
`Broken` group, the wrecks) and PAINT (`town.florist.paint`, a `PaintStation` on a `MarketDecor` `town.florist.decor`);
when repair is bought and the first paint confirmed, OPEN (`town.florist.chore.open`, a `ChoreStation`) appears and
brings in the shop (counter, queue, cash), the rose beds, the well, the seed box and the tulips offer. The shop has two
coats built in (`Grubby` / `Painted`), swapped by the decor; everything that blocks (wall, stand and counter
colliders, fence) is built once in `Building`, so nothing that blocks anyone blinks. The audit names: painted walls
are `Wall`, the grubby set `DirtyWall` (never counted twice); `Painted` / `Working` / `Building` are one building.
**Per-decor pattern list:** `MarketDecor.patterns` (empty = `PatternFactory.MarketPatterns`, the supermarket's ten, saved
indices unchanged); the florist lists `PatternFactory.FloristPatterns` = plain, stripes, dots, diamonds, waves + the
new FLOWERS, LEAVES, TRELLIS (indices 10-12, appended), no two-band wall (its walls are 0.7 m); the menu lays out only
what the shop lists (`PaintMenu.LayoutPatterns`). The hints under the grid name each colour's job.

**Flowers are grown in a cycle** (`Stations/FlowerBed.cs`, `Stations/BedStation.cs`). Each flower has a STOCK
(`ItemBuffer` `town.florist.stock.<id>`, 12, behind the counter, unseen) that a harvest of its two beds fills. A bed goes
Empty -> (plant a SEED) -> needs water -> (water) -> grows -> needs water again -> (water) -> grows -> Ready ->
(harvest) -> Empty. The two growing spells are the only thing that moves by itself (`FloristBuilder.SpellSeconds`,
rose 10, tulip 11, sunflower 13, hydrangea 14 s); the waiting stages never decay. A harvest puts
`FlowersPerHarvest` (4) flowers into the stock and needs room for all four (a full stock leaves the bed ripe).
Each bed has ONE square, a Task-mode `BedStation` (0.9 s): with a seed in the arms on bare soil it plants, with a
can on a dry bed it waters, on a ripe bed it harvests; it shows the seed / the can / the flower and "Growing 40%".
Six looks, one per stage (the approved `FloristModels.BedStage` models: empty, seedlings, seedlings wet, half-grown,
half-grown wet, bloom), so nothing blinks. A bubble over the bed (`UI/BedBubble.cs`: seed packet, water drop, pink
bloom; hidden while growing) asks for the next thing. Seeds are free from the SEED BOX (`ItemBuffer` of 8 +
`SupplyFeed` 2.5 s + `CollectStation`, east of the well) and water from the well (`ItemBuffer` of 6 + `SupplyFeed`
0.4 s + `CollectStation`), neither costs money, and the well is free from the start: no money trap. Items:
`Item_seed` (the pouch model) and `Item_watering_can`.

**Customers want bouquets.** `CustomerQueue.bouquets` (new, florist only): a customer wants 1-3 kinds of
flower that the shop sells AND grows (`ProductOffer` on each stock, live once that flower is bought),
1-3 of each, at most 6. They are a farm-style queue (3 slots) with an entry route in through the left
door, west of the central stand, to the counter, and out east of the stand through the right door.
The order lives in `CustomerAgent`'s basket lines (`BeginOrder`, `Wants`, `Deliver` by line); the bubble
shows each flower with its count (`OrderBubble.ShowLines`). Demand grows a third per extra flower bought
(`demandPerCapacity` 0.35); a customer every 8 s at full reputation.

**Serving** is `Stations/BouquetCounter.cs` (new), a `RegisterStation` display counter over the four stocks
(`RegisterStation.TickCounter` now serves any line the front customer still wants that is in a case, so a
sold-out flower does not hold up the rest; a one-product shopper behaves exactly as before):
- the hired **florist** (pickup = dropoff = the counter, like a cashier) is served by the base class:
  flower by flower out of the stock, at the base price x reputation;
- the **player** standing there 0.6 s, with a customer whose whole bouquet is in stock, opens the
  **bouquet minigame**; when it is done the whole bouquet is handed over at the same price plus a tip
  (`tipShare` 20 / 45 / 75 % for 1 / 2 / 3 stars). Nothing is taken until it is finished; closing it
  early takes nothing and the square re-arms when the player steps out.
Takings land on the cash pile at the counter's west end (`town.florist.cash`).

**The minigame** (`UI/MinigameOverlay.cs` base + `UI/BouquetMinigame.cs`, both new). The base is what the
haircut will reuse: opened from a square, world frozen (timeScale 0) and joystick hidden while open
(restored on close and in OnDisable), in the HUD's safe area, a 1080 x 1920 design card scaled to fit
(portrait phone 1:1, a landscape window smaller), a dim layer that eats taps, and `MinigamePad`: touch and
mouse both arrive as press / drag / release in design coordinates. The bouquet: header (BOUQUET, the ORDER
with the finished bouquet and each flower x N, tabs CUT > FILL > TIE > WRAP) as the approved mock-ups,
then CUT (swipe across the dashed line, every stem crossed is trimmed), FILL (drag each flower from YOUR
FLOWERS into a place that wants it; a tap also places it; a wrong drop shakes and counts as a mistake),
TIE (circle the knot twice), WRAP (swipe right to left), RESULT (stars, PRICE / TIP / TOTAL; tap or 3 s
closes). Stars: time against a par of 6 s + 1.2 s per flower (<= par 3, <= 1.8 x par 2, else 1), minus one
for two or more wrong drops. Art is `BouquetArt` / `HudArt`. **Nothing is painted at runtime**: painting one
big bouquet picture took 3-6 s in the editor, so `Editor/BouquetBake.cs` paints every picture once (100
of them, 2.6 MB of PNG, ~4 min) into `Resources/Bouquet` with an index of anchors and crop rectangles. The
scene build re-bakes only when `BouquetMinigame.ArtVersion` changes. The gathered bouquet is stacked at
runtime from its baked layers (`BouquetArt.Part` paper/leaves/ribbon, one stem per head count and position,
one bloom per flower) in `BouquetMinigame.BunchView`, in the same order `BouquetArt.Bunch` paints it. An
unbaked picture is painted on the spot with a warning, so a missing bake is slow, not broken.

**Texture memory.** The baked PNGs are imported with the settings in code (`BouquetBake.ApplyImportSettings`,
re-applied by every scene build if they differ - the `.meta` files are not edited by hand): WebGL override
**ASTC 6x6** (ASTC 4x4 for pictures of 256 px or less, where 6x6 visibly softens crisp outlines), max size
1024, mipmaps only on the bouquet layers (`b*`). ASTC is what iPhone and Android GPUs take natively; the
project's default WebGL format is DXT5, which iOS lacks (a phone would unpack it to full size). All 100
pictures: 37 MB as RGBA32 (with mipmaps), 4.7 MB as ASTC; a typical order loads about 48 of them: 18.8 MB
before, 2.5 MB after. Pictures are loaded on first use per picture (`Cached`) and kept; they are not released
on close - the built screen's images hold them, and at 2.5 MB it is not worth rebuilding the screen on every
open. A browser without ASTC (desktop Chrome on Windows) makes Unity unpack the textures on load - works, but
unmeasured; the phone is the target.

**Phone fit and orientation.** The content is two groups, each in the 1080 x 1920 design coordinates: the
header (title, ORDER, step tabs) and the body (the play area, banner, step pill, the touch pad). Portrait
stacks them on one card (the approved layout). Landscape sets them side by side - header column left, body
right, each in its own frame (`BoardTop` / `BoardBottom` instead of the one big `Board`) - on a 2080 x 1260
card (`LandW`/`LandH`). `MinigameOverlay.FitCard` runs every frame and takes whichever arrangement fits the
safe area at the larger scale, so an iPad rotated while the minigame is open switches live
(`ApplyOrientation`). The pad reports coordinates of its own group, so touch works in both. Sizes: portrait
390 x 844 phone 0.36 pt per design unit (44 pt = 122 units); phone sideways 0.29 (44 pt = 150 units); iPad
about 0.57 to 0.66. The X button has an invisible 160-unit hit area. The CUT row of stems is centred and
kept 24 units inside the play area for the widest bloom (`BuildCut`). Smallest labels are 8 to 11 pt on a
phone (design-size limited), 15 pt and up on a tablet.

**The stock panel** (round 3, in the HUD - it replaced a world board over the counter, which hid the customers'
bubbles): a small info panel at the bottom centre of the screen (`UI/StockPanel.cs`, built by `HudRoot` inside the safe
area, so the home bar never covers it) with the icon of every BOUGHT flower and `n / capacity`, red (and a dimmed icon)
at 0, so the player sees why a waiting customer cannot be served. It is shown only while the player stands inside the
florist's zone (`UI/StockInfoZone.cs` on the `Florist` group: the shop and the meadow, in the town's grid) and hidden on
the farm, in the supermarket and on the street. A flower whose stock object is switched off (not bought yet) has no cell;
the panel closes up and each purchase adds its cell. Information only: nothing in it takes a tap, so it cannot
interfere with the floating joystick; it sits below the task card, the money plaque and the pause button, and below
every menu. It reads the saved stocks, so it has no state of its own.

**Queue spacing** (round 3): customers' order bubbles used to overlap (slots 1.35 m apart in a row, a bouquet
bubble 2-4 m wide). Now `OrderBubble.LinePitch` is 100 (a 3-line bubble is 334 canvas units), the florist's customers use
`worldScale` 0.0075 (`FloristBuilder.BubbleScale`: 2.5 x 1.5 m for three lines) and the three queue slots are staggered
along a diagonal from the counter's front back towards the street door (`FloristBuilder.Slots`: (-7, 3.1), (-9.6, 2.4),
(-10.6, 0.4)): two bubbles clear each other if they are wider apart than a bubble sideways OR deeper apart than a bubble
is tall (a metre of depth is 0.77 m up the screen). `SquareAudit.FloristChain` checks every pair of slots against the
widest bouquet bubble (`OrderBubble.CanvasSize`) and measures the way out from slot 0 only (the others advance to the
front first). Other queues keep their layout (the butcher's one-product bubbles are not affected).

**Meadow layout** (Filip's sketch, round 3): the water square lies directly south of the well, touching it; the seed
box is a long rack against the north fence left of centre (`SeedBoxAt` (-8.6, 16.7)) with its collect square in front
(`SeedSquareAt` (-7.9, 15.35)); the gardener's hire square is against the north fence right of centre
(`GardenerHireAt` (-6.25, 16.2)). Beds and well did not move.

**Stands show only what is bought.** `FloristModels`' stands carry buckets named `Fill_<id>`; the builder
moves each flower's buckets into that flower's purchase group, so they appear when it is bought.

**Progression, one at a time** (all `Price()`; `plans/econ/market_sim.py`, `FLORIST`, 2026-10-17):

| Step | Price | Reveals |
|---|---|---|
| Litter (free) | - | repair and paint |
| Repair (`town.florist.unlock.repair`) | 2,000 | the counter and stands; the wrecks go |
| Paint (first job, `town.florist.paint`) | 1,000 (repaint 200) | the painted coat; with repair, OPEN |
| OPEN (`town.florist.chore.open`, free) | - | shop, queue, cash pile, rose beds, well, seed box, the tulips offer |
| Tulips | 3,500 | tulip beds + stock, the hydrangeas offer |
| Hydrangeas | 4,500 | hydrangea beds, the sunflowers offer |
| Sunflowers | 5,000 | sunflower beds, the florist hire |
| Florist (hire) | 5,500 | the florist worker (it only serves at the counter), the gardener hire |
| Gardener (hire) | 7,000 | the gardener: a `WorkerAgent` with three routes per bed (water from the well, plant from the seed box, harvest); `WorkerAgent.ChooseBed` does harvest, then water, then plant, nearest first, claims a bed for the whole job, carries one thing at a time and never throws a seed or can away |

Filip's limit: repair + first paint within 3 minutes of buying the town (the sim: paint at +1.3 min, repair at +2.5),
and no separate purchase for the first beds. The rest follow every 2.8-4.2 minutes on the farm and the supermarket
(both let go at half value once the town is bought, below) plus the florist: tulips +5.3, hydrangeas +8.9,
sunflowers +12.6, florist hire +16.7, gardener +20.9 minutes after the town; the florist alone ends near $330 a
minute in the sim. Flower prices 15 / 18 / 24 / 27 (rose / tulip / sunflower / hydrangea).

**The supermarket is let go when the town is bought** (the farm's rule, extended): every checkout
(`CheckoutStation.accountWhenUnlocked` = the town gate, new, `accountShare` 0.5) and the butcher counter
(`RegisterStation`, same fields) pay the account half of a sale instead of filling the cash pile, through the one
rule `RegisterStation.Takings(...)` that the farm tills use too (`TownBuilder.Build` sets them). The market has no
wear to switch off. The saved purchase decides, so it holds after a reload, and the player still earns (50%). The town
has its own ATM from the start (`town.atm`, east of the florist) because the account is where the farm and the shop
now pay and the renovation is paid in cash.

Task list: every purchase is an ordinary goal ("Repair the shop - $2K", "Paint the shop - $1K", "Open the shop",
"Clear out the rubbish", "Buy Tulips - $3.5K", "Hire Florist"); a free goal "Make a bouquet" while a whole bouquet
is ready and no florist is hired; and, while no gardener is on the beds, one problem line per flower and job at the
nearest bed that wants it: "Fetch seeds for the roses" / "Plant the roses", "Fetch water for the roses" / "Water the
roses" (pointing at the seed box or the well, then at the bed once the player holds the seed or can), "Harvest the
roses". The florist counter is excluded from the butcher's "Restock" problem.

**Audit:** `SquareAudit.FloristChain` (below) on top of the usual per-location checks.

---

## 20. Physical cash, the ATM and the account

Money is a thing the player carries. Every number here was read off the code.

- **`Wallet` is the ACCOUNT.** Unchanged (`GameRoot.Money`); an older save's balance stays on it and carried cash starts at 0.
- **`CashStack`** (`Scripts/Player/CashStack.cs`, on the player, saved as `player.cash`) is the cash in hand: one
  `double`, unlimited, drawn as banded bundles stacked 2 x 2 per layer on the player's BACK (`CashAnchor`,
  `CharacterLibrary.BackAnchor`), separate from the goods `CarryStack` in front. `CashStack.Of(carry)` returns the
  player's cash or null for a worker, so workers can neither pay nor collect.
- **`BundleStackView`** (same file) draws an amount: one bundle per $10 until the stack would pass `maxLayers`
  (4 on the back, 15 at a till); past that bundles MERGE (each stands for 10x) so height stays sane while the amount
  has no limit. Bundles are instances of `Prefabs/CashBundle.prefab` (built by `CashModels.BundlePrefab`; material and
  texture assets, never runtime Materials).
- **`CashPile`** (`Stations/CashPile.cs`) is a till's takings, saved as `<till>.cash`. `RegisterStation`/`CheckoutStation`
  `Deposit` sales there instead of `Wallet.Add`. Exception: a farm till whose `accountWhenUnlocked` (the
  `farm.unlock.market` gate) is owned pays the ACCOUNT at **`RegisterStation.accountShare` = 0.5** (the farm goes
  wholesale once the supermarket exists; the farm builder sets 0.5 on both farm tills; default 1). Supermarket
  checkouts always pile.
- **`CashCollectStation`** (same file) is the player-only square beside each till that moves the pile into the hand in
  growing steps (`workerCompatible=false`, so a cashier never touches it).
- **Paying** (`UnlockStation`, `UpgradeStation` incl. its refund, `RepairStation` paid part, `PaintMenu`) spends from
  `CashStack`. `RepairStation.freeRepairFraction` is untouched, so a jam with empty pockets is still repairable
  (the no-money-trap rule).
- **`AtmStation`** (`Stations/AtmStation.cs`): WITHDRAW square (account into hand) with a smaller DEPOSIT square
  (`deposit = true`). Stateless. One at the farm market row (`farm.atm`), one on the market's street frontage outside the shop (`market.atm`).
- **Wear off:** `Durability.retireWhen` = the market gate. When it is owned (also on restore) the machine is restored to
  full, `Wear` does nothing, and `retireDisable` (the repair square) is switched off. Derived from the saved purchase:
  no save field.
- **HUD:** the plaque shows cash in hand (big, by the coin) and the account (small, by the bank icon).
  **TaskBoard** adds "Collect cash at the till" / "Get cash at the ATM" goals when carried cash cannot cover the cheapest purchase.


---

## The butcher (`Editor/ButcherBuilder.cs`, filled into the shell `MarketBuilder.BuildButcher` makes)

The chain, end to end: **pigs make raw meat on the FARM, a van drives it to the butcher, the cutting table
turns it into steaks/sausages in the cutter's arms, the counter takes them into its case and serves shoppers
out of it, takings land on the cash pile.** Built from the existing parts; the only new runtime types are
`CutStation`, `MeatVan` and `ProductOffer`, plus small opt-in extensions (below).

Purchases, one step offered at a time (all prices through `Price()`; all in the butcher except the extension):

| Step | Price | Where | Brings |
|---|---|---|---|
| Butcher extension | 5,500 | market lot (existing) | the shell |
| Pig Farm | 5,000 | drop square | the farm's pig column (pen + 1 pig, corn field E1, pig hand, van, road link) and the drop pile |
| Cutting Table | 3,000 | cut square | table + `CutStation` |
| Steak Counter | 4,000 | counter square | display counter, cash pile, shoppers; offers pigs and corn field E2 |
| Pigs 2-4 | 3,500 / 5,000 / 7,000 | front room (`UpgradeStation.tierPrices`) | up to four pigs |
| Corn Field E2 | 6,000 | front room | the second pig field (the pig hand cuts the fuller one) |
| Butcher (hire) | 8,000 | behind the counter | offered once pigs are maxed AND E2 is bought |
| Sausages | 9,000 | back room, on the GRIND square | the grinder bench + its GRIND square (1 joint -> 3 sausages), the sausage case and `ProductOffer(sausage)`; offered after the butcher |

* **Pig pen** (farm x 26.25, `LevelBuildKit.BuildWorkshop(..., Livestock.Pig, maintenance: false)`): no
  durability, no repair/buy squares. 10 s per joint per pig, eats 2 corn, buffers 20 corn / 24 meat. It is NOT in
  `FarmParts.Unlocks`: the supermarket never waits for it. Farm edge 25.5 -> 36, road 34 -> 46.
* **`MeatVan`**: waypoint route (pen bay, farm road, link, market road, driveway, back-room door), no collider and no
  navmesh obstacle, so it never blocks anyone. Leaves with `minBatch` (4) or after 25 s with at least one, takes up to
  12 from the pen, backs up to the door, adds to the drop pile one unit at a time, drives home. Saved: cargo + time;
  offline it converts the gap into whole trips (order 100, after every buffer and machine has restored).
* **`CutStation`** (Task mode), one place and one product: the cutting table (`output` steak, `makes` 1) and the
  sausage grinder (`output` sausage, `makes` 3, own GRIND square, id `market.butcher.grind`). One raw meat per ring
  goes into the occupant's arms; it never makes what its counter case has no room for (counting what the cutter
  carries); before the counter exists the table just fills the arms. The player chooses by where they stand; the
  task list says "Cut steaks" / "Grind sausages" when that case is bare and meat is waiting.
* **Back room**: 7.3 x 11.6 m. Drop pile (north, by the van door), grinder + GRIND, table + CUT, all against the
  market wall with a 2.5 m walkway down the west wall; the driveway arm and van door moved north with it.
* **Counter** = `RegisterStation` with `stock` (the cases): standing there first puts carried goods into the case,
  then serves the front shopper out of it. The butcher's dropoff is this square, so it restocks and serves.
  Shoppers: `CustomerQueue` (farm style) with an `entryRoute` through the street door; `ProductOffer` is what lets
  them ask for steak (and sausage once its line is on); sausage adds demand (`demandPerCapacity` 0.5).
* **Butcher worker**: ordinary `WorkerAgent` with two routes (table -> counter, grinder -> counter). With empty
  arms `ChooseBench` takes the bench whose counter case is emptier (steak wins ties; benches that are not bought,
  have no meat or whose case is full are skipped); `deliverPartialLoads` takes a part load to the counter when the
  bench has nothing to do instead of waiting for a full one.
* **`StationBase.AdoptIfInside`**: a worker whose route changes to a square it is already standing in (the pig hand
  crossing E1 to reach E2) is enrolled; before this it stood in the field doing nothing.

---

## The hairdresser salon (`Editor/SalonBuilder.cs`): the second business on the town street

Built on the seated-waiting-room extension above (`CustomerQueue.seatedWaiting`/`chairs`, `ServiceChair`,
`ChairServiceStation`, `CallNextStation`). Shop x 4.8..18.2, z -0.8..9.6 (town grid); EdgeEast is 22.

**Reveal.** `SalonReveal` (RevealWhenAll in `TownBuilder`) watches the florist gardener hire (`FloristBuilder.Build`
returns it) and reveals the salon root. The florist is NOT let go; its cash piles stay.

**Chain.** litter (12 bags, free) -> Repair $2,800 -> Paint $1,400 (decor `town.salon.decor`, `PatternFactory.SalonPatterns`,
repaint $200) -> OPEN (chair 1) -> chair 2 $4,500 / 3 $7,000 / 4 $12,000; receptionist $5,500; hairdressers 1-4
$6,500 / 8,000 / 12,000 / 15,000 (their four squares share one footprint: only one is live at a time); cleaner $6,000.
One purchase live at a time (UnlockStation chain; SquareAudit `SalonChain` checks it).

**Customers.** One cash pile at reception, a waiting room with 4 seats (`seatedWaiting`), `CallNextStation` (NEXT square;
the receptionist is a WorkerAgent on it). Four hairstyles (Short 80, Bob 110, Long 160, Quiff 120), each an
`IProducer`/`ProductOffer` with `ItemDefinition.service = true` (the order bubble shows the icon only). Interval 40 s,
demand 0.25/capacity, max 11. Each chair after the first adds 7 capacity and each hired hairdresser 4 (a `ProductOffer` under the worker), so a full salon sees about 13 arrivals a minute and keeps its 4 chairs busy (measured 3.1 of 4 busy, 35 haircuts per chair in 10 min). A hairdresser takes 10 s per haircut (`ServiceSeconds`).

**Chair loop.** `SalonChairStation` (STYLE square). Player: after `openAfter`, `HaircutMinigame.Open` (Look, Cut, Dry,
Result; 1-3 stars; 3 stars pays about price x1.75 with the tip, `tipShare` 0.2/0.45/0.75); closing the overlay abandons
and the square re-arms on exit. Hairdresser worker: plain timed service, no minigame. `ServeWhole` is used so the
customer leaves at once. `HairMess` listens to `ServiceChair.Released`: each served haircut adds `BagsPerService` (3)
hair bags to its saved `ItemBuffer`; while non-empty the SWEEP square is on and the chair is `dirtyWhen`-blocked.
The player sweeps (hair bag on the CarryStack) to the side-door bin (`WorkerBinStation`, a DiscardStation that workers
may use). Cleaner = WorkerAgent, route heap -> bin, `deliverPartialLoads` (extended in WorkerAgent to also apply when
the pickup is a `CollectStation` whose source is empty).
The SWEEP square is switched off when the heap is gone and back on with the next haircut, so `HairMess` calls
`StationBase.ClearOccupants()` first; without it the sweeper's record outlived the first sweep and the next heap was
swept into their arms from anywhere (hair "spawning in the hands").

**Models.** hairdresser = Customer model recoloured, receptionist = Cashier model, cleaner = Farmer model.

**Tasks.** TaskBoard `AddSalonGoals`: Call the next customer, Give a haircut, Sweep up the hair, Take the hair to the bin.

**Economy (market_sim.py, `Salon` agent).** Reveal at the gardener (about 107.8 min); repair+paint paid 2.8 min after,
OPEN 1.3-1.5 min gaps; last hire about 36.6 min after the reveal (gaps 3.5-4.5 min); salon about $1,800/min fully staffed; total about $3,400/min.

**Save ids.** `town.salon.*` and `town.hire.*`. The hair heap quantity is saved; inactive objects do not register, so a
save restores along the reveal chain.

**Moved for the salon.** Florist `ExitPoint` x 18 -> 24; town EdgeEast 16 -> 22; the ATM now stands at (1.7, 3.0).
