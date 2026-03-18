---
name: feedback_post_fix_rules
description: Po každé opravě chyby nebo warningu odvodit obecné pravidlo a uložit do paměti (pokud dává smysl)
type: feedback
---

Po každé opravě compilační chyby nebo warningu (pokud proběhnou testy OK) se zeptat: "Dá se tato chyba zobecnit jako pravidlo, které ji zabrání v budoucím projektu?" Pokud ano, uložit jako feedback memory.

**Why:** Dnešní session opravovala 6 chyb za sebou (CS0103, cyclic dependency, CS0308, CS0219, CS0114, obs truncated) — většina z nich by se dala předem vyhnout obecným pravidlem.

**How to apply:**
- CS0103 (name does not exist) po přesunu souboru → zkontrolovat asmdef příslušnost
- Cyclic dependency → zkontrolovat zpětnou referenci před přidáním nové
- CS0308 (non-generic List) po odstranění using → grep zbývající generické typy
- CS0219 (unused variable) → při refactoru zkontrolovat lokální const/var deklarace
- CS0114 (hides inherited) v ML-Agents → přidat `new` keyword na Awake()
- Obs size truncated → synchronizovat VectorObservationSize při změně CollectObservations
- Pravidlo uložit jen pokud je obecně platné (ne jen pro jeden případ)
