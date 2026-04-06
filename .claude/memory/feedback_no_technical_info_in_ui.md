---
name: V UI nikdy technické informace — pouze lidsky čitelné údaje
description: Globální pravidlo — žádné názvy souborů, GUID, JSON cesty, interní ID v hráčském UI
type: feedback
---

V hráčském UI nikdy zobrazovat technické/vývojářské informace. Pouze lidsky čitelné údaje.

**Why:** Replay HUD zobrazoval název JSONL souboru (game_1_20260406_142036.jsonl) místo čitelného popisu. Hra je pro lidského hráče, ne pro vývojáře.

**How to apply:**
- Žádné názvy souborů, cesty, GUID, interní ID
- Datum: "06.04.2026 14:20" (ne "20260406_142036")
- Výsledek: "Mutants win (20 rounds)" (ne "winner: Mutant, rounds: 20")
- Match číslo: pořadové číslo v seznamu (ne interní matchCounter)
- Vždy se zeptat: "Pochopil by toto normální hráč bez technických znalostí?"
- Platí pro VŠECHNY UI panely (HUD, menu, replay, post-match)
