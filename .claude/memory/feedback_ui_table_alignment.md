---
name: UI tables must have aligned columns and row numbers
description: Tabulky v UI musí mít zarovnané sloupce, číslování řádků, a čitelné rozvržení
type: feedback
---

Každá tabulka v UI (OnGUI, IMGUI) musí mít:
1. **Zarovnané sloupce** — pevné šířky, ne volný text na jednom řádku
2. **Číslování řádků** — 1-based, šedou barvou
3. **Header řádek** s názvy sloupců
4. **Oddělovací čáru** mezi headerem a daty

**Why:** Uživatel chtěl, aby Last Turns tabulka vypadala profesionálně — akce pod sebou zarovnané, řádky číslované. Volný text "R1 Robot_1: Capture" je nečitelný při více záznamech.

**How to apply:** Při vytváření jakékoliv tabulky v OnGUI definovat konstanty pro šířky sloupců (colNum, colUnit, colAction atd.), použít fixed-width layout, a přidat row numbering. Nikdy nesklápět více dat do jednoho nestrukturovaného stringu.
