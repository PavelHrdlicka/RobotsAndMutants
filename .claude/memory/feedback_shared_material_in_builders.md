---
name: Model builders must use shared static materials, never per-instance copies
description: V Build() metodách model builderů nikdy new Material per spawn — vždy static cache + .sharedMaterial
type: feedback
---

V model builder třídách (RobotModelBuilder, MutantModelBuilder, budoucí):
1. VŠECHNY materiály musí být `private static Material` — vytvořeny jednou, sdíleny
2. V `Build()` nikdy `new Material()` — vždy `if (mat == null) mat = CreateMat()`
3. Přiřazovat přes `.sharedMaterial =`, NIKDY `.material =` (to vytvoří kopii)
4. Speciální materiály (eye, hammer) musí být static cached, ne vytvářené per-unit

**Why:** Každý robot spawn vytvářel 3 nové materiály (hammer + 2× eye), každý mutant 2 (2× eye). Za 53 kol × 8 jednotek = stovky leaked materiálů → D3D11 Resource ID overflow (>1M) → červené chyby.

**How to apply:**
- Při přidání nového modelu/materiálu v Build(): přidat static field + null guard
- Nikdy `.material = new Material(...)` — vždy static cache + `.sharedMaterial`
- Test `ModelBuilders_NeverCreatePerUnitMaterials` — skenuje Build() na `new Material(`
- Test `ModelBuilders_UseSharedMaterialNotInstanceMaterial` — skenuje Build() na `.material =`
- Test `ModelBuilders_EyeMaterialIsCached` — ověřuje static eyeMaterial field
