---
name: feedback_ongui_stable_controls
description: OnGUI musí renderovat stabilní počet controls v každém volání — žádné podmíněné přidávání/odebírání controls
type: feedback
---

Nikdy nepřidávat/neodebírat GUI controls podmíněně (`if (condition) { GUILayout.Button(...); }`) pokud se podmínka může změnit mezi voláními OnGUI.

**Why:** Unity GUILayout počítá počet controls v Layout eventu a očekává STEJNÝ počet v Repaint eventu. Pokud podmínka změní počet controls mezi těmito dvěma eventy, vznikne `ArgumentException: Getting control N's position in a group with only N controls` a `GUI Error: Invalid GUILayout state`.

**How to apply:**
1. Každý control musí být vždy přítomen — nikdy ho nepřidávat/nezobrazovat jen když platí podmínka
2. Použít `GUI.enabled = false` pro zakázání controlu místo jeho vynechání
3. Pokud přece jen potřebuješ podmíněný obsah, použij `if (Event.current.type == EventType.Layout)` pro synchronizaci stavu
4. Tlačítka se proměnlivým textem OK — počet controls je pořád stejný, mění se jen obsah

Správně:
```csharp
GUI.enabled = hasCheckpoint;
if (GUILayout.Button(hasCheckpoint ? "Resume" : "Resume — no checkpoint")) { ... }
GUI.enabled = true;
```

Špatně:
```csharp
if (hasCheckpoint) {
    if (GUILayout.Button("Resume")) { ... }  // mění počet controls!
}
```
