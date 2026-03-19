# TODO — Robots & Mutants

## ✅ Hotovo (archiv)

### Rychlost
- [x] Sdílený `UnitCache.GetAll()` — deduplikace cache
- [x] `HexGrid.CountTiles(Team)` metoda — O(1) místo duplicitní iterace

### ML-Agents trénink
- [x] Kill bonus (+0.5 reward za zabití nepřítele)
- [x] Build reward (+0.05 za úspěšný TryBuild)
- [x] Přidat fortification do observací (+6 floats → 62 obs)
- [x] Přidat respawn cooldown do observací (+1 float → 63 obs)
- [x] Zvýšit síť 256×2 → 512×2, max_steps 10M, num_epoch 5
- [x] Opravit step progress clock
- [x] Přesunout reward shaping konstanty do GameConfig

### Architektura
- [x] Smazat mrtvý kód: TerritorySystem, CombatSystem
- [x] Rozdělit GameManager → 3 partial classes
- [x] Extrahovat TestModeDetector utility
- [x] Game replay logger (JSONL) + StrategyAnalyzer

---

## 🔴 Vysoká priorita — YouTube video

### Vizuály (nutné pro video)
- [ ] **Post-processing** — Bloom, Vignette, Color Grading v URP Volume
- [ ] **Territory border shader** — svítící linka mezi územími různých týmů
- [ ] **Kill cam** — auto zoom + slow-mo (0.1×) při zabití
- [ ] **Capture animace** — wave/fade efekt při změně vlastníka hexu
- [ ] **Death fade-out** — plynulé zmizení místo instant disappear
- [ ] **Unit trails** — barevná stopa za pohybujícími se jednotkami
- [ ] **Stíny pod jednotkami** — jednoduché blob shadows

### Kamera (nutné pro video)
- [ ] **Cinemachine** — dynamická kamera (follow, dolly, zoom, shake)
- [ ] **Slow-motion systém** — auto-detect klíčových momentů
- [ ] **Camera presets** — izometrický (přehled), close-up (boj), dramatic (nízký úhel)
- [ ] **Smooth transitions** — plynulé přechody mezi pohledy

### Audio (nutné pro video)
- [ ] **Zvukové efekty** — útok (clang/squelch), smrt, capture, build, respawn
- [ ] **Ambient hudba** — dynamická (klidná → napínavá podle stavu hry)
- [ ] **Win/loss jingle** — krátký fanfáre/smutný zvuk

### Replay systém (nutné pro video)
- [ ] **Replay přehrávač** — načíst JSONL, přehrát hru krok po kroku
- [ ] **Ovládání** — play/pause, speed control, step forward/back
- [ ] **Overlay režimy** — heatmapa pohybu, strategy labels, territory history
- [ ] **Unity Recorder integrace** — export gameplay do MP4/PNG sequence

---

## 🟡 Střední priorita — Herní mechaniky

### Game Design — hlubší strategie
- [ ] **Cooldown systém** — build: 3 tahy CD, shield: 5 tahů active / 10 CD
- [ ] **Terén** — hory (neprostupné), řeky (zpomalení), resource hexy (bonus)
- [ ] **Eskalace** — shrinking map po X kolech (battle royale styl)
- [ ] **Power-up hexy** — náhodně se objevují (+damage, +HP, +speed)
- [ ] **Mega-events** — meteor shower (reset hexů), boss spawn (NPC)

### Game Design — jednotky
- [ ] **Unit variace** — Scout (rychlý/slabý), Tank (pomalý/silný), Builder (neútočí)
- [ ] **Dynamický damage** — škálování podle HP, pozice, buffs
- [ ] Snížit win threshold 60% → 50% nebo přidat timer pressure
- [ ] Zvýšit respawn cooldown 12 → 20-30 kroků
- [ ] Slime spread: zvýšit 12% → 25% nebo deterministic
- [ ] Robot shield: přidat cooldown nebo HP threshold

### ML-Agents — pokročilý trénink
- [ ] **Curriculum learning** — 5×5 → 7×7 → 10×10 postupné zvětšování
- [ ] **Self-play** — Robot vs Robot, Mutant vs Mutant (evoluce silnějších agentů)
- [ ] **Diversity bonus** — reward za rozmanité strategie (anti-cheese)
- [ ] **Rozšířené observace** — threat levels, distance to nearest enemy, look-ahead
- [ ] **Time horizon** — zvýšit 128 → 256 (delší strategické plánování)

---

## 🟢 Nízká priorita — Polish

### Vizuály — extra
- [ ] Dynamic point lights na útoky/efekty
- [ ] Particle effect variety (víc typů explozí, spawn efektů)
- [ ] Minimap v rohu s territory overview
- [ ] Victory celebration animace (vítězný tým "tančí")
- [ ] Intro cinematic — kamera prolétne nad mapou

### YouTube — extra funkce
- [ ] **Commentator triggers** — events pro auto-voiceover ("TRIPLE KILL!")
- [ ] **Dramatic camera cuts** — auto přepínání kamer při events
- [ ] **Training progress overlay** — graf v rohu (epocha/generace)
- [ ] **Side-by-side mode** — Gen 0 vs Gen 1000 split screen
- [ ] Elo/skill rating zobrazený v HUD
- [ ] Speed control tlačítka v UI

### Architektura
- [ ] Extrahovat sdílenou MaterialManager utility
- [ ] Dependency injection místo FindFirstObjectByType
- [ ] Explicitní state machine pro GameManager (enum GamePhase)

### Tech Stack
- [ ] Weights & Biases — experiment tracking
