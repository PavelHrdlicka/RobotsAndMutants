---
name: feedback_replace_all_review
description: Po replace_all vždy zkontrolovat všechny nahrazené výskyty — mohou mít jiný kontext
type: feedback
---

Po použití replace_all (Edit tool) vždy přečíst celý soubor a ověřit, že VŠECHNY nahrazené výskyty dávají smysl v kontextu.

**Why:** replace_all nahradí textový pattern bez ohledu na sémantiku. Stejný textový výskyt může mít v jiném kontextu jiný význam — např. `, null);` v `method.Invoke(logger, null)` (reflection, očekává `object[]`) vs. v `logger.EndGame(..., null)` (očekává `HexGrid`). Slepé nahrazení způsobilo CS1503 chybu.

**How to apply:** Po každém replace_all: 1) Přečíst celý soubor, 2) Zkontrolovat každý nahrazený výskyt, 3) Pokud kontext nesedí, opravit individuálně. Alternativně — raději nahrazovat jednotlivě když string není unikátní.
