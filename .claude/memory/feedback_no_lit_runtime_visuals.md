---
name: Runtime vizuály musí používat Unlit shader, nikdy Lit
description: CreatePrimitive dává Lit shader → bloom. Vždy přiřadit vlastní Unlit materiál + MaterialPropertyBlock pro barvu
type: feedback
---

Runtime vizuální objekty (šipky, indikátory, aury) musí používat URP/Unlit shader.

**Why:** CreatePrimitive(Cube/Sphere) přiřadí defaultní URP/Lit materiál. Lit shader s jasnými barvami triggeruje bloom post-processing → masivní bílá záře přes celou obrazovku. Navíc `.material.color` vytváří novou instanci materiálu (leak).

**How to apply:**
- Vždy vytvořit static shared Unlit materiál (`Shader.Find("Universal Render Pipeline/Unlit")`)
- Přiřadit přes `.sharedMaterial` (ne `.material`)
- Barvu nastavovat přes `MaterialPropertyBlock` (zero allocation)
- Nikdy `.material.color` — vždy `SetPropertyBlock`
- Nikdy `_EmissionColor` s HDR multiplikátorem > 1.0
