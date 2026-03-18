---
name: Vše ovládat z Unity, ne z terminálu
description: Všechny akce (Python skripty, trénink, TensorBoard, testy, build, deploy) ovládat z Unity Editor UI. Žádné ruční příkazy v terminálu.
type: feedback
---

Uživatel nechce používat terminál. Veškeré operace musí být spustitelné z Unity editoru — přes ProjectToolsWindow tlačítka nebo MenuItem.

Pokud akce vyžaduje spuštění externího procesu (Python, npm, build tool), vytvořit Editor skript, který ho spustí přes System.Diagnostics.Process a loguje výstup do Unity Console.

**Why:** User chce jednotný workflow. Přepínání mezi terminálem a Unity zdržuje a je náchylné na chyby (špatný working directory, zapomenutý conda env, atd.).

**How to apply:** Ke každé nové funkci, která by normálně vyžadovala terminál, vytvořit tlačítko v ProjectToolsWindow. Cesty k Python exe, configům atd. hardcodovat nebo udělat konfigurovatelné v EditorPrefs. Platí pro tento i všechny budoucí Unity projekty.
