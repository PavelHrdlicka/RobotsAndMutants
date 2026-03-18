# TODO — Robots & Mutants

## Vysoká priorita

### Rychlost
- [ ] Sdílený `UnitCache.GetAll()` — HexAgent i HexMovement mají duplicitní static cache, 2× FindObjectsByType za frame místo 1×
- [ ] `HexGrid.CountTiles(Team)` metoda — duplicitní iterace celého gridu v GameManager + HexAgent

### ML-Agents trénink
- [ ] Kill bonus (+0.5 reward za zabití nepřítele)
- [ ] Build reward (+0.05 za úspěšný TryBuild)
- [ ] Přidat fortification do observací (1 float/neighbor, celkem +6 floats → 62 obs)
- [ ] Přidat respawn cooldown do observací (1 global float → 63 obs)
- [ ] Zvýšit síť 256×2 → 512×2 v hex_territory.yaml
- [ ] Zvýšit max_steps 5M → 10-15M v hex_territory.yaml
- [ ] Zvýšit num_epoch 3 → 5 v hex_territory.yaml
- [ ] Opravit step progress clock (Academy.StepCount / 6000f místo 2000f)

### Architektura
- [ ] Smazat mrtvý kód: TerritorySystem (nikdy nevolán z GameManageru)
- [ ] Smazat mrtvý kód: CombatSystem (nikdy nevolán, combat řeší HexMovement)
- [ ] Rozdělit GameManager (988 řádek) → GameLoop + GameHUD + EpisodeManager + GameStats
- [ ] Extrahovat `TestModeDetector` utility (duplicitní kód ve 3 souborech)

## Střední priorita

### Grafika
- [ ] Zapnout Bloom + Vignette v URP Volume (post-processing)
- [ ] Camera shake na útok + slow-mo na kill
- [ ] Capture animace na tiles (wave/fade efekt místo okamžité změny barvy)
- [ ] Stíny pod jednotkami
- [ ] Fade-out animace při smrti (místo instant zmizení)
- [ ] Dynamic point lights na útoky/efekty

### Zvuk
- [ ] Ambient hudba (loop)
- [ ] Zvukové efekty: útok, smrt, capture, build, respawn, win/loss jingle

### Game Design
- [ ] Snížit win threshold 60% → 50% nebo přidat timer pressure
- [ ] Zvýšit respawn cooldown 12 → 20-30 kroků
- [ ] Slime spread: zvýšit 12% → 25% nebo deterministic
- [ ] Robot shield: přidat cooldown nebo HP threshold
- [ ] Přesunout reward shaping konstanty z HexAgent do GameConfig

### YouTube / Divák
- [ ] Replay/highlight systém — zpomalení na klíčové momenty
- [ ] Elo/skill rating zobrazený v HUD
- [ ] Speed control tlačítka viditelná v UI (0.5×, 1×, 2×, 5×)
- [ ] Heatmapa — vizualizace pohybu jednotek (přepínací view)

## Nízká priorita

### Architektura
- [ ] Extrahovat sdílenou `MaterialManager` utility (duplicitní lazy-loading ve 4 souborech)
- [ ] Dependency injection místo FindFirstObjectByType
- [ ] Explicitní state machine pro GameManager (enum GamePhase)

### Tech Stack
- [ ] Unity Recorder — nahrávání gameplay videa
- [ ] Cinemachine — dynamická kamera (follow, shake, cuts)
- [ ] Curriculum learning: 5×5 → 7×7 → 10×10 postupné zvětšování mapy
- [ ] Self-play trénink (Robot vs Robot, Mutant vs Mutant)
- [ ] Weights & Biases — experiment tracking (lepší než TensorBoard)
