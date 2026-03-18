---
name: feedback_playmode_tests
description: Vždy vytvářet EditMode i PlayMode testy + performance test na rychlost simulace
type: feedback
---

Při vytváření testů vždy vytvořit obě sady:
- EditMode testy: čistá logika, bez scény, rychlé
- PlayMode testy: integrační, s GameObjecty a MonoBehaviour lifecycle
- **Performance test**: ověří, že simulace běží dostatečně rychle (minimální turns/s)

**Why:** User chce kompletní pokrytí. EditMode testy nepokryjí runtime chování. Performance test zachytí regresi rychlosti při změnách OnGUI, HUD, nebo herní logiky — což se stalo při přidání pixel-art ikon.

**How to apply:**
1. Vytvořit Assets/Tests/PlayMode/ složku s vlastním asmdef
2. PlayMode testy pro scénáře vyžadující běžící hru (spawn, movement, combat, game loop)
3. VŽDY přidat performance test který:
   - Spustí N kol simulace (např. 100 turns)
   - Změří čas
   - Assert: turns/s >= minimální threshold (např. 50 turns/s)
   - Zachytí budoucí regresi způsobenou pomalým OnGUI, FindObjectsByType, alokacemi atd.

Šablona performance testu:
```csharp
[UnityTest]
public IEnumerator Performance_SimulationSpeed_MinimumTurnsPerSecond()
{
    // setup: create grid + units, disable ML-Agents
    float startTime = Time.realtimeSinceStartup;
    int targetTurns = 100;
    // run N turns...
    float elapsed = Time.realtimeSinceStartup - startTime;
    float turnsPerSec = targetTurns / elapsed;
    Assert.Greater(turnsPerSec, 50f, $"Simulation too slow: {turnsPerSec:F0} turns/s");
}
```
