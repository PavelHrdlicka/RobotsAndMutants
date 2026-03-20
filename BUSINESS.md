# Hex Territory Control — Business Logic & Functionality

## Overview

AI simulace boje dvou asymetrických týmů (Roboti vs. Mutanti) na hexagonálním hřišti. Týmy se učí strategii přes reinforcement learning (MA-POCA). Cílem každého týmu je obsadit 60% (konfigurovatelné) území.

---

## Herní pole

- Hrací pole má tvar **velkého hexagonu** složeného z malých hexagonálních dílků.
- Velikost pole je definována parametrem **Board Side** (strana velkého hexu v počtu malých hexů).
  - Default: 5 (= 61 hexů).
  - Vzorec: `3 * side^2 - 3 * side + 1` celkových hexů.
- Souřadnicový systém: **axiální souřadnice (q, r)** s odvozeným `s = -q - r`.
- Střed pole je na souřadnici `(0, 0)`.
- Hex layout: **flat-top** (vrchol hexagonu míří doprava).

## Základny

- Každý tým má **základnu** v protilehlém rohu hexagonálního pole.
  - Roboti: levý dolní roh `(-max, max)`.
  - Mutanti: pravý horní roh `(max, -max)`.
- Velikost základny = **počet botů v týmu** (BFS expanze od rohového hexu).
- Základnové hexy:
  - Jsou permanentně vlastněny svým týmem.
  - Nelze je obsadit nepřítelem.
  - Slouží jako spawn pointy pro jednotky.
  - Po resetu se vlastnictví základen obnoví.

## Týmy a jednotky

### Obecné
- Dva týmy: **Robot** (modrý) a **Mutant** (zelený).
- Počet jednotek na tým je konfigurovatelný (default: 3).
- Oba týmy mají **stejný počet** jednotek.
- Jednotky jsou vizualizovány jako:
  - Roboti = modré kapsle.
  - Mutanti = zelené kostky.

### Statistiky jednotky
- **HP (zdraví):** 7 (maximum, konfigurovatelné 3-20).
- **Pozice:** jeden hex na mapě (axiální souřadnice).
- **Stav:** živý / mrtvý.
- **Respawn cooldown:** 12 kroků po smrti.
- **Buff — Štít (shield):** Robot na vlastním území nemá poškození v boji.
- **Buff — Rychlost (speedMultiplier):** Mutant na slizu se pohybuje 2× rychleji.

### Smrt a respawn
- Jednotka s 0 HP zemře.
- Po smrti: neviditelná na mapě, cooldown 30 kroků.
- Po vypršení cooldownu: respawn na volném hexu vlastní základny s plným HP.

## Pohyb

- Diskrétní pohyb: z jednoho hexu na sousední hex (6 směrů).
- Jednotka může zůstat stát (akce "stay").
- Nelze vstoupit na hex obsazený spojeneckou jednotkou.
- Nelze se pohnout mimo hranice pole.
- Validace pohybu: kontrola sousednosti (distance = 1), platnost souřadnice, obsazenost spojencem.

## Zabírání území (Territory Capture)

- Když jednotka stojí na **neutrálním** hexu → hex se stane vlastnictvím jejího týmu.
- Když jednotka stojí na **nepřátelském** hexu:
  - Pokud hex nemá fortifikaci → okamžité převzetí.
  - Pokud hex má fortifikaci (1-3) → potřebuje odpovídající počet kroků k převzetí (capture progress).
- Převzatý hex ztrácí předchozí typ (Crate/Slime) a fortifikaci.
- Základnové hexy nelze převzít.

## Týmové schopnosti

### Roboti — Stavba krabic (Crate)
- Když Robot stojí na vlastním prázdném hexu, s **30% pravděpodobností** postaví krabici (`TileType.Crate`).
- Krabice poskytuje defenzivní bonus (vizuálně tmavší modrá).

