---
name: No Physics.Raycast without colliders
description: Nikdy nepoužívat Physics.Raycast na objekty bez colliderů — použít Plane.Raycast nebo jinou geometrickou metodu
type: feedback
---

Nikdy nepoužívat Physics.Raycast pro detekci kliknutí na hex tile nebo jiný objekt bez Collideru. Physics.Raycast tiše selže (vrátí false) a nic se nestane — žádný error, žádný warning.

**Why:** HumanInputManager původně používal Physics.Raycast, ale hex tiley nemají Collidery. Klikání nefungovalo bez jakékoliv chybové hlášky.

**How to apply:** Při implementaci klikatelných objektů vždy ověřit, zda mají Collider. Pokud ne, použít Plane.Raycast (rovina y=0) nebo jinou matematickou metodu. Přidat test, který ověří absenci Physics.Raycast ve zdrojovém kódu.
