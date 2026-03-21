# Hex Territory Control v2 — Business Logic & Functionality

## Overview

AI simulace boje dvou asymetrických týmů (Roboti vs. Mutanti) na hexagonálním hřišti. Týmy se učí strategii přes reinforcement learning (MA-POCA). Cílem každého týmu je obsadit 60% (konfigurovatelné) území.

---

## Herní pole

- Hrací pole má tvar **velkého hexagonu** složeného z malých hexagonálních dílků.
- Velikost pole: parametr **Board Side** (default: 5 = 61 hexů).
- Vzorec: `3 * side^2 - 3 * side + 1` celkových hexů.
- Souřadnicový systém: **axiální souřadnice (q, r)** s odvozeným `s = -q - r`.
- Hex layout: **flat-top**.

## Základny

- Každý tým má **základnu** v protilehlém rohu.
  - Roboti: levý dolní roh `(-max, max)`.
  - Mutanti: pravý horní roh `(max, -max)`.
- Velikost základny = `min(unitsPerTeam, 4)` hexů.
- Základnové hexy: permanentně vlastněny, nelze obsadit, spawn pointy.
- **Base regeneration:** Jednotka na vlastním base hexu regeneruje **+3 energie/krok**.

## Energie (Universal Resource)

Energie je univerzální zdroj — slouží jako HP i currency pro akce.

| Parametr | Hodnota |
|----------|---------|
| Max energie | 15 |
| Respawn cooldown | 6 kroků |
| Smrt | při 0 energie |
| Respawn | plná energie |

## Týmy a jednotky

### Obecné
- Dva týmy: **Robot** (modrý) a **Mutant** (zelený).
- Počet jednotek na tým: konfigurovatelný (default: 3).
- Vizualizace: Roboti = modré kapsle, Mutanti = zelené kostky.

### Smrt a respawn
- Jednotka s ≤ 0 energie zemře.
- Po smrti: neviditelná, cooldown 6 kroků.
- Respawn na volném base hexu s plnou energií (15).

## Akční prostor (25 akcí)

Jednoduchý diskrétní branch s 25 akcemi:

| Akce | Rozsah | Popis |
|------|--------|-------|
| Idle | 0 | Zůstat na místě |
| Move | 1-6 | Pohyb ve směru 0-5 |
| Attack | 7-12 | Útok ve směru 0-5 |
| Build | 13-18 | Stavba ve směru 0-5 |
| DestroyWall | 19-24 | Zničení vlastní zdi ve směru 0-5 |

## Pohyb

- Diskrétní: z hexu na sousední hex (6 směrů).
- **Blokováno:** nepřátelské území, zdi (všechny), obsazené hexy, mimo mapu.
- **Neutrální hex:** volný vstup + automatický capture (zdarma).
- **Vlastní hex:** volný vstup.
- **Robot vstup na nepřátelský sliz:** stojí 3 energie, sliz se zničí.
- **Mutant na vlastním slimu:** volný vstup (regen řeší AbilitySystem).

## Boj (Combat)

Útok ve směru na sousední hex. Prioritní systém:

### Priorita útoku
1. **Nepřátelská jednotka** → útok na jednotku
2. **Zeď** → útok na zeď
3. **Nepřátelský hex** → převzetí hexu
4. **Neutrální hex** → obsazení hexu

### Útok na jednotku
| Parametr | Hodnota |
|----------|---------|
| Cena | 3 energie |
| Base damage | 3 energie |
| Counter-damage | žádný |

### Proximity bonusy
- **Shield Wall (Robot obránce):** -1 damage za každého sousedního Robot spojence (max -3).
- **Swarm (Mutant útočník):** +1 damage za každého sousedního Mutant spojence (max +3).
- Výsledný damage: `max(0, baseDamage + swarmBonus - shieldWallReduction)`.

### Útok na zeď
| Parametr | Hodnota |
|----------|---------|
| Cena | 2 energie |
| Damage | 1 HP zdi |
| Zeď zničena | při 0 HP |

### Útok na nepřátelský hex
| Parametr | Hodnota |
|----------|---------|
| Cena | 2 energie |
| Efekt | Hex přejde do vlastnictví útočníka |

### Útok na neutrální hex
| Parametr | Hodnota |
|----------|---------|
| Cena | 1 energie |
| Efekt | Hex přejde do vlastnictví útočníka |

## Struktury

### Zdi (Robot)
- Stavba na **sousední vlastní prázdný hex** (ne na hex pod jednotkou).
- Cena: **4 energie**.
- Zeď HP: **3** (max).
- Zdi **blokují veškerý pohyb** (vlastní i nepřátelský).
- Zničení vlastní zdi: **1 energie**.
- Vizuálně: vyvýšený hex (0.12), tmavší šedo-modrá, jasnější dle HP.

### Sliz (Mutant)
- Umístění na **vlastní hex pod jednotkou** (ne na sousední hex).
- Cena: **2 energie**.
- Mutant na vlastním slimu: **+1 energie/krok** regenerace.
- Robot vstup na nepřátelský sliz: **3 energie**, sliz zničen.
- Vizuálně: jasně zelená.

## Herní smyčka (Game Loop)

Sekvenční tahový model. Každý krok:

