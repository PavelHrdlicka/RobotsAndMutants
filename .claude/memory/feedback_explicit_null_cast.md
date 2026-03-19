---
name: feedback_explicit_null_cast
description: Při volání metod s null argumentem vždy explicitně přetypovat na očekávaný typ
type: feedback
---

Při předávání `null` jako argumentu metody vždy použít explicitní cast: `(TypTřídy)null`.

**Why:** Unity/C# kompilátor občas nedokáže rozlišit overloady nebo správně resolvovat `null` bez typu, což vede k CS7036 chybám. Stalo se u `GameReplayLogger.EndGame(..., null)` → oprava `(..., (HexGrid)null)`.

**How to apply:** Kdykoli voláš metodu a předáváš `null` místo class/interface parametru, piš `(ExpectedType)null`. Platí zejména pro testy a volání s mnoha parametry.
