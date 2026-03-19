---
name: feedback_no_save_during_playmode
description: Nikdy neukládat soubory do Assets/ nebo .cs soubory zatímco uživatel běží v Unity Play mode
type: feedback
---

Nikdy neukládat .cs soubory ani jiné soubory v Assets/ zatímco uživatel běží v Unity Play mode. Vždy se nejdřív zeptat, zda je play mode aktivní, nebo implementovat vše najednou a uživatele požádat o restart Play mode.

**Why:** Každý uložený .cs soubor spustí Unity Asset Pipeline auto-refresh → script compilation → domain reload → 2+ sekundy freeze uprostřed simulace. Stalo se opakovaně při editaci kódu během tréninku ML-Agents — každý save způsobil viditelné zamrznutí. Přestože jsme přidali PlayModeAutoRefreshGuard, nejlepší je problému předejít.

**How to apply:** 1) Před editací souborů ověřit, zda uživatel není v Play mode, 2) Pokud ano, nasbírat všechny změny a aplikovat je najednou po výstupu z Play mode, 3) Pokud je nutné editovat během Play mode, upozornit uživatele na možný freeze, 4) Memory soubory (.md) mimo Assets/ jsou OK.
