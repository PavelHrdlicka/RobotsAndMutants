---
name: feedback_test_visibility
description: Methods called from tests must be public — Unity test asmdefs can't see internal members
type: feedback
---

Metody volané z testů musí být `public`, ne `internal`.

**Why:** Unity test assembly (EditModeTests.asmdef) kompiluje do vlastní DLL a nevidí `internal` členy z Assembly-CSharp bez explicitního `[InternalsVisibleTo]`. Chyba CS0117 "does not contain a definition" při kompilaci testů.

**How to apply:** Při psaní testovatelného kódu — pokud metoda bude volaná z testů, rovnou ji udělat `public`. Pokud chci zachovat `internal`, musím přidat `[assembly: InternalsVisibleTo("EditModeTests")]` do runtime assembly.
