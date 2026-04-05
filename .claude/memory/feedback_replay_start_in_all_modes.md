---
name: Replay logging must start in GameManager.Start(), not only ResetGame()
description: replayLogger.StartGame() musí být v Start() — ResetGame() se v HumanVsAI nevolá pro první hru
type: feedback
---

`replayLogger.StartGame()` MUSÍ být voláno v `GameManager.Start()`, ne jen v `ResetGame()`.

`ResetGame()` se volá pouze z:
- ML-Agents `OnEpisodeBegin` (jen training mode)
- `AutoRestartCoroutine` (jen po dokončení hry s autoRestart=true)

V HumanVsAI mode spuštěném z MainMenu se `ResetGame()` pro PRVNÍ hru nikdy nevolá → replay se nenahraje.

**Why:** Hráč odehrál celou hru za roboty, ale v menu Replays nebyl žádný replay. replayLogger nikdy nedostal StartGame().

**How to apply:**
- Při přidání nové inicializace do ResetGame(): ověřit, že se volá i v Start()
- Každá feature, která musí běžet od PRVNÍ hry, musí mít init path v Start()
- Test `ReplayLogger_StartedInGameManagerStart` automaticky ověřuje
