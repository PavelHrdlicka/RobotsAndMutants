---
name: Modální okno musí blokovat VŠECHNY kliknutí pod sebou
description: OnGUI modal musí blokovat Canvas i OnGUI eventy — GUI.ModalWindow + GraphicRaycaster disable
type: feedback
---

Modální okno (quit confirm, delete confirm) musí blokovat VŠECHNY vstupy pod sebou.

**Why:** Fullscreen invisible GUI.Button blokoval i kliknutí na tlačítka UVNITŘ modalu (Cancel nefungoval). OnGUI a Canvas jsou nezávislé systémy.

**How to apply:**
- Použít `GUI.ModalWindow()` — Unity nativně blokuje OnGUI eventy mimo modal
- Při zobrazení modálu: `GraphicRaycaster.enabled = false` (blokuje Canvas)
- Při zavření modálu: obnovit `GraphicRaycaster.enabled = true`
- NIKDY fullscreen invisible `GUI.Button` jako blocker — sežere kliknutí i na modalu
