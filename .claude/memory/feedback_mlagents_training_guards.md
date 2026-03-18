---
name: feedback_mlagents_training_guards
description: Prerekvizity pro mlagents trénink + robustní Play mode entry (k_IsEnteringPlayMode, DrawSection try/finally)
type: feedback
---

**Prerekvizity pro mlagents módy:**
Vždy validovat prerekvizity před spuštěním Python tréningu:
- `--resume`: musí existovat `results/{runId}/` adresář s checkpointem
- `--initialize-from=X`: musí existovat `results/X/HexRobot.onnx`

**Why:** exit code 1 pokud checkpoint chybí a je požadován `--resume`.

**How to apply:**
1. V OnGUI: `GUI.enabled = false` pokud prerekvizity neexistují
2. V `StartTraining()`: druhý guard `Directory.Exists`/`File.Exists` → `Debug.LogError` a `return`

---

**Play mode entry — prevence dvojitého `isPlaying=true`:**
NIKDY nespoléhat na `isPlayingOrWillChangePlaymode` pro detekci Play mode domain reloadu. Použít `k_IsEnteringPlayMode` SessionState flag + `playModeStateChanged` event.

**Why:** `isPlayingOrWillChangePlaymode` vrací false během Play mode domain reload → `TryResumeAutoPlay` spustí druhý `isPlaying=true` → `NullReferenceException` v ML-Agents `RpcCommunicator.Initialize` → Python exit code 120.

**How to apply:**
1. Registrovat `playModeStateChanged` v `[InitializeOnLoadMethod]` (přežije každý domain reload)
2. Nastavit `k_IsEnteringPlayMode = true` těsně PŘED `isPlaying = true`
3. `TryResumeAutoPlay()`: pokud `k_IsEnteringPlayMode = true` → return bez druhého `isPlaying = true`
4. `OnPlayModeStateChanged(EnteredPlayMode/EnteredEditMode)` → clear oba flagy

---

**DrawSection try/finally:**
Vždy zabalit content lambda DrawSection do `try/finally` aby `EndVertical()` byl vždy zavolán.

**Why:** Exception v content() způsobí GUILayout Begin/End mismatch → "Getting control N in group with only N controls" error → přeruší ML-Agents Play mode inicializaci → Python exit code 120.
