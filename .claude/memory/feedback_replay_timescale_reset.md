---
name: Replay mode must reset timeScale to 1
description: Replay přehrávání musí explicitně nastavit Time.timeScale=1f, nesmí dědit z předchozího režimu
type: feedback
---

Replay mód musí vždy nastavit `Time.timeScale = 1f` při startu. Nesmí dědit timeScale z předchozího režimu (Training=20x).

**Why:** Po hře HumanVsAI (timeScale=1) se replay zdál OK, ale po Training (timeScale=20) se přehrával 20× rychleji. `Time.time` se škáluje podle timeScale, takže turnDelay=1s se v reálu stal 0.05s.

**How to apply:**
- Každý herní režim (Training, HumanVsAI, Replay) musí explicitně nastavit svůj timeScale
- Nikdy nespoléhat na zdědění timeScale z předchozí scény
- Test `ReplayPlayer_SetsTimeScaleToOne` v SilentTrainingFlagTests.cs to ověřuje statickou analýzou
