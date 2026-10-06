# CHANGE MAP

The cheat sheet. "I want to change X" → the exact file and line. Detail and warnings are in
`EDITING_GUIDE.md`.

**Shorthand:** **FSB** = `game/Assets/_Project/Editor/FarmSceneBuilder.cs` ·
**LBK** = `game/Assets/_Project/Editor/LevelBuildKit.cs` ·
script paths are under `game/Assets/_Project/Scripts/`.

**Almost everything below needs a scene rebuild afterwards**, and a layout audit if it moved,
resized or added anything.

---

## Money and prices

| I want to change… | Go to | Current |
|---|---|---|
| **ALL prices at once (test mode)** | **FSB:44 `TestPriceOverride`** | **`10d` — set to `0d` for the real economy** |
| Unlock price — Field | FSB:419 | 600 |
| Unlock price — Coop | FSB:423 | 900 |
| Unlock price — Dairy Till | FSB:427 | 1400 |
| Unlock price — Dairy | FSB:443 | 3200 |
| Unlock price — Meadow | FSB:455 | 4500 |
| Unlock price — Cow Shed | FSB:459 | 6000 |
| Hire prices (all 8) | FSB:379-410, `price:` | 250 / 400 / 700 / 800 / 1500 / 1700 / 2200 / 2400 |
| Animal base price | FSB:320, 329, 357, 366, `unitPrice:` | 120 / 150 / 400 / 450 |
| Animal price growth per purchase | `Stations/UpgradeStation.cs:30` `priceGrowth` | 2.2× (global) |
| How fast a purchase square drains money | FSB:564 (gates), FSB:595 (hires) | `max(4, price/40)` / `max(6, price/40)` |
| Animal purchase drain rate | `Stations/UpgradeStation.cs:33` `payPerTick` | 5 |
| **What a product sells for** | **FSB:129-147 `price:`** | corn 1, egg 5, hay 1, milk 12 |
| Repair cost per point | FSB:322, 331, 359, 368 | 0.25 coops / 0.35 sheds |
| Free repair fraction (**never 0**) | `Stations/RepairStation.cs:33` | 0.25 |
| Worker pay per delivered unit | FSB:379-410, `feePerDelivery:` | 0.5 / 0.6 / 0.8 / 1.4 |
| Starting balance | `Core/SaveSystem.cs:81` | $0 |
| Money display format | `Core/Wallet.cs:48-60` | `$950`, `$1.2K`, `$3.4M` |

> ⚠ Do **not** edit `Assets/_Project/Configs/Item_*.asset` — they are regenerated from
> `FSB:125-149` on every scene build.

---

## Production

| I want to change… | Go to | Current |
|---|---|---|
| Production speed | FSB:318, 327, 355, 364, `secondsPerOutput:` | 6 s egg, 10 s milk |
| Feed hopper capacity | same lines, `inputCapacity:` | 10 everywhere |
| Output basket capacity | same lines, `outputCapacity:` | 12 coops, 10 sheds |
| Animals per building | same lines, `startUnits:` / `maxUnits:` | 1 / 3 |
| Input units per output | `LBK:686` `inputPerOutput` | 1 |
| Field plot count | FSB:305, 309, 342, 347, `plots:` | 8 |
| Field regrow time | same lines, `regrowSeconds:` | 2.2 corn, 2.6 hay |
| Field size | FSB:213 `FieldSize` | 4.5 × 3.5 |
| Spoil time | FSB:137, 147, `spoilSeconds:` | 90 s egg, 120 s milk |
| Whether an item spoils | FSB:129-147, `perishable:` | egg + milk only |
| Wear per unit produced | FSB:319, 328, 356, 365, `wearPerOutput:` | 1.5 coops, 2.0 sheds |
| Max condition | `Upkeep/Durability.cs:18` `max` | 100 |
| Condition restored per repair ring | `Stations/RepairStation.cs:22` `repairPerTask` | 25 |
| Repair ring duration | `Stations/StationBase.cs:65` `taskDuration` | 1.2 s |
| Warning threshold | `Upkeep/Durability.cs:27` `warnBelow` | 0.3 |

> `Durability.repairPerSecond` is a **dead field** — nothing reads it.

---

## Customers and the market

