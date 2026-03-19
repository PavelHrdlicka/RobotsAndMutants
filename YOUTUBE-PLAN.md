# YOUTUBE-PLAN — Robots vs Mutants: AI Territory War

## Vize

Vytvořit virální YouTube video ve stylu **AI Warehouse** + **MrBeast** pacing.
Ukázat jak se dvě armády AI (Roboti vs Mutanti) učí od nuly ovládat hexagonální mapu.
Divák sleduje evoluci od chaosu k sofistikovaným strategiím.

---

## 1. Referenční styl

### AI Warehouse
- **Kanál:** ~815K subs, ~21 videí, průměr 2M views/video
- **Maskot:** "Albert" — oranžová kostka s googlí očima
- **Prostředí:** Čisté, minimalistické 3D (Unity + ML-Agents)
- **Formát:** Úkol → AI selhává → trénink → AI zvládá → překvapivé řešení
- **Tech:** Unity + ML-Agents + PPO, 50-200 paralelních kopií

### MrBeast pacing
- **Hook:** Prvních 5 sekund řekni o co jde + proč to musí vidět
- **Escalation:** Každých 30-60s nová informace, twist, nebo milestone
- **Pattern interrupt:** Nikdy víc než 60s bez vizuální/informační změny
- **Stakes:** Neustále připomínat co je v sázce ("kdo ovládne mapu?")
- **Payoff:** Finální bitva / překvapivý výsledek na konci

---

## 2. Struktura videa

### Optimální délka: 8-12 minut
(Dostatečné pro monetizaci, přitom udrží pozornost.)

### Scénář po sekcích

#### [0:00-0:15] HOOK (15s)
- Záběr na finální natrénovanou bitvu (nejlepší moment)
- Voiceover: "Tihle roboti a mutanti se naučili válčit o každý kousek mapy. Ale na začátku neuměli ani chodit."
- Smysl: Ukázat "before/after" okamžitě

#### [0:15-1:00] PŘEDSTAVENÍ (45s)
- Vysvětlení pravidel: hexagonální mapa, dva týmy, territory control
- Grafické overlaye: šipky, zvýraznění, text labels
- "Roboti staví krabice a mají štít. Mutanti šíří sliz a jsou rychlejší."
- Animovaný diagram asymetrických schopností

#### [1:00-2:30] GENERACE 0 — CHAOS (90s)
- AI dělá náhodné akce, chodí do zdí, ignoruje nepřítele
- Humor: zoomovat na hloupé chování, sound effects
- Metriky na obrazovce: "0 zabití, 0 obsazených hexů"
- "Po 1000 hrách se konečně naučili... chodit rovně."
- Timelapse tréninku s TensorBoard grafy

#### [2:30-4:00] PRVNÍ STRATEGIE (90s)
- AI začíná obsazovat territory, objevuje se clustering
- "Roboti zjistili, že když drží skupinu, mají štít. Mutanti zase..."
- Highlight: první úmyslné zabití, první stavba krabice
- Split-screen: Gen 0 vs Gen 1000
- Statistiky z replay analyzéru

#### [4:00-5:30] ARMS RACE (90s)
- Mutanti vyvinou counter-strategii (obklíčení, slime rush)
- Roboti reagují (turtle, fortifikace)
- "Tady se děje něco zajímavého..." — narrator pozastavení
- Slow-mo na klíčové momenty
- Heatmapa pohybu: kde se soustředí boje

#### [5:30-7:00] TWIST / PŘEKVAPENÍ (90s)
- Nečekaná strategie: sacrifice play, bait, pincer movement
- "Tohle mě překvapilo — podívejte co Mutanti udělali..."
- Detailní rozbor jedné hry krok po kroku
- Replay s komentářem a šipkami

#### [7:00-9:00] FINÁLNÍ BITVA (120s)
- Nejlepší natrénované modely proti sobě
- Pomalejší tempo, dramatická hudba
- Komentář každého klíčového tahu
- Territory bar ukazuje měnící se poměr
- Slow-mo na rozhodující momenty

