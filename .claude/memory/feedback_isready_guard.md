---
name: feedback_isready_guard
description: IsReady/IsInitialized guard musí explicitně kontrolovat VŠECHNY závislosti které kód používá
type: feedback
---

Při odstraňování kódu zkontrolovat, zda odstraňovaná field/třída neslouží jako implicitní proxy pro "inicializace dokončena" v IsReady nebo podobném guardu.

**Why:** Smazání TerritorySystem rozbilo IsReady garanci. Stará IsReady kontrolovala `territorySystem != null` (nastavován jako poslední v Start()). To implicitně zaručovalo že `abilitySystem` (nastavený dříve) je také inicializovaný. Po smazání TerritorySystem byl IsReady true dříve než byl abilitySystem nastaven → NullReferenceException.

**How to apply:**
1. Před smazáním pole/třídy zkontrolovat: je použita v IsReady nebo podobném guardu?
2. Pokud ano a existují jiné pole inicializovaná dříve — přidat je do guardu explicitně
3. IsReady/IsInitialized by měl obsahovat všechna pole která kód PŘÍMO používá (ne proxy přes jiné pole)
4. Po každém dead code removal spustit hru a ověřit že NullReference nevznikla