| I want to change… | Go to | Current |
|---|---|---|
| **Spawn rate** | FSB:239 `BaseSpawnInterval` | 2.5 s |
| How much the farm's size speeds it up | FSB:240 `DemandPerCapacity` | 0.35 |
| Cap on that | FSB:241 `MaxDemand` | 3× |
| **Patience** | `Customers/CustomerAgent.cs:28` | 75 s |
| Customer walk speed | `Customers/CustomerAgent.cs:20` | 2.7 m/s |
| Order size | `Customers/CustomerQueue.cs` `minOrder`/`maxOrder` | 1–4 |
| Queue length | FSB:268, 271, `queueLength:` | 3 per till |
| Queue slot spacing | `LBK:1098-1099` | 2.2 m |
| Reputation per happy / angry | `Customers/CustomerQueue.cs` | +0.05 / −0.14 |
| Reputation floor | `Customers/CustomerQueue.cs` `minReputation` | 0.25 |
| Price multiplier curve | `Customers/CustomerQueue.cs` `PriceMultiplier` | `Lerp(0.6, 1.15, rep)` |
| **What a till sells** | **FSB:267-271** | A = Egg only, B = Milk only ⚠ |
| Which end shoppers arrive from | FSB:268, 271, `enterFromWest:` | A west, B east |
| How far shoppers spawn along the road | FSB:254 `walkHalfLength` | 19 m |

> ⚠ **One product per till.** `CustomerQueue.Front` still head-of-line blocks; the single-product
> menus are what stop it firing.

---

## Player, workers and camera

| I want to change… | Go to | Current |
|---|---|---|
| **Player speed** | FSB:820 `motor.moveSpeed` | 5.2 |
| **Player carry capacity** | FSB:824 `carry.capacity` | 8 |
| Player start position | FSB:67 | (−10.5, 0.2, −11) |
| Player acceleration / turn | `Player/PlayerMotor.cs` | 40 / 900°/s |
| **Worker speed (all of them)** | `Upkeep/WorkerAgent.cs:31` `moveSpeed` | 3.1 |
| Worker carry capacity | `LBK:626` `capacity = 4` | 4 |
| Worker route | FSB:379-410, `pickup:` / `dropoff:` | see `GAMEPLAY_SYSTEMS.md` §10 |
| Worker stand-off distance / delay | `Upkeep/WorkerAgent.cs:99,102` | 2.9 m / 0.8 s |
| Where carried goods sit on a character | `Editor/CharacterLibrary.cs` `HandAnchor` | (0, 0.86, 0.42) |
| Transfer rate (every square) | `Stations/StationBase.cs:62` `tickInterval` | 0.18 s/unit |
| Bin transfer rate | `LBK:863` | 0.09 s/unit |
| **Camera angle** | FSB:788 `rig.pitchYaw` | (50, 75) ⚠ pitch is load-bearing |
| **Camera zoom** | FSB:790 `rig.orthographicSize` | 9.5 |
| Camera follow smoothing | `Core/IsometricCameraRig.cs` `smoothTime` | 0.18 |
| Camera framing nudge | FSB:792 `rig.lookOffset` | (0, 0, 0.8) |
| Joystick size / dead zone | `UI/VirtualJoystick.cs:23,25` | 170 px / 0.08 |

---

## Layout and the world

| I want to change… | Go to | Current |
|---|---|---|
| Column spacing | FSB:191 `ColumnSpacing` | 10.5 ⚠ under ~9.3 overlaps |
| Column positions | FSB:193-196 | −15.75 / −5.25 / +5.25 / +15.75 |
| Row positions | FSB:215-217 | market −14, buildings −7.85, fields −1.05 |
| Till positions | FSB:200-201 | egg −10.5, milk +10.5 |
| **Map boundary** | **FSB:231-234** | W −25.5, E +25.5, N +7, S −23 |
| Invisible walls | FSB:649-679 `BuildBoundary` | thickness 2, height 6 |
| **Fence** | FSB:681-706 `BuildFence` | follows the same lines, gap at the road |
| **Road** position / depth | FSB:237 `RoadZ`, `LBK:1024` | −18.6, 2.8 m deep |
| Road length | FSB:254 `roadHalfLength` | 34 m |
| Ground plane size | FSB:748 | 160 × 160 m |
| Grass spread / density | FSB:619, 634 | 11 m beyond the fence, 2200 tufts |
| Grass keep-clear areas | FSB:628-631 | two rectangles |
| Level rotation | FSB:35 `LevelYaw` | 45° |
| Bin positions | FSB:293-301 | west/centre/east of the market row |
| Sun angle / intensity | FSB:754, 757 | (48, 30, 0) / 1.15 |
| Sky colour | FSB:774 | (0.47, 0.72, 0.87) |