#### [9:00-10:00] VÝSLEDEK + STATISTIKY (60s)
- Kdo vyhrál a proč
- Celkové statistiky: tisíce odehraných her, miliony kroků
- Graf vývoje strategií přes čas
- "Za X hodin tréninku se naučili to, co by lidskému hráči trvalo..."

#### [10:00-10:30] OUTRO + CTA (30s)
- "Co kdyby měly ještě víc schopností? Napište do komentářů..."
- Teaser na další video (větší mapa? Více týmů? Boss fight?)
- Subscribe + like

---

## 3. Vizuální styl pro video

### Inspirace
- **Polytopia** — čisté low-poly hex tiles
- **Dorfromantik** — soft colors, gentle geometry
- **AI Warehouse** — minimalistické prostředí, focus na AI chování

### Barvy
| Element | Barva | Hex |
|---------|-------|-----|
| Neutrální hex | Světle šedá | #8C8C85 |
| Robot territory | Ocelově modrá | #4D80D9 |
| Mutant territory | Organická zelená | #73BF4D |
| Robot base | Tmavě modrá | #264DB3 |
| Mutant base | Tmavě zelená | #408C26 |
| Pozadí | Tmavě modro-šedá | #1F1F2E |
| Crate | Oranžovo-hnědá | #B3751A |
| Slime | Neonově zelená | #59D933 |

### Jednotky — vizuální upgrade
- **Roboti:** Hranatý design (kvádry), modré tělo, žluté "oči" (LED), kovový lesk
- **Mutanti:** Organický design (zaoblené tvary), zelené tělo, červené oči, tentákly
- Oba týmy: výrazné siluety rozlišitelné i při oddálení
- Inspirace: Albert z AI Warehouse (jednoduchý tvar + oči = osobnost)

### Kamera pro video
- **Hlavní pohled:** Izometrický 45° (současný) — přehled celé mapy
- **Close-up:** Zoom na konkrétní boj (Cinemachine dolly)
- **Dramatic angle:** Nízký úhel při klíčových momentech
- **Transition:** Smooth zoom-in/zoom-out mezi pohledy
- **Slow-motion:** 0.1× speed při zabití, capture, a klíčových momentech

### Post-processing
- **Bloom:** Jemný glow na territory borders a útoky
- **Vignette:** Tmavší okraje pro focus na střed
- **Color grading:** Lehce desaturované neutrální, saturované territory
- **Depth of field:** Rozostření okrajů při close-up záběrech

---

## 4. Audio plán

### Hudba
- **Intro/Hook:** Dramatický orchestrální hit (5s)
- **Chaos fáze:** Komická/lehká hudba (ukulele, pizzicato)
- **Strategie fáze:** Budující napětí (synth ambient)
- **Arms race:** Epická, gradující (orchestral + electronic)
- **Finální bitva:** Plný orchestr, dramatické pauzy
- **Outro:** Relaxační, satisfied feeling

### Sound effects
- Útok: metalický clang (robot), squelch (mutant)
- Smrt: explosion poof + sad trombone (komická verze)
- Capture: satisfying "pop" + territory color wash
- Build: construction click (robot), organic grow (mutant)
- Respawn: teleport whoosh
- Win: victorious fanfáre
- Territory flip: domino-like chain sound

### Voiceover styl
- **Tón:** Nadšený ale inteligentní (jako Kurzgesagt meets MrBeast)
- **Tempo:** Rychlé při akci, pomalé při analýze
- **Jazyk:** Angličtina (pro globální reach) nebo čeština (pro lokální trh)
- **Fráze:** Personifikovat AI ("Roboti se rozhodli...", "Mutanti zjistili...")
- Mluvit o AI jako o živých bytostech = emocionální investice diváka

---

## 5. Změny v simulaci PRO VIDEO

