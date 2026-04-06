---
name: Jakékoliv mazání musí být potvrzeno modálním oknem
description: Globální pravidlo — žádné destruktivní akce bez potvrzení uživatelem
type: feedback
---

Jakékoliv mazání dat (soubory, záznamy, nastavení) musí být potvrzeno modálním oknem s jasným popisem co se smaže.

**Why:** Tlačítko Delete v Replays panelu okamžitě smazalo replay soubor bez jakéhokoliv potvrzení. Jedno nechtěné kliknutí = ztráta dat.

**How to apply:**
- V Editor: `EditorUtility.DisplayDialog("Confirm Delete", "...", "Delete", "Cancel")`
- V runtime: Modální overlay s tlačítky Confirm/Cancel
- Lepší alternativa: úplně odstranit Delete z hráčského UI (použít jen v Editor tools)
- Platí pro VŠECHNY destruktivní akce v celém projektu
