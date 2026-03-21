# Hex Territory Control v2 — Business Logic & Functionality

## Overview

AI simulace boje dvou asymetrických týmů (Roboti vs. Mutanti) na hexagonálním hřišti. Týmy se učí strategii přes reinforcement learning (MA-POCA). Cílem každého týmu je obsadit 70% (konfigurovatelné) území.

---

## Herní pole

- Hrací pole má tvar **velkého hexagonu** složeného z malých hexagonálních dílků.
- Velikost pole: parametr **Board Side** (default: 4 = 37 hexů).
- Vzorec: `3 * side^2 - 3 * side + 1` celkových hexů.
- Souřadnicový systém: **axiální souřadnice (q, r)** s odvozeným `s = -q - r`.
- Hex layout: **flat-top**.

## Základny

- Každý tým má **základnu** v protilehlém rohu.
  - Roboti: levý dolní roh `(-max, max)`.
  - Mutanti: pravý horní roh `(max, -max)`.
- Velikost základny = `min(unitsPerTeam + 1, 4)` hexů.
- Základnové hexy: permanentně vlastněny, nelze obsadit, spawn pointy.
- **Nepřátelské jednotky nemohou vstoupit na základnu protivníka.**
- **Jednotky na vlastní základně jsou imunní vůči útoku.**
- **Base regeneration:** Jednotka na vlastním base hexu regeneruje **+baseRegenPerStep energie/krok** (default: 2).
- Na základně **nelze stavět** zdi ani sliz.

## Energie (Universal Resource)

Energie je univerzální zdroj — slouží jako HP i currency pro akce.

| Parametr | Hodnota |
|----------|---------|
| Max energie | 15 |
| Respawn cooldown | 10 kroků |
| Smrt | při 0 energie |
| Respawn | plná energie na base hexu |

## Týmy a jednotky

### Obecné
- Dva týmy: **Robot** (modrý) a **Mutant** (zelený).
- Počet jednotek na tým: konfigurovatelný (default: 4).
- Vizualizace: Roboti = modré blocky figury (1.8× scale), Mutanti = zelené blobby figury (1.8× scale).
- Každá jednotka má unikátní číslo (0-3) zobrazené nad hlavou.

### Smrt a respawn
- Jednotka s 0 energie **okamžitě teleportuje na volný base hex**.
- Mrtvá jednotka zůstává **viditelná** (černě podbarvená) a **blokuje hex**.
- Mrtvá jednotka **nemůže jednat** (move, attack, build, destroy).
- Na mrtvou jednotku **nelze útočit**.
- V pořadí tahů mrtvá jednotka **zabírá slot** ("waiting for respawn").
- Po uplynutí cooldownu se jednotka **respawnuje** s plnou energií.
- Animační fronta se při smrti **okamžitě vyčistí**.

### Energie vizualizace
- Energy bar nad jednotkou (zelená >60%, žlutá >30%, červená pod 30%).
- Číslo energie vedle baru.
- Model se **šedne odspodu nahoru** dle ztráty energie.
- Mrtvé jednotky jsou **černé** (near-black).

## Střídání tahů

- **Striktní alternace:** R, M, R, M, R, M...
- Všechny jednotky (živé i mrtvé) jsou zahrnuty v pořadí.
- Mrtvé jednotky: tick cooldown, po uplynutí respawn — tah "použit".
- **Startující tým:** prohrávající z minulé hry začíná (první hra/remíza → náhodně).
- **Startující tým alternuje každé kolo** v rámci hry.

## Akční prostor (25 akcí)

Jednoduchý diskrétní branch s 25 akcemi:

| Akce | Rozsah | Popis |
|------|--------|-------|
| Idle | 0 | Zůstat na místě (penalizováno -0.01) |
| Move | 1-6 | Pohyb ve směru 0-5 |
| Attack | 7-12 | Útok ve směru 0-5 |
| Build | 13-18 | Stavba ve směru 0-5 |
| DestroyWall | 19-24 | Zničení vlastní zdi ve směru 0-5 |

**Runtime guard:** Všechny akce kontrolují `IsValid*` PŘED provedením. Neplatná akce = Idle.

## Pohyb

- Diskrétní: z hexu na sousední hex (6 směrů).
- **Blokováno:** zdi (všechny), obsazené hexy (živé i mrtvé), nepřátelská základna, mimo mapu.
- **Neutrální hex:** volný vstup + automatický capture (zdarma).
- **Vlastní hex:** volný vstup.
- **Nepřátelský hex:** volný vstup + automatický capture (zdarma).
- **Robot vstup na nepřátelský sliz:** stojí slimeEntryCostRobot (default: 2) energie, sliz se zničí, hex přechází na robota.
- **Mutant na vlastním slimu:** volný vstup (regen řeší AbilitySystem).

## Boj (Combat)

Útok ve směru na sousední hex. **Pouze dva platné cíle:**