### Priorita 1 — Musí být ve videu
- [ ] **Cinemachine kamera** — dynamické záběry (zoom, pan, follow unit)
- [ ] **Slow-motion systém** — auto-detect klíčových momentů (kill, territory flip > 3 hexů)
- [ ] **Replay přehrávač** — načíst JSONL, přehrát hru krok po kroku s ovládáním
- [ ] **Territory border shader** — jasná hranice mezi územími (svítící linka)
- [ ] **Post-processing** — Bloom, Vignette, Color Grading v URP Volume
- [ ] **Kill cam** — automatický zoom + slow-mo při zabití

### Priorita 2 — Výrazně zlepší video
- [ ] **Heatmapa pohybu** — overlay vizualizace kde se jednotky pohybují
- [ ] **Strategy overlay** — zobrazit detekované strategie (z StrategyAnalyzer)
- [ ] **Training progress visualization** — graf v rohu ukazující epochu/generaci
- [ ] **Side-by-side comparison** — Gen 0 vs Gen 1000 ve split screen
- [ ] **Particle upgrade** — výraznější efekty pro útoky, smrti, capture
- [ ] **Unit trails** — barevná stopa za pohybujícími se jednotkami

### Priorita 3 — Nice to have
- [ ] **Minimap** — malá mapa v rohu s territory overview
- [ ] **Commentator triggers** — events pro automatický voiceover (např. "TRIPLE KILL!")
- [ ] **Dramatic camera cuts** — automatické přepínání kamer při zajímavých událostech
- [ ] **Victory celebration** — vítězný tým "tančí" na konci
- [ ] **Intro cinematic** — kamera prolétne nad mapou na začátku

---

## 6. Herní pravidla — vylepšení pro zajímavější video

### Současné problémy
1. **Boj je statický** — vždy 2 dmg útočník, 1 dmg obránce
2. **Schopnosti nemají cooldown** — vždy dostupné = méně taktiky
3. **Mapa je prázdná** — žádné překážky, chokepoints
4. **Všechny jednotky jsou stejné** — žádná specializace

### Navrhované změny

#### A) Terén a překážky
- **Hory** (neprostupné hexy uprostřed mapy) → vytvoří chokepoints
- **Řeky** (hexy s penalizací pohybu) → zpomalení
- **Zdroje** (speciální hexy s bonusem) → motivace k boji o konkrétní body
- Důvod: Divácky atraktivní "bitvy o průsmyk"

#### B) Jednotkové typy (variace)
- **Scout** — rychlý, slabý (1 HP, 2 pohyby za tah)
- **Tank** — pomalý, silný (8 HP, 1 pohyb za 2 tahy)
- **Builder** — nemůže útočit, staví 2× rychleji
- Důvod: Divák vidí "armádní kompozici" a taktiku

#### C) Cooldown systém
- Build: 3 tahy cooldown po stavbě
- Shield: aktivní jen 5 tahů, pak 10 tahů cooldown
- Slime spread: radius závisí na počtu sousedních slime hexů
- Důvod: Časování schopností = taktická hloubka

#### D) Eskalace během hry
- **Shrinking map** — po X kolech se okraje mapy stávají neprůchozí (battle royale styl)
- **Power-up hexy** — náhodně se objevují speciální hexy (+damage, +HP, +speed)
- Důvod: Přirozený build-up napětí, vynucení konfrontace

#### E) Mega-events
- Každých 100 kol: "Meteor shower" — náhodné hexy se resetují na neutrální
- Každých 200 kol: "Boss spawn" — NPC nepřítel na středu mapy, oba týmy musí spolupracovat
- Důvod: Pattern interrupt pro diváka, nepředvídatelnost

---

## 7. Metriky pro sledování pokroku tréninku

Pro video potřebujeme vizualizovat jak se AI zlepšuje:

- **Win rate** za posledních 100 her (graf)
- **Průměrná délka hry** (kratší = efektivnější strategie)
- **Kill/death ratio** per team
- **Territory control speed** (jak rychle dosáhnou 30%, 50%, 60%)
- **Action distribution** (% move/attack/build/idle) — ukazuje strategický posun
- **Cluster index** (seskupení jednotek) — ukazuje kooperaci
- **Strategy labels** z StrategyAnalyzer

