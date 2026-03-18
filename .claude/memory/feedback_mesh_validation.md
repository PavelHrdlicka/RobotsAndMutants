---
name: Vždy validovat procedurální meshe testy
description: Při generování meshů vždy přidat EditMode test na winding order, normály a bounds. Předchází neviditelným meshům kvůli back-face cullingu.
type: feedback
---

Při vytváření nebo úpravě procedurálních meshů vždy přidat/aktualizovat EditMode test, který ověří:
- Winding order (normály míří správným směrem — typicky +Y pro top-down pohled)
- Počet vertexů a trojúhelníků odpovídá očekávání
- Bounds nejsou nulové

**Why:** Hex mesh měl counter-clockwise winding → normály mířily dolů → kamera shora viděla back-faces → hexagony byly neviditelné. Chyba nebyla zřejmá vizuálně ani z kódu.

**How to apply:** Ke každému skriptu generujícímu mesh přidat odpovídající test v Assets/Tests/EditMode/. Testy se spouští automaticky v Unity Test Runner.
