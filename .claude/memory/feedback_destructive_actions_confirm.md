---
name: Destruktivní akce vyžadují potvrzení modálním oknem
description: Globální pravidlo — quit, delete, reset a jakákoliv nevratná akce musí mít potvrzovací dialog
type: feedback
---

Jakákoliv nevratná akce (quit, delete, reset) musí vyžadovat potvrzení modálním oknem.

**Why:** Quit button v menu okamžitě ukončoval hru. Delete v Replays okamžitě mazal soubor. Jedno nechtěné kliknutí = ztráta dat/progress.

**How to apply:**
- Quit: modální overlay "Quit Game? Are you sure?" s Quit/Cancel tlačítky
- Delete: buď zcela odstranit z hráčského UI, nebo přidat potvrzení
- Reset: EditorUtility.DisplayDialog v Editor, modální overlay v runtime
- Nejlepší: destruktivní akce zcela odebrat z hráčského UI (ponechat jen v Editor tools)
- Platí pro VŠECHNY nevratné akce v celém projektu
