---
name: feedback_asmdef_references
description: Při přidání using direktivy nebo vytváření testů vždy ověřit a doplnit asmdef reference chain
type: feedback
---

Při přidání nové `using` direktivy do .cs souboru, VŽDY ověřit, že cílová assembly je v `references` příslušného `.asmdef`.

**Why:** Unity .asmdef striktně omezuje viditelnost. `using Unity.InferenceEngine;` v Agents assembly bez reference na InferenceEngine.asmdef → CS0234. Tato chyba se opakovala vícekrát a stojí čas.

**How to apply:**
1. Při každém `using XYZ;` zkontrolovat, ve které assembly leží .cs soubor (najít nejbližší .asmdef)
2. Ověřit, že namespace `XYZ` patří do assembly uvedené v `references`
3. Pokud chybí: `grep "guid" Library/PackageCache/<package>/<path>.asmdef.meta` → přidat GUID do references
4. Totéž platí pro testy — test asmdef musí referencovat všechny runtime assemblies
5. **NIKDY nepoužívat Editor třídy (z Assembly-CSharp-Editor) v PlayMode/EditMode testech** — Editor assembly není dostupná z test assemblies. Místo toho použít runtime API nebo testovací helper

**Aktuální řetězec referencí v tomto projektu:**
- **Grid.asmdef**: žádné závislosti (leaf)
- **Agents.asmdef**: Grid + Unity.ML-Agents + Unity.InferenceEngine
- **Game.asmdef**: Grid + Agents + Unity.ML-Agents + Unity.InferenceEngine
- **PlayModeTests.asmdef**: Grid + Agents + Game + Unity.ML-Agents + TestRunner
- **EditModeTests.asmdef**: Grid + Agents + Game + TestRunner
