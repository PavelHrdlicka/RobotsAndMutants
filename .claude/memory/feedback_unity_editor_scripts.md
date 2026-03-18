---
name: Unity workflow — vše přes Editor skripty
description: User chce minimalizovat ruční práci v Unity editoru. Vždy vytvářet Editor skripty (Assets/Editor/) s MenuItem, které automatizují setup scény, prefabů, komponent atd.
type: feedback
---

Když vytvářím Unity komponenty nebo nastavuji scénu, vždy přidej i Editor skript (Assets/Editor/) s menu příkazem, který celý setup provede automaticky.

**Why:** User pracuje primárně v konzoli/Claude Code a chce jen spustit menu příkaz v Unity místo ručního tahání komponent, vytváření prefabů a konfigurování scény.

**How to apply:** Ke každé nové funkci (skripty, prefaby, scéna) přibalím Editor skript s `[MenuItem("Tools/...")]`, který vše nastaví jedním kliknutím. Pokud rozšiřuji existující setup, aktualizuji stávající Editor skript.