### Mutanti — Slime (Slime)
- Když Mutant stojí na vlastním prázdném hexu, automaticky ho pokryje slizem (`TileType.Slime`).
- Sliz **se nešíří** na sousední hexy — territory kontrola je čistě o pozici jednotek.

### Štít Robotů
- Robot stojící na hexu vlastněném Roboty získává **štít**.
- Štít = 0 poškození v boji (úplná imunita proti damage).

### Rychlost Mutantů
- Mutant stojící na slizu má **2× speed multiplier**.
- (Připraveno pro rozšíření: pohyb o 2 hexy za krok.)

### Adjacency bonus (fortifikace)
- Jednotka se spojeneckými jednotkami na sousedních hexech zvyšuje **fortifikaci** svého hexu.
- Fortifikace = počet spojeneckých sousedů (max 3).
- Fortifikovaný hex vyžaduje více kroků k převzetí nepřítelem.

## Boj (Combat)

- Boj nastává, když jednotka zaútočí na nepřátelskou jednotku na **sousedním hexu**.
- Útočník zůstává na místě, oboustranný damage:
  - Útočník dává **2 damage**, obránce vrací **1 damage**.
  - Výjimka: jednotka se **štítem** (Robot na vlastním území) neobdrží damage.
- Jednotka s 0 HP po boji zemře (→ respawn cooldown 12 kroků).

### Robot Flanking (dvojitý damage)
- Když Robot útočí a vedle něj stojí spojenecké jednotky, má šanci na **dvojitý damage** (2 → 4).
- Šance = **10% × počet sousedních spojenců** (max 3 spojenci = max 30%).
- Konfigurovatelné přes `robotFlankingChancePerAlly` (0-50%).

### Mutant Swarm Cover (úhyb)
- Když na Mutanta útočí nepřítel a vedle Mutanta stojí spojenecké jednotky, má šanci **uhnout** (0 damage).
- Šance = **10% × počet sousedních spojenců** (max 3 spojenci = max 30%).
- Konfigurovatelné přes `mutantDodgeChancePerAlly` (0-50%).

## Herní smyčka (Game Loop)

Každý krok (step) probíhá v tomto pořadí:

1. **Rozhodnutí agentů** — ML-Agents / Heuristic vybere akci (stay / move direction).
2. **Pohyb** — jednotky se přesunou na cílový hex.
3. **Boj** — řeší se kolize nepřátelských jednotek na stejném hexu.
4. **Zabírání území** — jednotky claimují hexy pod sebou.
5. **Schopnosti** — přepočet štítů, rychlosti, adjacency fortifikace.
6. **Respawn** — tick cooldownu mrtvých jednotek, respawn připravených.
7. **Win condition check** — kontrola podmínek výhry.

## Podmínky výhry

- **Territory threshold:** Tým, který obsadí **X%** contestable hexů (default: 60%), vyhrává okamžitě.
- **Timeout:** Po dosažení max kroků (default: 2000) vyhrává tým s více obsazenými hexy.
- **Remíza:** Při timeoutu se stejným počtem hexů → draw.
- Po výhře se všechny jednotky zastaví (DecisionRequester + HexAgent disabled).

## ML-Agents integrace

### Observations (56 floatů na agenta)
- **Vlastní stav (5):** normalizovaná pozice (q, r), HP, alive flag, team (+1/-1).
- **6 sousedních hexů × 8 hodnot (48):**
  - Ownership one-hot (3): neutrální / vlastní / nepřátelský.
  - TileType one-hot (3): empty / crate / slime.
  - Přítomnost nepřátelské jednotky (1).
  - Přítomnost spojenecké jednotky (1).
- **Globální stav (3):** vlastní tiles %, nepřátelské tiles %, step progress.

### Actions (1 diskrétní branch, 7 hodnot)
- 0 = stay (zůstat na místě).
- 1-6 = pohyb ve směru 0-5 (E, NE, NW, W, SW, SE).