---

## 8. Grafické zdroje a assety

### Doporučené free zdroje
| Zdroj | Co nabízí | Licence |
|-------|-----------|---------|
| **kenney.nl/assets** | Hex pack, character pack, UI | CC0 (free) |
| **quaternius.com** | Low-poly roboti, creatures | CC0 (free) |
| **kaylousberg.itch.io** | Low-poly medieval/dungeon packs | CC0 (free) |
| **Catlike Coding hex tutorial** | Hex mesh, shaders, blending | Educational |
| **Synty Studios** | POLYGON Sci-Fi, Robots | Paid (Asset Store) |

### Shader reference
- **Catlike Coding** (catlikecoding.com/unity/tutorials/hex-map/) — hex mesh + territory blending
- **Sebastian Lague** (YouTube) — procedural mesh, shader tutorials
- **Ben Cloward** (YouTube) — Shader Graph grid patterns

### Vizuální reference hry
- **Polytopia** — clean low-poly hex tiles s barevnými frakcemi
- **Dorfromantik** — soft hex vizuál
- **Splatoon** — territory "ink" coverage (čitelný na první pohled)
- **Auralux** — minimalist RTS s territory glow
- **Civilization VI** — colored borders expanding around cities

---

## 9. Produkční workflow

### Fáze 1: Příprava simulace (1-2 týdny)
1. Vylepšit vizuály (post-processing, territory borders, particle effects)
2. Přidat Cinemachine pro dynamickou kameru
3. Implementovat slow-motion systém
4. Přidat replay přehrávač z JSONL souborů
5. Přidat zvukové efekty (alespoň základní)

### Fáze 2: Trénink a analýza (3-7 dní)
1. Spustit plný trénink (10M+ steps)
2. Analyzovat replay soubory — najít zajímavé momenty
3. Identifikovat milníky (kdy se objevila první strategie?)
4. Vybrat nejlepší hry pro video

### Fáze 3: Nahrávání (1-2 dny)
1. Unity Recorder — nahrát gameplay v 1080p/4K
2. Nahrát klíčové momenty s různými kamerami
3. Nahrát side-by-side srovnání (Gen 0 vs Gen final)
4. Nahrát TensorBoard grafy a statistiky
5. Nahrát heatmapy a strategy overlaye

### Fáze 4: Post-produkce (3-5 dní)
1. Sestříhat podle scénáře (sekce 2)
2. Přidat voiceover
3. Přidat hudbu a sound effects
4. Přidat text overlaye, annotations, arrows
5. Thumbnail: dramatický záběr Roboti vs Mutanti, text "AI LEARNED TO..."
6. A/B test thumbnailů

### Fáze 5: Publikace
1. SEO title: "I Trained AI Robots and Mutants to Fight for Territory"
2. Tags: AI, machine learning, simulation, unity, ML-Agents
3. Description s timestamps pro každou sekci
4. Chapters v YouTube
5. Community post den před publikací

---

## 10. Thumbnail koncept

### Varianta A: "Battle"
- Levá strana: modrý robot (close-up, svítící oči)
- Pravá strana: zelený mutant (close-up, tentákly)
- Střed: exploze/blesk mezi nimi
- Text: "AI WAR" velkým písmem
- Hexagonální mapa v pozadí (rozmazaná)

### Varianta B: "Evolution"
- Vlevo: Gen 0 — chaotický screenshot (rozmazaný, šedý)
- Vpravo: Gen final — organizovaná bitva (ostrý, barevný)
- Šipka uprostřed
- Text: "AI Learned Strategy"

### Varianta C: "Territory"
- Top-down pohled na mapu — modrá vs zelená území
- Dramatická frontlinie uprostřed
- Jednotky v boji na hranici
- Text: "WHO WINS?"

---

## Aktualizace

| Datum | Změna |
|-------|-------|
| 2026-03-19 | Počáteční verze plánu |