---

## Interaction squares

| I want to change… | Go to | Current |
|---|---|---|
| Feed square position / size | `LBK:767` | (0,0,+2.8), 3.4 × 2.0 |
| Collect square | `LBK:771` | (0,0,−2.8), 3.4 × 2.0 |
| Fix square | `LBK:778` | (−3.9,0,−1.6), 1.8 × 1.8 |
| Buy square | `LBK:783` | (−3.9,0,+1.6), 1.8 × 1.8 |
| Bin square | `LBK:856` | (0,0,+1.7), 1.7 × 1.5 |
| Serve square | `LBK:1067` | centre, 3.6 × 2.2 |
| Harvest square | `LBK:1195` | = field size |
| Gate squares | FSB:418-460 | per gate |
| Hire squares | FSB:589 | 1.8 × 1.8 |
| Hire square placement rules | FSB:509 `NorthOfField`, FSB:543 `AtTill` | |
| **Minimum card size (everywhere)** | `UI/InteractionSquare.cs:64` `DefaultMinCard` | (2.5, 2.1) |
| One square's card minimum | the `smallestCard:` argument | only the bin uses it |
| Content block size | `UI/InteractionSquare.cs:74` `ContentSize` | (2.5, 2.1) |
| Square float height / pulse | `UI/InteractionSquare.cs:38,41,43` | 0.09 / 0.045 / 2.1 |
| Whether squares face the camera | `UI/InteractionSquare.cs:323` `ScreenAlign` | 0 = world-aligned |
| Band proportions inside a square | `UI/InteractionSquare.cs:183-187` | |
| **A square's label text** | the station's `ActionLabel` override, or the `label` argument | |
| **A square's number** | the station's `StatusValue` override | |
| A square's icon | the station's `Icon` / `IconItem` override, or `UnlockStation.icon` | |
| A square's colour | the `Color` argument to `Station<T>()` | |

---

## World UI

| I want to change… | Go to |
|---|---|
| **An icon's drawing** | `UI/IconFactory.cs`, the `Draw<Name>(Painter p)` method |
| Add a new icon | `UI/IconFactory.cs` — `SquareIcon` enum + `Action()`, **or** `For()` **and** `HasArtwork()` **and** `Editor/IconSheet.cs:39` |
| Order bubble size / layout | `UI/OrderBubble.cs:33-109` |
| Order bubble height / scale | `UI/OrderBubble.cs:19,22` (2.25 / 0.01) |
| Patience bar colours | `UI/OrderBubble.cs:153-155` |
| **Capacity board format** | `UI/CapacitySign.cs:209` — hardcoded `CURRENT / MAX` |
| Capacity board thresholds / colours | `UI/CapacitySign.cs:70-78` |
| Capacity board height | `LBK:748` (2.65) |
| **Warning marker thresholds** | `UI/AttentionMarker.cs:32,35` (0.6 / 0.2) |
| Warning marker colours | `UI/AttentionMarker.cs:63-65` |
| Warning marker motion | `UI/AttentionMarker.cs:186-195` |
| Warning marker height | `LBK:754` (4.35) |
| **A popup's text/colour/height** | the calling station — see `GAMEPLAY_SYSTEMS.md` |
| Popup lifetime / rise / merge | `UI/WorldFeedback.cs:21,104,105` |
| Dashed outline look | `UI/UIFactory.cs:74-145` |
| The shared font | `UI/UIFactory.cs:31-32` (`LegacyRuntime.ttf`) |
| Off-screen culling margin | `UI/WorldUi.cs:40` (0.3) |

---

## HUD

