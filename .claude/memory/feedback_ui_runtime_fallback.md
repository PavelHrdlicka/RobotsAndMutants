---
name: Canvas UI panely musí mít runtime fallback pro chybějící serializované reference
description: Serializované reference (prefaby, Transform) mohou být null po rebuild scény — vždy mít runtime fallback
type: feedback
---

Canvas UI komponenty s `[SerializeField]` referencemi musí mít runtime fallback pro případ, že reference jsou null.

**Why:** ReplaysPanel měl `replayRowPrefab` a `listContent` jako serializované reference nastavené MainMenuSetup.cs. Po rebuild scény nebo při prvním spuštění byly null → panel nenašel žádné soubory (ve skutečnosti je našel, ale nemohl zobrazit řádky). Uživatel viděl prázdný seznam 3× za sebou.

**How to apply:**
- Každý Canvas panel s `[SerializeField]` referencemi musí mít `EnsureXxx()` metodu volanou na začátku hlavní logiky
- Fallback vytvoří UI programaticky (stejný kód jako MainMenuSetup ale v runtime)
- Logovat warning pokud se fallback aktivuje (znamená potřebu re-runu MainMenuSetup)
- Testy: `ReplaysPanel_HasRuntimeFallback`, `ReplaysPanel_FallbackCreatesUI`