### Action masking
- Neplatné směry (mimo mapu, obsazené spojencem) jsou maskovány.
- Mrtvá jednotka má maskované všechny směry kromě "stay".

### Reward shaping
- **+0.1** za každý nově získaný hex vlastního týmu.
- **+0.1** za každý ztracený hex nepřítele.
- **-0.001** penalizace za krok (motivace k akci).
- **+1.0 / -1.0** group reward za výhru / prohru (MA-POCA).
- **+0.5 / -0.5** group reward při timeoutu (vítěz / poražený).

### Trénink
- Trainer: **MA-POCA** (Multi-Agent POsthumous Credit Assignment).
- Dva samostatné brains: `HexRobot` a `HexMutant` (asymetrické týmy).
- Team ID: 0 = Roboti, 1 = Mutanti.
- Python: `mlagents 1.1.0` + `torch 2.2.2` v conda env `mlagents2` (Python 3.10.12).
- Konfigurace: `Assets/ML-Agents/config/hex_territory.yaml`.

## Vizuální systém

### Akční indikátory jednotek
Nad každou jednotkou se zobrazuje plovoucí indikátor aktuální akce:

| Akce | Symbol | Barva | Popis |
|------|--------|-------|-------|
| Idle | `-` | Šedá | Jednotka zůstala stát |
| Move | `>>` | Bílá | Pohyb na sousední hex |
| Attack | `X` | Červená | Boj s nepřítelem |
| Build Crate | `#` | Oranžová | Robot postavil krabici |
| Spread Slime | `~` | Zelená | Mutant šíří sliz |
| Capture | `+` | Cyan | Zabírání území |

- Pod symbolem je textový label akce.
- Pod indikátorem je HP bar (zelený/žlutý/červený).
- Priorita zobrazení: Attack > Capture/Build/Slime > Move > Idle.

### Barvy hexů
| Stav | Barva |
|------|-------|
| Neutrální | Šedá `(0.55, 0.55, 0.50)` |
| Robot territory | Modrá `(0.30, 0.50, 0.85)` |
| Mutant territory | Zelená `(0.45, 0.75, 0.30)` |
| Robot base | Tmavě modrá `(0.15, 0.30, 0.70)` |
| Mutant base | Tmavě zelená `(0.25, 0.55, 0.15)` |
| Crate (Robot) | Tmavší modrá `(0.20, 0.35, 0.65)` |
| Slime (Mutant) | Jasně zelená `(0.35, 0.85, 0.20)` |

- Fortifikace zvyšuje jas hexu (+0.08 na úroveň).
- Barva se aktualizuje reaktivně přes event `OnTileChanged`.

### HUD
- **Levý panel (modrý):** ROBOTS — alive count, tiles count, procento, progress bar.
- **Pravý panel (zelený):** MUTANTS — totéž.
- **Horní střed:** Step counter (aktuální / max).
- **Game over banner:** Zlatý text s výsledkem uprostřed obrazovky.

### Match History (dole na obrazovce)
- Tabulka posledních **20 simulací** s výsledky.
- Sloupce: #, Winner, Steps, Score (R:X vs M:Y).
- Řádky barevně odlišené: modrý = Robot win, zelený = Mutant win, žlutý = remíza.
- Barevná tečka u každého řádku indikující vítěze.
- Nejnovější výsledek nahoře.
- Historie přetrvává přes resety v rámci jedné Play session.

### Auto-restart
- Po konci hry se automaticky po 2 sekundách spustí nová epizoda.
- Agenti se re-aktivují, skupiny se přeregistrují.
- Umožňuje nepřetržitý běh simulací a plnění match history.

### Kamera
- Ortografický top-down pohled.
- Automaticky vycentrovaná nad hřiště.
- Tmavé pozadí `(0.15, 0.15, 0.20)`.

## Konfigurovatelné parametry (GameConfig)

Vše konfigurovatelné z Unity editoru přes **Project Tools → Game Config**:

| Parametr | Range | Default | Popis |
|----------|-------|---------|-------|
| Board Side | 3-15 | 5 | Strana velkého hexu v malých hexech |
| Units per Team | 1-10 | 3 | Počet jednotek na tým (= velikost základny) |
| ms / tick | 1-5000 | 200 | Rychlost simulace (ms na tick) |
| Win % | 10-100 | 60 | Procento území k výhře |
| Max Steps | 100-10000 | 2000 | Maximální počet kroků za epizodu |
| Unit Max Health | 3-20 | 7 | Maximální HP jednotky |
| Robot Flank %/ally | 0-50% | 10% | Šance na dvojitý damage za sousedního spojence |
| Mutant Dodge %/ally | 0-50% | 10% | Šance na úhyb za sousedního spojence |
| Replay: log every Nth game | 1-1000 | 1 | Logování replay souboru každou N-tou hru |

- Uloženo jako ScriptableObject v `Assets/Resources/GameConfig.asset`.
- Sim Speed se mění i za běhu.
- Ostatní parametry se aplikují při dalším Reset + Setup + Play.

## Editor tooling (Project Tools Window)

Všechny akce ovládané z Unity editoru přes dokovatelné okno **Project Tools**. Layout sleduje ML workflow: Configure → Train → Observe → Analyze → Test.

### 1. Configuration
- Board Side, Units per Team, ms/tick, Win %, Max Steps.
- Combat: Unit Max Health, Robot Flank %/ally, Mutant Dodge %/ally.
- Replay: log every Nth game.

### 2. Training
- **Start Training** (hero button) — reset + setup + start Python + auto-play po detekci portu 5004. Odolné vůči domain reloadu.
- Run ID + Status indikátor (Idle / Training PID / Finished OK / Error).
- **Stop Training** — ukončí Python proces, načte ONNX modely.
- **Advanced foldout:** Start Training (manual), Resume, Init From Previous.
- **TensorBoard:** Open / Stop.

### 3. Observe (no training)
- **Launch Game** — reset + setup + play bez Pythonu.
- Speed info (read-only).
- **Reset Game** — restart epizody.

### 4. Analysis
- **Analyze Last N / All** — replay analýza přes StrategyAnalyzer (metriky, CSV export).
- **Detect Highlights (Last N) / Detect All** — algoritmická detekce zajímavých momentů (comeback, territory swing, coordinated attack, flanking, wipe event, blitz win, close game, stalemate break, kill streak). Výstup: `Replays/highlights.json` pro AI interpretaci přes Claude Code.
- **Open Replays Folder** — otevře adresář s replay soubory.

### Replay Viewer
- **Replay sekce** v ProjectToolsWindow: výběr souboru, "Select Latest Replay", "Play Replay".
- **Play Replay** nastaví `ReplayPlayer.PendingReplayPath`, provede Reset + Setup + Play.
- **ReplayPlayer** (runtime): parsuje JSONL replay soubor, vypne GameManager loop + ML-Agents, přehrává tahy krok po kroku.
  - Playback controls: Play/Pause, Step Turn, Step Round, JumpToRound.
  - Reaktivní vizualizace: nastavuje UnitData/HexTileData stav → HexVisuals, health bary, akční indikátory reagují automaticky.
- **ReplayPlayerHUD** (OnGUI): transportní ovládání — play/pause, step, speed slider (1-100 turns/s), round scrubber.
- **ReplayData** (parser): zpracovává header, turn a summary řádky z JSONL formátu.

### 5. Testing
- **Run EditMode Tests** — spustí testy okamžitě.
- **Open Test Runner** — otevře Unity Test Runner.
- **Auto-run after compile** — automatické spuštění testů po každé rekompilaci.

### 6. Scene Setup (collapsed foldout)
- **Setup Scene / Reset Scene** — manuální správa scény.
- **Randomize Tile Ownership** — náhodné obarvení hexů pro vizuální QA.
