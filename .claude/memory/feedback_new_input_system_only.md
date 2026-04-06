---
name: Use only new Input System, never old UnityEngine.Input
description: Projekt používá výhradně nový Input System (activeInputHandler=1), starý Input API je zakázán
type: feedback
---

Projekt má `activeInputHandler: 1` (pouze nový Input System). Starý `UnityEngine.Input` API je zakázán.

Pravidla:
- `Input.GetKey`, `Input.GetMouseButton`, `Input.mousePosition` → NIKDY
- Použít `InputAction` + `WasPressedThisFrame()` / `ReadValue<T>()`
- Každý `InputAction` musí být `Dispose()`-ován v `OnDestroy()`
- `OnGUI` `Event.current.keyCode` je OK (nezávislé na Input System)
- Agents.asmdef musí mít referenci na Unity.InputSystem (GUID: 75469ad4d38634e559750d17036d5f7c)

**Why:** `activeInputHandler: 2` (oba systémy) generoval warning "Input Manager is marked for deprecation" při každém startu. Přepnuto na `1` po přepsání HumanInputManager na nový Input System.

**How to apply:**
- Při přidání nového inputu: použít `new InputAction(...)` + `Enable()` + `Dispose()`
- Test `NoOldInputAPI_InProjectScripts` skenuje CELÝ projekt na starý Input API
- Test `AgentsAsmdef_ReferencesInputSystem` ověřuje asmdef referenci
