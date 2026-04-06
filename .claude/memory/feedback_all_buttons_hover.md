---
name: Všechna tlačítka musí mít vizuální hover feedback
description: Globální pravidlo — každé tlačítko musí reagovat na najetí kurzorem
type: feedback
---

Všechna interaktivní tlačítka musí mít vizuální hover feedback (zvýraznění při najetí kurzorem).

**Why:** V hlavním menu PLAY a QUIT měly hover, ale REPLAYS a SETTINGS ne. Příčina: Image.color byl nastaven na barvu pozadí, takže Button.colors tinting nefungoval (double-tint). Oprava: Image.color = Color.white, Button.colors se stará o tinting.

**How to apply:**
- Canvas Button: `img.color = Color.white`, nastavit `colors.normalColor`, `colors.highlightedColor = normal * 1.3f`
- OnGUI Button: buď `GUIStyle.hover` nastavit, nebo ručně detekovat `Rect.Contains(Event.current.mousePosition)`
- Platí pro VŠECHNA tlačítka v celém projektu (menu, HUD, replay, settings)
