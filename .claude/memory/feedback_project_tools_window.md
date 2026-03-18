---
name: feedback_project_tools_window
description: Všechny Editor akce přidávat do ProjectToolsWindow, v novém projektu vytvořit jako první. Auto-open vpravo, runInBackground.
type: feedback
---

Každou novou Editor akci (setup, reset, generování, trénink, utility) přidat jako tlačítko do ProjectToolsWindow (Assets/Editor/ProjectToolsWindow.cs). MenuItem v menu Tools ponechat jako alternativu, ale primární přístup je přes okno.

V jakémkoliv novém Unity projektu vytvořit ProjectToolsWindow jako jednu z prvních věcí.

**Why:** User chce mít všechny akce na jednom místě v dokovatelném okně, ne hledat v menu. Rychlejší workflow. Simulace musí běžet i na pozadí.

**How to apply:**
1. Při vytváření nové Editor funkce: vytvořit statickou metodu + přidat tlačítko do ProjectToolsWindow ve správné sekci (DrawSection)
2. Okno se VŽDY automaticky otevírá při startu editoru — použít `[InitializeOnLoadMethod]` s `EditorApplication.delayCall`
3. Dockovat vedle Inspectoru (pravá strana): `GetWindow<T>("name", false, typeof(InspectorWindow))`
4. VŽDY nastavit `Application.runInBackground = true` — simulace běží i když Unity nemá focus (přepnutí na jiné okno)
5. Pokud okno neexistuje v novém projektu, vytvořit ho jako PRVNÍ věc

Šablona pro auto-open:
```csharp
[InitializeOnLoadMethod]
private static void AutoOpen()
{
    EditorApplication.delayCall += () =>
    {
        if (!HasOpenInstances<ProjectToolsWindow>())
            ShowWindow();
        Application.runInBackground = true;
    };
}
```
