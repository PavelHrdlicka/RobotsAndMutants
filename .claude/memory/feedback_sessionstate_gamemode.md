---
name: SessionState GameMode must be set by every Play mode entry point
description: Každý entry point do Play mode musí nastavit SessionState("GameMode") — jinak zůstane stará hodnota z předchozí session
type: feedback
---

Každý entry point, který spouští Play mode (Launch Game, Play vs AI, Load Replay, Start Training), MUSÍ nastavit `SessionState.SetString("GameMode", ...)` PŘED vstupem do Play mode.

**Why:** SessionState přežívá domain reload. Pokud entry point nenastaví GameMode, zůstane hodnota z předchozího spuštění (např. HumanVsAI zůstane při spuštění Replay). To způsobí, že GameManager startuje ve špatném režimu.

**How to apply:**
- Při přidání nového GameMode enum value: přidat SessionState writer do ProjectToolsWindow + reader do GameManager.InitSessionState
- Při přidání nového entry pointu do Play mode: vždy nastavit SessionState("GameMode")
- Test `AllGameModes_HaveSessionStateEntryPoint` to automaticky ověřuje pro všechny enum hodnoty
