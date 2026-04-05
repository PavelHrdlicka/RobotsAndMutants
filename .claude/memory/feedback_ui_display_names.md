---
name: UI display names without underscores
description: V UI nikdy nezobrazovat podtržítka — interně OK, ale uživateli vždy formátovat (Robot_1 → Robot 1)
type: feedback
---

V uživatelském rozhraní nikdy nezobrazovat technické identifikátory s podtržítky. Interně v kódu (gameObject.name, replay data, testy) je podtržítko OK, ale při zobrazení člověku vždy nahradit mezerou nebo jiným formátováním.

**Why:** Uživatel viděl "Robot_1: Capture" v tabulce tahů — vypadá to technicky a neprofesionálně. V play mode má UI vypadat jako hotová hra, ne jako debug výstup.

**How to apply:** Při zobrazení jmen jednotek, akcí nebo jakýchkoliv identifikátorů v OnGUI/UI vždy použít formátovací helper (např. `name.Replace("_", " ")`). Nikdy přímo zobrazovat `gameObject.name` nebo interní ID uživateli.
