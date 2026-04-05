---
name: Never use AfterSceneLoad for code that must run on scene transitions
description: RuntimeInitializeOnLoadMethod(AfterSceneLoad) fires only ONCE at app start — use SceneManager.sceneLoaded for scene transitions
type: feedback
---

`RuntimeInitializeOnLoadMethod(AfterSceneLoad)` fires ONLY ONCE when the application starts. It does NOT fire again when `SceneManager.LoadScene()` transitions between scenes.

Pro kód, který musí běžet při KAŽDÉM přechodu scény, použít:
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
static void Register() { SceneManager.sceneLoaded += OnSceneLoaded; }
static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { /* ... */ }
```

**Why:** GameBootstrap používal AfterSceneLoad — fungoval při prvním spuštění, ale ne při přechodu z MainMenu do SampleScene přes LoadScene. Výsledek: prázdná scéna.

**How to apply:**
- `AfterSceneLoad` / `BeforeSceneLoad` — pouze pro jednorázovou inicializaci (registrace eventů, static state reset)
- `SceneManager.sceneLoaded` — pro kód, který musí reagovat na KAŽDÉ načtení scény
- Test `GameBootstrap_DoesNotUseAfterSceneLoad` automaticky detekuje chybné použití
