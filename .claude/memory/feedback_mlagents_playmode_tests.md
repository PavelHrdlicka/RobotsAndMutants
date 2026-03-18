---
name: feedback_mlagents_playmode_tests
description: PlayMode testy v Unity — 5 povinných záruk: HeuristicOnly guard, skip URP init, skip CreatePrimitive, workflow (exit play mode), NIKDY neničit "Code-based tests runner"
type: feedback
---

Při každém novém Unity projektu s PlayMode testy implementovat VŠECHNY čtyři záruky níže.

**Why:** Čtyři různé příčiny způsobovaly freeze/selhání PlayMode testů v tomto projektu. Každá má svůj vlastní fix. Všechny čtyři se opakují v každém Unity projektu se stejnou architekturou.

---

## Záruka 1 — ML-Agents: HeuristicOnly guard v Agent.Awake

Kdykoliv projekt dědí z `Agent`, přidat guard v `Awake()`:

```csharp
using Unity.MLAgents.Policies;

private static bool? s_testMode;

private void Awake()
{
    if (!s_testMode.HasValue)
    {
        s_testMode = false;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            if (asm.FullName.Contains("UnityEngine.TestRunner")) { s_testMode = true; break; }
    }
    if (s_testMode.Value)
    {
        var bp = GetComponent<BehaviorParameters>();
        if (bp != null) bp.BehaviorType = BehaviorType.HeuristicOnly;
    }
}
```

**Why:** `Agent.OnEnable()` volá `Academy.LazyInitialize()`. Při `BehaviorType=Default` bez Python traineru blokuje ~60s. Awake běží před OnEnable → fix zasáhne včas. Cached static → jen jednou za session.

---

## Záruka 2 — URP: skip camera init v test mode

Každý MonoBehaviour který v `Awake()`/`Start()` přidává `UniversalAdditionalCameraData` nebo jiné URP komponenty, musí tuto inicializaci přeskočit v test mode:

```csharp
private static bool? s_testMode;
private static bool IsTestMode()
{
    if (!s_testMode.HasValue)
    {
        s_testMode = false;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            if (asm.FullName.Contains("UnityEngine.TestRunner")) { s_testMode = true; break; }
    }
    return s_testMode.Value;
}

private void Awake()
{
    if (!IsTestMode())
        SetupCamera(); // AddComponent<UniversalAdditionalCameraData> atd.
}
```

**Why:** PlayMode test runner vytváří prázdnou `InitTestScene` bez nastaveného URP rendereru. Přidání `UniversalAdditionalCameraData` do takové scény způsobí hang (URP nenajde renderer). Testy nikdy nedokončí.

---

## Záruka 3 — Workflow: test runner nespouštět za běhu hry

Do ProjectToolsWindow (nebo jiného editor okna) přidat varování:

```csharp
if (EditorApplication.isPlaying)
{
    EditorGUILayout.HelpBox(
        "EXIT Play mode before running PlayMode tests!\n" +
        "Test Runner fails with 'cannot be used during play mode' if game is running.",
        MessageType.Warning);
}
```

**Why:** Pokud uživatel klikne "Run All" v Test Runner zatímco hra běží, framework volá `EditorSceneManager.GetSceneManagerSetup()` (Editor-only API) → `InvalidOperationException: This cannot be used during play mode` → celý test run selže PŘED spuštěním jakéhokoli testu.

**Správný postup:** Exit Play Mode → otevři Test Runner → Run All.

---

## Záruka 4 — GameObject.CreatePrimitive() blokuje v InitTestScene

Jakýkoliv kód volaný z testů (přes `skipMLAgents` flag nebo jinak) nesmí volat `GameObject.CreatePrimitive()`.

```csharp
// V UnitFactory nebo jiné factory třídě:
if (!skipMLAgents)  // nebo !skipVisuals
{
    // Model builders, health bars, indicators, effects
    // → tyto SMĚJÍ volat CreatePrimitive
    go.AddComponent<RobotModelBuilder>().Build();
    go.AddComponent<UnitHealthBar3D>();
    go.AddComponent<UnitActionIndicator3D>();
    go.AddComponent<AttackEffects>();
}
// Testy dostanou unit pouze s UnitData + HexMovement — to stačí
```

**Why:** `GameObject.CreatePrimitive(PrimitiveType.Cube/Sphere/...)` při prvním volání v URP projektu spustí async kompilaci shaderů. V prázdné InitTestScene bez shader cache to blokuje desítky sekund nebo indefinitely. V projektu: 6 jednotek × ~9 primitiv = 54 volání → smrtelné.

**How to apply:** Vždy guard vizuální/mesh komponenty za `if (!skipMLAgents)` (nebo dedikovaný `skipVisuals` flag). Testy potřebují pouze logické komponenty (data + movement), ne vizuály.

---

## Záruka 5 — NIKDY neničit "Code-based tests runner" GameObject v SetUp

V každém PlayMode test SetUp, který čistí scénu přes `GetRootGameObjects()`, vynechat `"Code-based tests runner"`:

```csharp
// ŠPATNĚ — zničí PlaymodeTestsController → NullReferenceException v PlayModeRunTask:
foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
    Object.Destroy(go);

// SPRÁVNĚ:
foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
    if (go.name != "Code-based tests runner")
        Object.Destroy(go);
```

**Why:** `PlaymodeTestsController` (MonoBehaviour) žije v InitTestScene jako root GameObject s názvem `"Code-based tests runner"`. `PlayModeRunTask` v editoru drží referenci na tento objekt a v každém frame kontroluje `controller.RaisedException`. Pokud test SetUp objekt zničí, Unity objekt je null/destroyed → NullReferenceException → `An unexpected error happened while running tests` → celý test run selže.

**How to apply:** Každý PlayMode test SetUp s `GetRootGameObjects()` + `Object.Destroy` musí mít guard na `go.name != "Code-based tests runner"`. Toto je páté povinné pravidlo vedle čtyř výše.

---

## Sdílený helper (lze extrahovat do utility třídy)

Stejný pattern pro detekci test mode se opakuje. V projektech s více třídami ho extrahovat:

```csharp
public static class TestModeDetector
{
    private static bool? s_isTestMode;
    public static bool IsTestMode
    {
        get
        {
            if (!s_isTestMode.HasValue)
            {
                s_isTestMode = false;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    if (asm.FullName.Contains("UnityEngine.TestRunner")) { s_isTestMode = true; break; }
            }
            return s_isTestMode.Value;
        }
    }
}
```
