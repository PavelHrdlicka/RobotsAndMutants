---
name: feedback_ongui_throttle
description: Unity OnGUI — nikdy nethrottlovat rendering přes early return, throttlovat pouze výpočty dat
type: feedback
---

Nikdy nepřidávat časový throttle na rendering v `OnGUI()` přes `return` (early exit).

**Why:** Unity volá `OnGUI()` pro každý event typ zvlášť: `Layout`, `Repaint`, plus input eventy (`MouseMove`, `KeyDown`, ...). Pokud throttle aktualizuje timestamp pouze na `Repaint` eventu ale blokuje i ostatní eventy, `Layout` a `Repaint` se rozjedou — GUI je viditelné jen na jeden repaint cycle z N framů. Při 60fps a throttlu 10fps = GUI viditelné 1/6 framů → problikávání.

```csharp
// ŠPATNĚ — způsobuje flickering:
float now = Time.unscaledTime;
if (now - lastGuiRepaintTime < 0.1f) return;
if (Event.current.type == EventType.Repaint) lastGuiRepaintTime = now;

// SPRÁVNĚ — throttlovat pouze výpočty dat, ne rendering:
private void OnGUI()
{
    RefreshHudCache(); // interně cachuje výsledky po 0.15s
    DrawEverything();  // vždy, každý frame
}

private void RefreshHudCache()
{
    if (Time.unscaledTime - hudCacheTime < 0.15f) return;
    hudCacheTime = Time.unscaledTime;
    // ... přepočítej data ...
}
```

**How to apply:** Kdykoli je HUD/OnGUI pomalý, cachovat výsledky výpočtů (počty tiles, stringy, statistiky) s intervalem ~0.1–0.2s. Nikdy neblokovat samotné volání GUI.Box/GUI.Label — ta jsou levná a musí běžet každý frame pro všechny eventy.
