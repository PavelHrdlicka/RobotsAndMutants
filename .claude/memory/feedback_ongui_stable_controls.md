---
name: feedback_ongui_stable_controls
description: OnGUI: hodnoty pro větvení cachovat při Layout eventu; GUI.enabled místo podmíněného vykreslení
type: feedback
---

Hodnoty použité pro větvení v OnGUI (if/else bloky) NESMÍ se měnit mezi Layout a Repaint eventem — obě volání OnGUI musí renderovat přesně stejný počet controls.

**Why:** Unity GUILayout počítá počet controls v Layout eventu a očekává STEJNÝ počet v Repaint eventu. Pokud podmínka (např. `trainingProcess.HasExited`) změní hodnotu mezi Layout a Repaint, vznikne `ArgumentException: Getting control N's position in a group with only N controls`. Toto se stalo při exitu Python procesu uprostřed GUILayout cyklu.

**How to apply (správné pořadí řešení):**

**1. HLAVNÍ FIX — cachovat v Layout eventu:**
```csharp
private bool cachedTrainingRunning;
private void OnGUI()
{
    if (Event.current.type == EventType.Layout)
        cachedTrainingRunning = trainingProcess != null && !trainingProcess.HasExited;
    // pak používat jen cachedTrainingRunning, nikoli přímý výpočet
}
```
Toto je správné řešení pro if/else bloky s jiným počtem controls v každé větvi.

**2. PRO JEDNOTLIVÉ CONTROLS — GUI.enabled místo podmíněného vykreslení:**
```csharp
// Správně — počet controls je vždy stejný:
GUI.enabled = hasCheckpoint;
if (GUILayout.Button(hasCheckpoint ? "Resume" : "Resume — no checkpoint")) { ... }
GUI.enabled = true;

// Špatně — mění počet controls:
if (hasCheckpoint) { if (GUILayout.Button("Resume")) { ... } }
```

**3. Kombinace obou:** Caching pro if/else větvení (různý počet controls v každé větvi), GUI.enabled pro jednotlivé podmíněné controls.
