---
name: Každá změna GameMode musí nastavit SessionState
description: Nejen Editor entry points, ale i runtime tlačítka (Watch Replay) musí persistovat mode do SessionState
type: feedback
---

Každá změna `GameModeConfig.CurrentMode` musí být doprovázena `SessionState.SetString("GameMode", ...)`.

**Why:** Watch Replay tlačítko v post-match HUD nastavovalo jen static field `GameModeConfig.CurrentMode`, ale ne SessionState. Po scene reload (`LoadScene`) proběhne domain reload → `InitSessionState` přečte starý SessionState a přepíše mode zpět na HumanVsAI. ReplayPlayer se nespustí.

**How to apply:**
- Platí pro VŠECHNY místa kde se mění GameMode — Editor entry points I runtime UI tlačítka
- Při Replay navíc persistovat `ReplayPlayer_PendingPath` do SessionState (static field se ztratí domain reloadem)
- Testy: `WatchReplayHUD_SetsReplaySessionState` (EditMode), `WatchReplayHUD_SetsSessionState` (PlayMode)
