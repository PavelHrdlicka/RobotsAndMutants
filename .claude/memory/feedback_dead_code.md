---
name: feedback_dead_code
description: Při větších změnách aktivně vyhledávat a odstraňovat mrtvý kód (nepoužívané třídy, metody, importy)
type: feedback
---

Při každé větší změně (refactoring, nová feature, architekturní úprava) aktivně hledat a odstraňovat mrtvý kód.

**Why:** Mrtvý kód mate čtenáře, zvětšuje codebase, a vytváří falešný dojem že systémy fungují. V tomto projektu byly nalezeny celé třídy (TerritorySystem, CombatSystem) které nikdy nebyly volány.

**How to apply:**
1. Před commitem větší změny zkontrolovat: volá někdo tuto třídu/metodu?
2. Grep pro název třídy/metody — pokud jen definice a žádné volání → smazat
3. Zkontrolovat `using` importy — nepoužívané smazat
4. Zkontrolovat private metody — pokud nikde nevolány → smazat
5. Po smazání spustit testy (EditMode + PlayMode) pro ověření
