---
name: feedback_no_write_to_assets
description: Nikdy za běhu nezapisovat soubory do Assets/ — Unity Asset Pipeline je importuje a zpomaluje hru
type: feedback
---

Nikdy za běhu (Play mode / training) nezapisovat soubory do složky Assets/. Vždy použít cestu mimo Assets/ (např. project root, Application.persistentDataPath, nebo vlastní složku v rootu projektu).

**Why:** Unity Asset Pipeline automaticky detekuje nové soubory v Assets/, generuje .meta soubory a spouští import/refresh. Při tréninku (stovky souborů) to zpomalilo simulaci z desítek turns/sec na 2 turns/sec. Replay logger zapisoval do Assets/ML-Agents/Replays/ a 225 .jsonl souborů + 225 .meta souborů způsobilo permanentní Asset Pipeline refresh.

**How to apply:** Při jakémkoliv runtime zápisu souborů (logy, replay, export, CSV, statistiky) vždy ověřit, že cílová cesta NENÍ pod Assets/. Bezpečné alternativy:
- `Path.GetFullPath("Replays")` — project root
- `Application.persistentDataPath` — user AppData
- `Path.GetFullPath("results")` — vedle ML-Agents results
