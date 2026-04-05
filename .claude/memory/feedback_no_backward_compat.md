---
name: No backward compatibility
description: Nikdy nepřidávat zpětnou kompatibilitu — vždy aplikovat nová pravidla
type: feedback
---

Nikdy nepřidávat zpětnou kompatibilitu pro staré formáty, replaye, uložené stavy apod.

**Why:** Vyvíjíme novou hru, ne udržujeme produkční systém. Zpětná kompatibilita je zbytečná komplexita.

**How to apply:** Při jakékoliv změně formátu (replay, config, naming) prostě aplikovat nová pravidla. Staré soubory se ignorují nebo smažou. Nikdy nenavrhovat fallback/migration kód.
