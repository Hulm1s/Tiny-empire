# TESTING CHECKLIST

Concrete things to check, in the order that finds problems fastest. Written against the game as
it actually is — every item names what you should see and where it breaks if it does not.

**The point of this document is to let you deploy once per batch of changes instead of once per
change.** See `DEVELOPMENT_WORKFLOW.md` §"One deploy per feature".

---

## 0. Pick a tier

| Tier | When | Time |
|---|---|---|
| **QUICK** | one value changed — a price, a colour, a label, a position | ~3 min |
| **GAMEPLAY** | a mechanic, a station, a worker, customers, the economy | ~15 min |
| **FULL REGRESSION** | layout changes, save changes, new content, or before any release | ~40 min |

Each tier includes the one above it.

---

## QUICK (always, no exceptions)

### Build
- [ ] **Local server stopped** before building. A running server holds
      `docs/Build/docs.loader.js` open and the build fails with *"the requested operation cannot
      be performed on a file with a user-mapped section open"*.
- [ ] Scene build logged `SCENE_OK`.
- [ ] `grep "error CS" build-scene.log` → nothing.
- [ ] If the change touched any position, size, or new object: run `SquareAudit.Run` and check
      the log says **`[Audit] Nothing overlaps.`**
      **⚠ Do NOT gate on `AUDIT_OK`.** That token and exit code 0 are printed unconditionally,
      even when the audit finds faults (`SquareAudit.cs:129-132`). A failing run says
      `[Audit] <n> faults.` and still prints `AUDIT_OK`.
- [ ] Web build logged `BUILD_OK` (only needed if you are going to look at it in a browser).

### In the browser
- [ ] Opened in a **fresh tab**. Reloading the same tab repeatedly degrades the WebGL context
      and produces shader errors that look like a broken build but are not.
- [ ] Game loads past the PLAY button.
- [ ] Console has no red errors.
- [ ] **The thing you changed is actually different.**

---

## GAMEPLAY

Everything in QUICK, plus:

### Start
- [ ] PLAY button appears, tapping it starts the game.
- [ ] Player spawns between coop A's collect square and the egg till, facing the farm.
- [ ] Camera is looking down at ~50°, player roughly centred.
- [ ] Money readout top-centre shows the expected starting balance (**$0** on a fresh save).
- [ ] Pause button top-right.
- [ ] Joystick is invisible until you touch the lower part of the screen.

### Movement and controls
- [ ] Joystick appears where your thumb lands (it floats — it is not fixed to one corner).
- [ ] Player walks in the direction you push, relative to the screen, not to the world grid.
- [ ] **WASD and arrow keys work** on desktop — this is the normal way to test.
- [ ] Walking into a building stops you; you do not pass through it.
- [ ] Hold a direction for 20 seconds — you hit the fence and stop. **You must not fall out of
      the world.**
- [ ] The joystick ring disappears when you let go. (It getting stuck on screen is a known
      browser failure mode that `VirtualJoystick.Update` re-asserts against.)

### The egg chain (the opening loop)
- [ ] Stand in a **corn field** → the HARVEST square fills your arms one corn at a time, and a
      `+N Corn` popup rises.
- [ ] The field's ripe plots visibly disappear as you take them.
- [ ] Carry stack is visible above/in front of the character and grows item by item.
- [ ] Stack stops at **8** items (player capacity).
- [ ] Stand in coop A's **FEED** square → corn drains out of your arms into the hopper.
- [ ] The coop's capacity board shows `N / 10` and counts up.
- [ ] Wait → the board stops saying `NEEDS FEED` and shows `1/3 working`.
- [ ] Eggs accumulate; stand in **COLLECT** → eggs fill your arms, `+N Egg` popup.
- [ ] Walk to the **egg till (counter A)** and stand in SERVE → the front customer is served one
      egg at a time, `+$N` popups appear, money goes up.