### Priorita útoku
1. **Nepřátelská jednotka** → útok na jednotku
2. **Nepřátelská zeď** → útok na zeď

**Neplatné cíle (útok selže):** neutrální hex, nepřátelský prázdný hex, sliz, vlastní jednotka, vlastní zeď, mrtvá jednotka, vlastní prázdný hex.

### Friendly fire
- **Zakázáno.** Jednotky nemohou útočit na spoluhráče.

### Útok na jednotku
| Parametr | Hodnota |
|----------|---------|
| Cena | 3 energie |
| Base damage | 4 energie |
| Counter-damage | žádný |

### Proximity bonusy
- **Shield Wall (Robot obránce):** -1 damage za každého sousedního Robot spojence (max -2).
- **Swarm (Mutant útočník):** +1 damage za každého sousedního Mutant spojence (max +2).
- Výsledný damage: `max(0, baseDamage + swarmBonus - shieldWallReduction)`.

### Útok na zeď
| Parametr | Hodnota |
|----------|---------|
| Cena | 2 energie |
| Damage | 1 HP zdi |
| Zeď zničena | při 0 HP |

### Vizuální feedback útoku
- **Červený flash** pod útočníkem (jen během tahu).
- **Oranžový flash** na cílovém hexu (jen během tahu).
- Spouštěno přímo z HexAgent (timer-based, ne frame-based).

## Struktury

### Invarianty
- **Sliz může existovat jen na Mutant území.** Při změně vlastníka se automaticky smaže.
- **Zeď může existovat jen na Robot území.** Při změně vlastníka se automaticky smaže.

### Zdi (Robot)
- Stavba na **sousední vlastní prázdný hex** (ne base, ne obsazený).
- Cena: **3 energie**.
- Zeď HP: **3** (max). Zobrazeno číslem nad zdí.
- Zdi **blokují veškerý pohyb** (vlastní i nepřátelský).
- Zničení vlastní zdi: **1 energie**.
- Vizuálně: vyvýšený hex (0.12), tmavší šedo-modrá, jasnější dle HP.

### Sliz (Mutant)
- Umístění na **vlastní hex pod jednotkou** (ne base, ne obsazený strukturou).
- Cena: **2 energie**.
- Mutant na vlastním slimu: **+1 energie/krok** regenerace.
- Robot vstup na nepřátelský sliz: **2 energie**, sliz zničen.
- Vizuálně: žlutozelená barva + šrafovaný overlay (diagonální pruhy) + mírná extruze (0.04).

## Herní smyčka (Game Loop)

Sekvenční tahový model se striktní alternací R/M:

1. **Výběr jednotky** — dle turn orderu (R, M, R, M...).
2. **Mrtvá jednotka?** → tick cooldown, respawn pokud ready, skip tah.
3. **Živá jednotka** → ML-Agents vybere akci (0-24) s runtime IsValid guardem.
4. **Provedení akce** — Move/Attack/Build/DestroyWall/Idle.
5. **Post-turn** — logování, teleport mrtvých na base, check win condition.
6. **Konec kola** — po vyčerpání všech jednotek.
7. **Schopnosti** — base regen, slime regen (per round).

## Podmínky výhry

- **Territory threshold:** 70% contestable hexů (včetně base) → okamžitá výhra.
- **Timeout:** po 600 krocích → tým s více hexy vyhrává.
- **Remíza:** při timeoutu se stejným počtem hexů.

## ML-Agents integrace

### Observations (69 floatů)
- **Vlastní stav (5):** q_norm, r_norm, energy/maxEnergy, alive, team(+1/-1).
- **6 sousedů × 10 hodnot (60):**
  - Ownership (3): neutral, own, enemy.
  - Structures (3): has_wall, wall_hp_norm, has_slime.
  - Units (2): has_enemy_unit, has_ally_unit.
  - enemy_energy_norm (1), is_base (1).
- **Globální (4):** own_territory_pct, enemy_territory_pct, step_progress, respawn_cooldown_norm.

### Actions (1 diskrétní branch, 25 hodnot)
- 0 = Idle, 1-6 = Move, 7-12 = Attack, 13-18 = Build, 19-24 = DestroyWall.
- Runtime IsValid guard: neplatná akce degraduje na Idle.
- Heuristic mode: vybírá jen z platných akcí.

### Action masking
- Move: blokováno pokud cíl je zeď, obsazený, base nepřítele, off-map, nebo slime bez energie.
- Attack: blokováno pokud na cíli není nepřítel ani zeď, nebo nedostatek energie.
- Build (Robot): blokováno pokud sousední hex není vlastní prázdný non-base, nebo nedostatek energie.
- Build (Mutant): blokováno pokud hex pod jednotkou není vlastní prázdný non-base, nebo nedostatek energie.
- DestroyWall: blokováno pokud sousední hex nemá vlastní zeď, nebo nedostatek energie.
- Dead: jen Idle povoleno.

