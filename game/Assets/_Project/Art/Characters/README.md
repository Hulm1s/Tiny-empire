# Postavy — kde co je a jak to měnit

## Kde jsou soubory

| Co | Kde |
|---|---|
| **Zdrojový model** (původní, 991k trojúhelníků) | `D:\iosGame\models\low+poly+humanoid+3d+model.glb` |
| **Blender skripty** (retopo, rig, animace, role) | `D:\iosGame\models\pipeline\` |
| **Blender soubor s rigem a klipy** | `D:\iosGame\models\pipeline\humanoid_animated.blend` |
| **Modely v Unity** | `Assets/_Project/Art/Characters/` |
| **Materiály postav** | `Assets/_Project/Materials/Characters/` |
| **Animator** | `Assets/_Project/Animation/Villager.controller` |

## Jak to je poskládané

Jeden skeleton, jedny animace, čtyři meshe.

- `Villager_Rig.fbx` — **vlastní kostru a všechny čtyři klipy** (Idle, Walk, CarryIdle,
  CarryWalk). Nic jiného z něj hra nebere.
- `Villager_Owner / Farmer / Cashier / Customer.fbx` — jen mesh na té samé kostře.
  Import je automaticky nastaví na *Copy From Other Avatar* → `Villager_Rig`.

Proto stačí jeden animator controller pro všechny. Nová role = nový mesh a nic víc.

`CharacterImportSettings.cs` nastaví import sám (flat shading, Generic rig, avatar,
přejmenování klipů z `Rig|Walk` na `Walk`, zapnutí smyčky, URP materiály). Do import
inspectoru nemusíš sahat.

## Nejčastější úprava: přebarvit roli

Nejrychlejší cesta a **nevyžaduje Blender**. Materiály jsou v
`Assets/_Project/Materials/Characters/`, pojmenované `Role_Část`:

```
Farmer_Shirt.mat   Farmer_Trouser.mat   Farmer_Hat.mat   ...
```

Změň Base Color a hotovo. Sloty na meshi jsou vždy v tomhle pořadí:

| Slot | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| | Boty | Kalhoty | Triko | Kůže | Klobouk | Lem klobouku |

Kód na ně odkazuje přes `CharacterLibrary.ShirtSlot` atd., ne přes čísla.

## Upravit proporce nebo tvar

1. Otevři `models\pipeline\humanoid_animated.blend`.
2. Uprav mesh objektu `Body`. **Nesahej na jména kostí** — `Hand.L`, `Thigh.R` atd.
   Unity je páruje podle jmen a přejmenování rozbije avatar.
3. Spusť export:

```bash
"D:\Blender\blender.exe" --background --python "D:\iosGame\models\pipeline\export_unity.py" -- "D:\iosGame\models\pipeline\humanoid_animated.blend" "D:\iosGame\game\Assets\_Project\Art\Characters"
```

4. V Unity: `Tycoon → Reimport Character Models`.

Scéna se aktualizuje sama — postavy jsou prefab instance, ne kopie.

## Upravit animace

Klipy se klíčují kódem v `models\pipeline\animate.py` (funkce `walk_clip`, sekce
`Idle` a `CarryIdle`). Čísla jsou stupně rotace kostí. Pak:

```bash
"D:\Blender\blender.exe" --background --python "D:\iosGame\models\pipeline\animate.py" -- "D:\iosGame\models\pipeline\humanoid_rigged.blend" "D:\iosGame\models\pipeline"
```

a znovu export + reimport jako výše.

## Přidat novou roli

1. V `export_unity.py` přidej položku do `ROLES` — čtyři barvy a volitelně klobouk.
2. Export + `Tycoon → Reimport Character Models`.
3. Do `CharacterLibrary.cs` přidej konstantu s jejím jménem.
4. Použij ji: `CharacterLibrary.Spawn(CharacterLibrary.TvojeRole, parent)`.

Žádný nový controller, žádné nové klipy.

## Co se rozbije a jak to poznat

- **Postava se nehýbe** → nemá avatar. Spusť `Tycoon → Verify Character Models`,
  vypíše `avatarSetup` a `source` pro každou roli.
- **Postava je růžová** → materiál není URP. Smaž ho z
  `Materials/Characters/` a reimportuj; postprocessor ho vyrobí znovu správně.
- **Klobouk zůstává stát na místě** → jeho vrcholy nejsou přiváhované na kost `Head`.
  Dělá to `roles.py` / `export_unity.py` při joinu.

## Čeho se nedotýkat

Modely jsou jen vizuál. Hra je pověšená na `StationBase`, `InteractionSquare`,
`CarryStack`, `WorkerAgent`, `CustomerAgent` a `SaveIdentity` — postava je potomek
objektu `Visual` a nic z gameplay logiky o ní neví. `CharacterVisual` jen kouká na
rychlost a carry stack a přepíná stav animátoru; nic neřídí.

Nesené zboží visí na `CarryAnchor`, který je v `CharacterLibrary.HandAnchor`
(před hrudníkem, ve výšce rukou). Dřív byl nad hlavou.
