---
name: Game scene must have runtime bootstrap (not only Editor setup)
description: Herní scéna musí fungovat jak z Editoru (HexGridSetup) tak z runtime menu (GameBootstrap)
type: feedback
---

Herní scéna (SampleScene) MUSÍ mít dvě cesty inicializace:
1. **Editor path:** HexGridSetup.SetupScene() — volá se z ProjectToolsWindow před Play mode
2. **Runtime path:** GameBootstrap (AfterSceneLoad) — volá se automaticky při SceneManager.LoadScene z MainMenu

GameBootstrap musí:
- Detekovat chybějící HexGrid a vytvořit celou scénu (Grid, GameManager, UnitFactory, kamera)
- Přeskočit pokud HexGrid už existuje (Editor setup)
- Přeskočit pro MainMenu scénu
- Aplikovat config z menu (BoardSize, GameMode)

**Why:** HexGridSetup je v Assets/Editor/ — nedostupný za runtime. Při přechodu z MainMenu přes SceneManager.LoadScene se scéna načetla prázdná (jen skybox). Hráč viděl prázdnou obrazovku.

**How to apply:**
- Při přidání nové herní scény: zajistit že GameBootstrap ji umí inicializovat
- Při přidání nového požadovaného komponentu do scény: přidat jeho vytvoření i do GameBootstrap
- Test `GameScene_HasBothSetupPaths` ověřuje existenci obou cest
