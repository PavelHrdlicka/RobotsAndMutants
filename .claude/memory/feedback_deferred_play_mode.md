---
name: Editor Play mode launch must use deferred pattern
description: Nikdy isPlaying=true přímo po isPlaying=false — vždy čekat na EnteredEditMode callback
type: feedback
---

Při spouštění Play mode z Editor kódu, který potřebuje nejdřív otevřít scénu, MUSÍ se použít deferred pattern:

1. `EditorApplication.isPlaying = false` (exit current Play)
2. Registrovat callback na `playModeStateChanged`
3. V callbacku čekat na `PlayModeStateChange.EnteredEditMode`
4. TEPRVE PAK otevřít scénu, uložit, a nastavit `isPlaying = true`

NIKDY: `isPlaying = false; OpenScene(); isPlaying = true;` — Unity nezvládne přechod a načte backup scénu místo nové.

**Why:** Launch Main Menu nastavil `isPlaying = true` okamžitě po `isPlaying = false`. Unity nestihlo ukončit Play mode, `OpenScene` se ignorovalo, a Play mode načetl backup SampleScene místo MainMenu. Hráč viděl prázdnou obrazovku.

**How to apply:**
- Každá metoda v ProjectToolsWindow, která otevírá scénu + vstupuje do Play, musí mít wrapper (LaunchX) + implementaci (DoX)
- Wrapper: if isPlaying → exit + defer; else → DoX přímo
- DoX: OpenScene + Save + isPlaying=true
- Test `AllLaunchMethods_UseDeferredPattern` toto ověřuje
