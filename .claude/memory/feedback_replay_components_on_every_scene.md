---
name: ReplayPlayer+HUD musí existovat na KAŽDÉ herní scéně
description: GameBootstrap musí vždy přidat ReplayPlayer+ReplayPlayerHUD, ne jen HexGridSetup
type: feedback
---

ReplayPlayer a ReplayPlayerHUD komponenty musí existovat na GameManager objektu v KAŽDÉ herní scéně, bez ohledu na to, jak byla vytvořena.

**Why:** Watch Replay nefungovalo 3× za sebou. Příčina: ReplayPlayer se přidával pouze v Editor-only HexGridSetup.cs, ale ne v GameBootstrap.cs (runtime). Po kliknutí na Watch Replay se scéna reloadovala, ReplayPlayer neexistoval → game loop startoval normálně → AI hry se hrály rychle bez replay HUD.

**How to apply:**
- `GameBootstrap.EnsureReplayComponents()` VŽDY přidá ReplayPlayer + ReplayPlayerHUD pokud chybí
- Volá se na KAŽDÉM `sceneLoaded` (ne jen při vytváření nové scény)
- `GameManager.Start()` musí mít explicitní Replay mode branch: `gameOver=true, autoRestart=false, timeScale=1f`
- `GameManager.Start()` nesmí volat `replayLogger.StartGame()` v Replay mode
- Testy: `GameBootstrap_EnsuresReplayComponents`, `GameManager_HandlesReplayMode`, `GameManager_SkipsReplayLoggingInReplayMode`
