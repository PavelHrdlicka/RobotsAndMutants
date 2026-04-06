---
name: Žádná redundantní UI — každá funkcionalita jen na jednom místě
description: Globální pravidlo — Play, Replays, Settings pouze v MainMenu, Editor tools jen pro dev/training
type: feedback
---

Každá uživatelská funkce musí být dostupná pouze na jednom místě.

**Why:** Project Tools mělo Play vs AI, Replay, Launch Game — vše duplicitní s MainMenu. Uživatel nevěděl kam kliknout. Editor tools ztrácel přehlednost.

**How to apply:**
- MainMenu (runtime): Play, Replays, Settings, Quit — vše pro hráče
- Project Tools (Editor): Configuration, Training, Launch (Main Menu + AI vs AI), Testing & Analysis — vše pro vývojáře
- Nikdy neduplikovat stejnou funkcionalitu na dvou místech
