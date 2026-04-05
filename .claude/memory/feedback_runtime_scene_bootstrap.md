---
name: Game scene must have runtime bootstrap (not only Editor setup)
description: Herní scéna musí fungovat jak z Editoru (HexGridSetup) tak z runtime menu (GameBootstrap via sceneLoaded)
type: feedback
---

Herní scéna (SampleScene) MUSÍ mít dvě cesty inicializace:
1. **Editor path:** HexGridSetup.SetupScene() — volá se z ProjectToolsWindow před Play mode
2. **Runtime path:** GameBootstrap (SceneManager.sceneLoaded) — volá se automaticky při každém přechodu scény

GameBootstrap musí:
- Detekovat chybějící HexGrid a vytvořit celou scénu (Grid, GameManager, UnitFactory, kamera)
- Přeskočit pokud HexGrid už existuje (Editor setup)
- Přeskočit pro MainMenu scénu
- Aplikovat config z menu (BoardSize, GameMode)

**NIKDY použít `RuntimeInitializeOnLoadMethod(AfterSceneLoad)`** pro bootstrap, který musí běžet při scene transitions. AfterSceneLoad se volá JEN JEDNOU při startu aplikace. Použít `SceneManager.sceneLoaded` event.

**Why:** 
1. HexGridSetup je v Assets/Editor/ — nedostupný za runtime → prázdná scéna při přechodu z menu.
2. AfterSceneLoad bootstrap běžel jen při startu, ne při menu→game přechodu → druhá prázdná scéna.

**How to apply:**
- Pro runtime setup vždy `SceneManager.sceneLoaded` (ne AfterSceneLoad)
- Při přidání nové herní scény: zajistit že GameBootstrap ji umí inicializovat
- Při přidání nového požadovaného komponentu: přidat do GameBootstrap i HexGridSetup
- Testy `GameBootstrap_UsesSceneLoadedEvent` a `GameBootstrap_DoesNotUseAfterSceneLoad` toto automaticky ověřují