### Reward shaping
| Reward | Hodnota |
|--------|---------|
| Kill bonus | +0.3 |
| Build reward | +0.08 |
| Capture per tile | +0.1 |
| Enemy loss per tile | +0.1 |
| Hex capture (near enemy) | +0.05 |
| Build adjacency | +0.03 per neighbor |
| Slime placement | +0.08 |
| Slime network | +0.04 per adjacent slime |
| Cohesion | +0.02 × cohesion ratio |
| Group split | +0.3 |
| Base connection | +0.005 |
| Frontline | +0.005 |
| Idle penalty | -0.01 |
| Step penalty | -0.001 |
| Win | +1.0 group reward |
| Loss | -1.0 group reward |
| Timeout win | +0.5 |
| Timeout loss | -0.5 |

### Trénink
- Trainer: **MA-POCA**.
- Dva brains: `HexRobot`, `HexMutant`.
- hidden_units: 256, num_layers: 2.
- batch_size: 512, buffer_size: 5120.
- Max steps: 600 per episode.
- Device: CPU (CUDA koliduje s Unity Editor).
- Konfigurace: `Assets/ML-Agents/config/hex_territory.yaml`.

## Konfigurovatelné parametry (GameConfig)

| Parametr | Hodnota | Popis |
|----------|---------|-------|
| boardSide | 4 | Velikost hřiště |
| unitsPerTeam | 4 | Počet jednotek |
| msPerTick | 337 | Rychlost simulace |
| winPercent | 70 | Procento k výhře |
| maxSteps | 600 | Max kroků za epizodu |
| unitMaxEnergy | 15 | Max energie |
| respawnCooldown | 10 | Kroky do respawnu |
| attackUnitCost | 3 | Cena útoku na jednotku |
| attackUnitDamage | 4 | Damage útoku na jednotku |
| attackWallCost | 2 | Cena útoku na zeď |
| attackWallDamage | 1 | Damage útoku na zeď |
| wallBuildCost | 3 | Cena stavby zdi |
| wallMaxHP | 3 | Max HP zdi |
| slimePlaceCost | 2 | Cena umístění slizu |
| destroyOwnWallCost | 1 | Cena zničení vlastní zdi |
| slimeEntryCostRobot | 2 | Cena vstupu Robota na nepřátelský sliz |
| baseRegenPerStep | 2 | Regenerace na base hexu |
| slimeRegenPerStep | 1 | Regenerace Mutanta na slimu |
| shieldWallMaxReduction | 2 | Max Shield Wall redukce |
| swarmMaxBonus | 2 | Max Swarm bonus |
| idlePenalty | -0.01 | Penalizace za Idle |
| killBonus | 0.3 | Odměna za kill |
| buildReward | 0.08 | Odměna za stavbu |

## Replay systém

### JSONL formát
- Každá hra = jeden `.jsonl` soubor v `Replays/`.
- Řádky: header → turn lines → summary → territory snapshot.
- Neúplné soubory (přerušená hra) se **automaticky mažou**.

### Turn line obsahuje
- Unit name, team, action, energy, position.
- `attackHex`: cíl útoku (sousední hex, ne pozice útočníka).
- `wallHP`: zbývající HP zdi po útoku.
- `built`: cíl stavby/zničení struktury.
- `captured`: hex který byl zabrán.
- `energies`: snapshot energie **všech** jednotek v daném tahu.

### Replay přehrávání
- Klávesové ovládání: Space (play/pause), šipky (step/round).
- Panel dole s transport controls.
- DETAIL overlay: souřadnice hexů.
- Správné zobrazení: "Attack wall → (1,0) HP:2", "BuildWall → (1,0)".

## Silent Training mode

- Checkbox "Train Silent" v Project Tools.
- **Vypnuto:** kamera, unit modely, health bary, action indikátory, attack efekty, labels, hex MeshRenderers.
- **Zapnuto:** jen match history tabulka + session stats.
- **TimeScale = 20**, VSync off, uncapped framerate.
- Flag: `GameConfig.SilentTraining` (set via RuntimeInitializeOnLoadMethod BeforeSceneLoad).

## Editor tooling (Project Tools Window)

### Configuration
- Max Steps a ms/tick vždy viditelné. Ostatní v rozbalovacím "All Parameters".

### Training
- Train Silent checkbox, Start/Stop Training, Run ID, status.
- Advanced: manual start, resume, init from previous.

### Observe
- Launch Game, Reset Game, Speed info.

### Replay
- Browse, Latest (disabled bez souborů), Open Folder, Load Replay.

### Analysis
- Strategy Analyzer (CSV export), Highlight Detector.

### Testing
- Run All Tests (EditMode + PlayMode sequential), Open Test Runner.
- Auto-run after compile.
- Reset All History & Models (smaže vše: replays, results, ONNX, PlayerPrefs).
