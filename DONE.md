# DONE — Changelog

## 2026-03-18

### Fix: PlayMode testy — NullReferenceException v PlayModeRunTask
- Test SetUp ničil `PlaymodeTestsController` ("Code-based tests runner") spolu se všemi scene objekty
- Přidán guard `go.name != "Code-based tests runner"` do všech 4 PlayMode test souborů
- Přidáno jako Záruka 5 do memory pravidel
- **Commit:** `271c89b`

### Fix: HUD problikávání — špatný OnGUI throttle
- `lastGuiRepaintTime` se aktualizoval jen na Repaint eventu, ale blokoval i Layout
- GUI viditelné jen 1/6 framů → problikávání
- Odstraněn rendering throttle, data cache (`HudCacheInterval=0.15s`) zůstává
- **Commit:** `dd22f84`

### Fix: Prefab reference ztracená při vstupu do Play mode
- `Reset+Setup` vytvořil nový prefab ale nepersistoval scénu
- Přidáno `AssetDatabase.SaveAssets()` + `EditorSceneManager.SaveOpenScenes()` před Play
- **Commit:** `65fefed`

### Fix: SaveOpenScenes během Play mode — InvalidOperationException
- `delayCall` se spouštěl během přechodu z Play mode
- Nahrazeno `playModeStateChanged` callbackem (čeká na `EnteredEditMode`)
- **Commit:** `40989ab`

### Fix: Port 5004 conflict — osiřelé Python procesy
- Domain reload (kompilace) clearuje static `trainingProcess` ale OS proces běží dál
- `StopTraining()` nyní zabíjí osiřelé Python procesy se stejným exe path
- **Commit:** `40967c2`

### Vylepšení: Training vizuální feedback
- Validace Python/config cest před startem procesu
- try-catch kolem Process.Start s chybovou hláškou
- Blikající zelený indikátor s PID během tréninku
- Console auto-open při startu tréninku
- Monitoring ukončení procesu (exit code v GUI i Console)
- **Commity:** `3a3a611`, `2c00328`

### Memory: Nová pravidla
- Záruka 5: nikdy neničit "Code-based tests runner" v PlayMode test SetUp
- OnGUI throttle anti-pattern: throttlovat data, ne rendering
- SaveBeforePlay: vždy SaveAssets + SaveOpenScenes před isPlaying=true
- Auto-read all logs: při chybě automaticky číst Unity, ML-Agents, TensorBoard logy
- Claude memory soubory přidány do gitu (`.claude/memory/`)
- **Commity:** `22c84fa`, `692020f`
