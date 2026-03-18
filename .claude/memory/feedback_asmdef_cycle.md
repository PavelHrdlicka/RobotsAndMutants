---
name: feedback_asmdef_cycle
description: Před přidáním reference do asmdef vždy zkontrolovat, zda cíl already nereferuje zdroj (cyklická závislost)
type: feedback
---

Před přidáním `GUID:xxx` do asmdef references zjistit GUID obou asmdef a zkontrolovat, zda cíl neobsahuje GUID zdroje.

**Why:** Přidal jsem `Game` referenci do `Agents.asmdef`, ale `Game.asmdef` už referencoval `Agents` — Unity hodilo "cyclic dependencies detected" a projekt přestal kompilovat.

**How to apply:**
1. Nová utilita potřebuje typ z assembly X → umísti ji do assembly X, ne do jiné assembly
2. Pokud přidáváš referenci A → B, grep meta soubory: `Game.asmdef` obsahuje GUID `Agents`? Pokud ano → nevkládat opačnou referenci
3. Obecné pravidlo umístění: třída závisí na `UnitData` (Agents) → patří do Agents; závisí jen na Grid → patří do Grid nebo Game
