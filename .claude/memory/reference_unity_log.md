---
name: Unity Editor log cesta
description: Unity Editor log je na C:\Users\mail\AppData\Local\Unity\Editor\Editor.log — číst odtud chyby místo ptaní na screenshot.
type: reference
---

Unity Editor log: `C:\Users\mail\AppData\Local\Unity\Editor\Editor.log`

Při chybách v Unity vždy nejdřív přečíst poslední chyby z tohoto souboru příkazem:
```
grep -n "error CS\|Error\|Exception" "/c/Users/mail/AppData/Local/Unity/Editor/Editor.log" | tail -30
```