1. **Rozhodnutí agenta** — ML-Agents vybere akci (0-24).
2. **Provedení akce** — Move/Attack/Build/DestroyWall/Idle.
3. **Schopnosti** — base regen, slime regen.
4. **Respawn** — tick cooldownu, respawn na base.
5. **Win condition** — kontrola podmínek výhry.

## Podmínky výhry

- **Territory threshold:** 60% contestable hexů → okamžitá výhra.
- **Timeout:** po 800 krocích → tým s více hexy vyhrává.
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

### Action masking
- Move: maskováno pokud cíl je blokovaný (enemy territory, wall, occupied, off-map).
- Attack: maskováno pokud na cíli není nic k útoku nebo nedostatek energie.
- Build (Robot): maskováno pokud sousední hex není vlastní prázdný nebo nedostatek energie.
- Build (Mutant): maskováno pokud hex pod jednotkou není vlastní prázdný nebo nedostatek energie (směr ignorován).
- DestroyWall: maskováno pokud sousední hex nemá vlastní zeď.
- Dead: jen Idle povoleno.

### Reward shaping
- **Territory growth:** +0.1 za nový hex v largest connected group.
- **Enemy loss:** +0.1 za ztracený hex nepřítele.
- **Cohesion:** +0.02 × (largest_group / total_tiles).
- **Group split:** +0.3 za fragmentaci nepřítele.
- **Base connection:** +0.005 za propojení largest group s base.
- **Frontline:** +0.005 za přítomnost na hranici.
- **Kill bonus:** +0.5 za zabití nepřátelské jednotky.
- **Hex capture:** +0.05 za obsazení hexu útokem.
- **Build reward:** +0.05 za stavbu + adjacency bonus.
- **Step penalty:** -0.001 za krok.
- **Win/loss:** +1.0 / -1.0 group reward.

### Trénink
- Trainer: **MA-POCA**.
- Dva brains: `HexRobot`, `HexMutant`.
- Max steps: 800 per episode.
- Konfigurace: `Assets/ML-Agents/config/hex_territory.yaml`.

## Vizuální systém

### Barvy hexů
| Stav | Barva |
|------|-------|
| Neutrální | `(0.55, 0.55, 0.50)` |
| Robot territory | `(0.30, 0.50, 0.85)` |
| Mutant territory | `(0.45, 0.75, 0.30)` |
| Robot base | `(0.15, 0.30, 0.70)` |
| Mutant base | `(0.25, 0.55, 0.15)` |
| Wall (Robot) | `(0.25, 0.30, 0.50)` + HP brightness |
| Slime (Mutant) | `(0.35, 0.85, 0.20)` |

- Zdi: vyvýšené (0.12), jasnější s vyšším HP (+0.05/HP).
- Barvy se aktualizují reaktivně přes `OnTileChanged`.

### Akční indikátory
| Akce | Symbol | Barva |
|------|--------|-------|
| Idle | `-` | Šedá |
| Move | `>>` | Bílá |
| Attack | `X` | Červená |
| Build Wall | `#` | Oranžová |
| Place Slime | `~` | Zelená |
| Capture | `+` | Cyan |

### Energy bar
- Kontinuální bar (single quad) pod jednotkou.
- Zelená >60%, žlutá >30%, červená pod 30%.

## Konfigurovatelné parametry (GameConfig)

| Parametr | Default | Popis |
|----------|---------|-------|
| boardSide | 5 | Velikost hřiště |
| unitsPerTeam | 3 | Počet jednotek |
| maxSteps | 800 | Max kroků za epizodu |
| unitMaxEnergy | 15 | Max energie |
| respawnCooldown | 6 | Kroky do respawnu |
| attackUnitCost | 3 | Cena útoku na jednotku |
| attackUnitDamage | 3 | Damage útoku na jednotku |
| attackWallCost | 2 | Cena útoku na zeď |
| attackWallDamage | 1 | Damage útoku na zeď |
| attackEnemyHexCost | 2 | Cena útoku na nepřátelský hex |
| attackNeutralCost | 1 | Cena útoku na neutrální hex |
| wallBuildCost | 4 | Cena stavby zdi |
| wallMaxHP | 3 | Max HP zdi |
| slimePlaceCost | 2 | Cena umístění slizu |
| destroyOwnWallCost | 1 | Cena zničení vlastní zdi |
| slimeEntryCostRobot | 3 | Cena vstupu Robota na nepřátelský sliz |
| baseRegenPerStep | 3 | Regenerace na base hexu |
| slimeRegenPerStep | 1 | Regenerace Mutanta na slimu |
| shieldWallMaxReduction | 3 | Max Shield Wall redukce |
| swarmMaxBonus | 3 | Max Swarm bonus |

## Editor tooling (Project Tools Window)

### Configuration
- Energy, structures, combat costs, proximity bonuses — vše editovatelné z GUI.

### Training
- Start/Stop Training, Resume, TensorBoard integration.

### Observe
- Launch Game, Reset Game, Speed control.

### Analysis
- Strategy Analyzer (CSV export), Highlight Detector (comeback, territory swing, coordinated attack, flanking, wipe event, blitz win, close game, stalemate break, kill streak).

### Replay Viewer
- Select/play replay files, transport controls (play/pause, step, speed, round scrubber).
- Backward compatible with old v1 replay files (BuildCrate→BuildWall, SpreadSlime→PlaceSlime, hp→energy).

### Testing
- EditMode + PlayMode tests, auto-run after compile.
