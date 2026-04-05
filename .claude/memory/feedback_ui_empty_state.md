---
name: UI empty state — hide action buttons, show placeholder
description: Pokud je seznam prázdný, schovat akční tlačítka a zobrazit placeholder text. Back tlačítko vždy viditelné.
type: feedback
---

Každý UI panel se seznamem a akčními tlačítky MUSÍ:
1. Schovat akční tlačítka (Watch, Delete, Edit...) když je seznam prázdný
2. Zobrazit placeholder text ("No replays found", "No items"...)
3. Disablovat akční tlačítka když nic není vybráno (`button.interactable = selectedIndex >= 0`)
4. Back/návratové tlačítko musí být VŽDY viditelné a dostatečně velké

**Why:** Hráč viděl Watch a Delete tlačítka na prázdném replay seznamu bez možnosti vrátit se zpět. Back tlačítko bylo malý šedý text v rohu mimo viditelnou oblast. Hráč se zasekl na obrazovce.

**How to apply:**
- Při vytváření UI panelu s listem: UpdateEmptyState() schová/zobrazí tlačítka podle `entries.Count == 0`
- Back tlačítko vždy jako CreateMenuButton (ne CreateText), centrované, min 200x44px
- Test `ReplaysPanel_HidesButtonsWhenEmpty` a `BackButtons_AreProperButtons_NotText` toto automaticky ověřují