### The bin
- [ ] Fill your arms with something, stand in a **BIN** square → items are thrown away with a
      `-N` popup, and **no money is returned**.
- [ ] The bin square is noticeably **smaller** than other squares — it is deliberately harder to
      step into by accident.
- [ ] There are **three** bins: west end, centre, east end of the market row.

### Customers
- [ ] Shoppers walk in along the road and queue at the counter.
- [ ] Each shows a bubble with an **icon** (not just a coloured dot) and a `×N` count.
- [ ] Egg and milk bubbles are **distinguishable at a glance** — both products are near-white,
      so this is exactly what the drawn icons are for.
- [ ] A patience bar under the icon drains green → amber → red.
- [ ] Served customers show `THANKS!` and walk out the far end.
- [ ] A customer who times out shows `LEFT ANGRY` and leaves.
- [ ] Queue holds at most **3** per till; nobody spawns while it is full.
- [ ] **Counter A never shows a milk order. Counter B never shows an egg order.** If it does,
      the `sells` arrays got crossed — this was a real bug and it blocks the whole queue.
- [ ] **Counter B spawns nobody at all before a cow shed is unlocked** and logs no errors while
      standing empty.
- [ ] Shoppers for the two tills arrive from **opposite ends** of the street and do not walk
      through each other.

### Workers
Buy a hire square (prices are $10 in test mode — see `PROJECT_STRUCTURE.md` §11):
- [ ] Standing in a hire square drains money gradually, and the square shows progress.
- [ ] Walking out part-way keeps the partial payment.
- [ ] On completion a worker appears and starts its route.
- [ ] The worker walks **around** buildings, not into them.
- [ ] It fills at its pickup, walks to its dropoff, empties, walks back. Repeats indefinitely.
- [ ] Money ticks **down** slightly per delivery (piece rate).
- [ ] **Player priority:** stand in a square the worker wants → the worker walks a couple of
      metres back down its own route and waits. It does not shove in, and it does not drop its
      load. When you leave, it resumes after about a second.
- [ ] **No worker is stuck.** Watch each hired worker complete at least one full round trip.
      A worker standing still with a full stack means its dropoff does not exist or does not
      accept what it carries.

### Buildings
- [ ] The capacity board over a building reads `CURRENT / MAX` (e.g. `6 / 10`), not a percentage.
- [ ] It shows the **feed** level, with the feed item's own icon (corn over a coop, hay over a
      shed).
- [ ] `FULL - COLLECT` appears when the output basket fills, and production stops.
- [ ] `NEEDS FEED` appears when the hopper empties.
- [ ] Buying an animal at the **BUY** square makes one visibly appear in the pen.
- [ ] More animals = faster production and more customers arriving.

### Condition and repair
- [ ] A building's condition drops as it produces.
- [ ] Below 60% a bouncing `!` marker appears above it, getting faster, bigger and redder as it
      worsens. **It must not blink.**
- [ ] Stand in **FIX** → a progress ring fills, `+N%` popups rise, `FIXED` on completion.
- [ ] **Repair works with $0 in the wallet.** This is a hard design rule — a jammed machine plus
      no money must never be terminal. If repair stalls at zero money, that is a release blocker.

---

## FULL REGRESSION

Everything above, plus:

### Progression, in order
Walk the whole unlock chain. In test-price mode this takes a couple of minutes.

- [ ] `Field` (corn B) — a second corn field appears.
- [ ] `Coop` (coop B) — **the coop and both of its hire squares appear together.** A hire must
      never be purchasable before the building its worker delivers into exists.
- [ ] `Dairy Till` (counter B) — the milk counter appears and stands empty.
- [ ] `Dairy` (cow shed A) — **the shed, meadow A, and both dairy hire squares appear together.**
      Hay must never be obtainable before something that eats it exists (this was a softlock).
