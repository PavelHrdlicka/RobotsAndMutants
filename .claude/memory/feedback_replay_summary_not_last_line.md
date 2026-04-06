---
name: Replay JSONL summary není poslední řádek
description: Po summary řádku následuje territory snapshot — parser musí hledat explicitně podle type
type: feedback
---

V replay JSONL souboru summary NENÍ poslední řádek. Po summary následuje territory snapshot řádek.

**Why:** ReplaysPanel hledal summary jako poslední řádek souboru, ale ten byl territory snapshot. Replay se zobrazoval jako "Incomplete" přestože byl kompletní.

**How to apply:**
- Při parsování replay souborů vždy hledat řádek podle `"type":"summary"`, nikdy nepředpokládat pozici
- Test `ReplaysPanel_ParsesSummaryNotLastLine` ověřuje toto pravidlo
