---
name: feedback_mlagents_training_guards
description: Před spuštěním mlagents --resume nebo --initialize-from vždy validovat prerekvizity (existence checkpointu/ONNX)
type: feedback
---

Vždy validovat prerekvizity před spuštěním Python tréningu v konkrétním módu:
- `--resume`: musí existovat `results/{runId}/` adresář s checkpointem
- `--initialize-from=X`: musí existovat `results/X/HexRobot.onnx` (nebo ekvivalentní ONNX soubory)

**Why:** Python mlagents-learn vrátí exit code 1 pokud checkpoint neexistuje a je požadován `--resume`. Unity zobrazí jen "Training process exited with code 1" bez jasného vysvětlení proč — uživatel neví co udělal špatně.

**How to apply:**
1. V OnGUI: tlačítka pro Resume/InitFrom `GUI.enabled = false` pokud prerekvizity neexistují (s vysvětlením v labelu)
2. V `StartTraining()`: druhý guard — `Directory.Exists` / `File.Exists` → `Debug.LogError` a `return` před spuštěním procesu
3. Dvojí ochrana (UI + kód) zabraňuje jak kliknutí přes UI, tak programatickému volání bez prerekvizit
4. Podobný vzor aplikovat na všechna tlačítka která spouštějí externí procesy s fileovými prerekvizitami
