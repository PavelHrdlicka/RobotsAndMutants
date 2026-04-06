---
name: Replay nesmí automaticky loopovat po skončení
description: Play() po Finished stavu nesmí restartovat — vyžadovat explicitní Restart()
type: feedback
---

Replay přehrávání nesmí automaticky restartovat po skončení. `Play()` ve stavu `Finished` musí být no-op.

**Why:** Uživatel zmáčkl Space po skončení replay a celá hra se přehrála znovu, opakovaně. Nechtěný loop znemožňoval normální ovládání.

**How to apply:**
- `Play()` ve stavu Finished → return (no-op)
- Explicitní `Restart()` metoda pro restart od začátku
- V HUD zobrazit "Restart" tlačítko místo ">" když je stav Finished
- Space klávesa nereaguje ve stavu Finished, klávesa R = restart
- Testy: `Play_WhenFinished_DoesNotRestart`, `Restart_WhenFinished_GoesToStartAndPlays`
