---
name: Static Material/Texture must have cleanup
description: Každý static Material/Texture musí mít GetStaticMaterials() registrovaný v StaticResourceCleanup
type: feedback
---

Každá třída se `static Material` nebo `static Texture2D` polem MUSÍ:
1. Mít `public static Material[] GetStaticMaterials()` která vrátí materiály a vynuluje reference
2. Být registrována v `StaticResourceCleanup.Cleanup()`

Nikdy `new Material()` v Update/LateUpdate/OnGUI — použít MaterialPropertyBlock nebo pre-alokaci.

**Why:** Statické materiály přežívají domain reload ale GPU resources se neuvolní. Při každém Play se vytvoří nové → D3D11 Resource ID overflow (>1M) → červené chyby, rendering selhání. Oprava vyžaduje restart Unity.

**How to apply:**
- Při přidání `static Material`: přidat GetStaticMaterials() a registrovat v StaticResourceCleanup
- Test `AllStaticMaterials_HaveGetStaticMaterials` automaticky detekuje chybějící cleanup
- Test `AllGetStaticMaterials_RegisteredInCleanup` detekuje neregistrované třídy
- Test `NoNewMaterialInHotPaths` detekuje alokace v Update/OnGUI
