---
name: feedback_no_hardcoded_values
description: Nikdy nehardcodovat herní hodnoty v testech ani kódu — vždy číst z GameConfig
type: feedback
---

Nikdy nehardcodovat herní parametry (costs, damage, regen, cooldown) v testech ani v kódu.

**Why:** Při změně balance parametrů v GameConfig.asset (wallBuildCost 4→3, attackUnitDamage 3→4, slimeEntryCostRobot 3→2, baseRegenPerStep 3→2) failovaly desítky testů protože asserty měly natvrdo `Assert.AreEqual(12, ...)` místo `Assert.AreEqual(15 - cfg.attackUnitCost, ...)`. Oprava trvala přes 3 iterace.

**How to apply:**
1. V testech VŽDY číst hodnoty z `GameConfig.Instance` s fallbackem: `int cost = GameConfig.Instance != null ? GameConfig.Instance.wallBuildCost : 4;`
2. V produkčním kódu VŽDY číst z `GameConfig.Instance` (už se děje v HexMovement, HexAgent, AbilitySystem).
3. Při vytváření nového testu s energy/cost assertem — nikdy nepoužívat magická čísla, vždy odvodit z configu.
4. Při změně balance parametrů stačí změnit GameConfig.asset — testy se automaticky adaptují.
