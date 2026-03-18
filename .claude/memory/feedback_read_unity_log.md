---
name: Vždy automaticky číst Unity log při chybách
description: Při jakékoliv zmínce o chybě v Unity (nebo po změně kódu) automaticky přečíst Editor.log. Neptyat se na screenshot. Platí pro všechny Unity projekty.
type: feedback
---

Kdykoliv uživatel zmíní chybu v Unity, nebo po větší změně kódu, automaticky přečíst chyby z Unity Editor logu. Nikdy se neptat na screenshot chyb.

Na Windows je log vždy: `C:\Users\mail\AppData\Local\Unity\Editor\Editor.log`

**Why:** User chce plynulý workflow. Ptát se na screenshot zdržuje.

**How to apply:** V jakémkoliv Unity projektu po zmínce chyby nebo po dokončení změn rovnou spustit grep na Editor.log a diagnostikovat. Pokud cesta neexistuje, najít log přes `%LOCALAPPDATA%\Unity\Editor\`.