- [ ] Milk now starts appearing in orders at counter B, and only now.
- [ ] `Meadow` (hay B).
- [ ] `Cow Shed` (cow shed B) + its two hires.
- [ ] Each gate's square disappears once paid.
- [ ] Partial payment on every gate survives walking away and coming back.

### Save and load
See `SAVE_SYSTEM.md` §14 for the detail. Minimum:
- [ ] Make progress: earn money, part-pay an unlock, leave items in a hopper, buy an animal, wear
      a building down.
- [ ] **Wait 15+ seconds** (the autosave interval) or switch tabs and back.
- [ ] Reload the page.
- [ ] Money, unlock progress, buffer contents, animal count, durability and which gates are open
      all survived.
- [ ] **No `[SaveKeys]` warnings in the console.** Any such warning means a saveable object is
      missing its `SaveIdentity` and will lose progress later.
- [ ] Pause → `DELETE SAVE` (two taps) → reload → **everything is genuinely gone.** Partial
      payments coming back is a specific bug this code is built to prevent.
- [ ] Offline catch-up: save, close the tab, move the system clock forward a few minutes, reopen
      → fields regrew, perishables rotted. Beyond 8 hours it is clamped.

### UI
- [ ] Every square's label fits inside its card. Nothing hangs over the grass or vanishes behind
      a building.
- [ ] Labels are legible at phone size: `FEED`, `FIX`, `COLLECT`, `SERVE`, `HARVEST`.
- [ ] Walk between a coop's `FIX` and `BUY` squares (they are **3.2 m apart**, the tightest pair
      on the farm) — the two cards must not overlap or touch.
- [ ] Icons render correctly — run `IconSheet.Export` and look at `icon-sheet.png`. Every icon
      should be a recognisable object with an outline and shading. **Arrows must have heads;
      corn must have leaves; the bin must look like a closed can, not a lid over stripes.**
      (Those three were the visible symptoms of a real bug where every triangle silently failed
      to draw.)
- [ ] Money readout pulses when you earn.
- [ ] Pause menu: `RESUME` resumes, `SOUND` toggles and persists across a reload, `DELETE SAVE`
      needs two taps.
- [ ] Pausing freezes production, customers and decay together — not just movement.

### Mobile viewport
- [ ] Tested at **375 × 812** in a fresh tab.
- [ ] HUD clears the notch and the home indicator area.
- [ ] Joystick is not under the home bar (a joystick there swipes the app away).
- [ ] Squares and bubbles are readable without zooming.
- [ ] Nothing runs off the left or right edge.

### Camera and world edges
- [ ] Walk to each of the four fence lines. **The edge of the ground plane is never visible.**
- [ ] The road passes through a gap in the fence on both sides, rather than stopping at a plank.
- [ ] Grass does not poke up through any interaction square.

### Performance
- [ ] Append `?debug=1` to the URL for the on-screen readout (fps, dt, position, carry).
      No rebuild needed.
- [ ] Frame rate holds on a fully built farm with all eight workers hired and both queues busy.
      The reference measurement from an earlier pass was a median of **2.6 ms** of main-thread CPU
      per frame; bigger UI or new per-frame work must not undo that.
- [ ] Walk the whole map — no stutter when many squares come on screen at once.

### Console
- [ ] No red errors anywhere during a full session.
- [ ] No `[SaveKeys]` warnings.
- [ ] No `[FarmSceneBuilder]` route warnings in the build log (`... carries X to Y, which does not
      sell it`).

---

## Release blockers

Do not publish if any of these is true:

- [ ] `TestPriceOverride` in `FarmSceneBuilder.cs:44` is **not** `0d`. Everything costs $10.
- [ ] `SquareAudit.Run` did not log `[Audit] Nothing overlaps.` (do not rely on `AUDIT_OK` —
      it is printed even on failure).
- [ ] Repair does not work at $0.
- [ ] A worker can be hired before its route exists.
- [ ] Any `[SaveKeys]` warning appears.
- [ ] Red console errors in the built player.
- [ ] A save does not survive a reload.
