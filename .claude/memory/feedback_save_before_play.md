---
name: feedback_save_before_play
description: Unity Editor — vždy SaveAssets + SaveOpenScenes před EditorApplication.isPlaying = true
type: feedback
---

Před každým `EditorApplication.isPlaying = true` v Editor skriptu MUSÍ být `AssetDatabase.SaveAssets()` + `EditorSceneManager.SaveOpenScenes()`.

**Why:** Editor skripty často vytvářejí/modifikují prefaby a scénové objekty (AddComponent, přiřazení referencí). Tyto změny existují jen v paměti editoru. Když Unity vstoupí do Play mode, serializuje scénu — ale nepersistované asset reference (prefaby, materiály, ScriptableObjects) se ztratí. Výsledek: null reference za runtime, "prefab is not assigned", prázdná scéna.

```csharp
// ŠPATNĚ — reference se ztratí:
HexGridSetup.Reset();
HexGridSetup.SetupScene();
EditorApplication.isPlaying = true;

// SPRÁVNĚ — persistovat před Play:
HexGridSetup.Reset();
HexGridSetup.SetupScene();
AssetDatabase.SaveAssets();
EditorSceneManager.SaveOpenScenes();
EditorApplication.isPlaying = true;
```

**How to apply:** Kdykoli Editor skript programaticky vstupuje do Play mode (`isPlaying = true`), přidat save volání BEZPROSTŘEDNĚ předtím. Platí i pro `delayCall` a `EditorApplication.update` callbacky. Toto je obzvlášť kritické po operacích s AssetDatabase (Delete + Create prefab, CreateAsset, SaveAsPrefabAsset).