| I want to change… | Go to |
|---|---|
| Money readout | `UI/HudRoot.cs:127-144` |
| Alert text | `UI/HudRoot.cs:146-156` |
| Debug readout (`?debug=1`) | `UI/HudRoot.cs:158-168` |
| HUD reference resolution | `UI/HudRoot.cs:115-117` (1080 × 1920, match 0.35) |
| Pause menu rows | `UI/PauseMenu.cs:60-100` |
| Pause button | `UI/PauseMenu.cs:42-49` |

> ⚠ **No HUD action buttons.** State readouts, the joystick and the pause/settings panel only.

---

## Characters and models

| I want to change… | Go to |
|---|---|
| **A role's colours** | `Materials/Characters/<Role>_<Part>.mat` — ⚠ except worker **shirts** |
| **A worker's shirt tint** | FSB:379-410, the `color:` argument (scene-embedded, needs a rebuild) |
| A customer's shirt tint | runtime, `Customers/CustomerQueue.cs` |
| Which model a hire uses | FSB:581-583 — ⚠ derived from the role **label string** |
| Body shape / proportions | `models/pipeline/humanoid_animated.blend`, object `Body` |
| An animation clip | `models/pipeline/animate.py` |
| A hat shape | `models/pipeline/export_unity.py` |
| Canonical role colours | `models/pipeline/export_unity.py:87-98` |
| Walk/idle threshold | `Scripts/Characters/CharacterVisual.cs` `walkThreshold` (0.25 m/s) |
| Add a new role | `export_unity.py` + `Editor/CharacterLibrary.cs` (3 places) |

> Full procedure, and the avatar gotcha, in `CHARACTERS_AND_MODELS.md`.

---

## Save system

| I want to change… | Go to |
|---|---|
| Autosave interval | `Core/GameRoot.cs:21` (15 s) |
| Offline cap | `Core/GameClock.cs:23` (8 h) |
| **Reset everyone's progress deliberately** | `Core/SaveSystem.cs:16` — bump `tycoon.save.v1` → `v2` |
| Add a saved field | that station's `State` struct + `CaptureState`/`RestoreState` |
| Make a new object saveable | implement `ISaveable`, then `LevelBuildKit.Identify(go, "farm.…")` |

> ⚠ **Never change an existing save id string or a saveable component's class name.** See
> `SAVE_SYSTEM.md` §11.

---

## Build, deploy, platform

| I want to change… | Go to |
|---|---|
| Web build output path | `Editor/TycoonBuild.cs:31-32` (→ `D:\iosGame\docs`) |
| Compression / exception support / stripping | `Editor/TycoonBuild.cs:148-167` |
| Orientation | `Editor/TycoonBuild.cs:136-141` (portrait only) |
| Version stamp format | `Editor/TycoonBuild.cs:134` |
| Which scene is built | `Editor/TycoonBuild.cs:106-118` |
| **URP render settings for web** | `Editor/WebPlatformSetup.cs` — ⚠ the black-screen fix |
| Render scale | `Assets/Settings/Mobile_RPAsset.asset` (0.8) |
| **The loading page / PLAY button** | `Assets/WebGLTemplates/MobilePWA/index.html` |
| Device pixel ratio cap | `index.html:114-117` (capped at 2) |
| PWA name / icons / theme | `MobilePWA/manifest.webmanifest`, `TemplateData/icons/` |
| Offline caching strategy | `MobilePWA/ServiceWorker.js` |
| Page styling / touch suppression | `MobilePWA/TemplateData/style.css` |
| **Deployment** | `publish.sh` → `gh-pages` → `https://hulm1s.github.io/Tiny-empire/` |
| What is gitignored | `.gitignore` |

---

## Diagnostics

| I want to… | Run |
|---|---|
| Check the layout | `Tycoon → Audit Interaction Squares` → read `[Audit] Nothing overlaps.` |
| See what the icons actually look like | `Tycoon → Export Icon Sheet` → `D:\iosGame\icon-sheet.png` |
| Check the character rigs | `Tycoon → Verify Character Models` |
| Debug the animator | `Tycoon → Diagnose Character Animation` |
| Cheap build smoke test | `-executeMethod Tycoon.EditorTools.TycoonBuild.Validate` |
| On-screen fps / position / carry | add `?debug=1` to the URL — no rebuild |
| Wipe a save | pause menu → `DELETE SAVE` (two taps) |
