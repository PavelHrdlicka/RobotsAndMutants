---
name: feedback_preallocate_collections
description: V hot-path kódu (Update/FixedUpdate) nikdy nealokovat kolekce — pre-alokovat a Clear()
type: feedback
---

V kódu volaném každý frame (Update, FixedUpdate, OnActionReceived, BFS, pathfinding) nikdy používat `new HashSet`, `new Queue`, `new List` apod. Vždy pre-alokovat jako instance field a volat `.Clear()`.

**Why:** Každý `new` alokuje na heapu. Při volání každý frame se za minuty nahromadí tisíce alokací → GC kolekce → viditelné záseky (freeze na pár vteřin). Stalo se u `ComputeTerritoryInfo` — `new HashSet<HexCoord>()` + `new Queue<HexCoord>()` každý frame způsobovaly periodické zamrznutí simulace.

**How to apply:** Při psaní jakéhokoliv per-frame algoritmu (BFS, flood-fill, neighbor search, pathfinding): 1) Kolekce deklarovat jako `private readonly` field, 2) Na začátku metody volat `.Clear()`, 3) Nikdy `new` uvnitř metody. Platí pro Unity i jakýkoliv real-time kód.
