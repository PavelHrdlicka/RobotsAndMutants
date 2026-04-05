---
name: Pool GPU resources — no runtime Material/Texture allocation
description: Nikdy nevytvářet new Material() nebo new Texture2D() za běhu v Update/opakovaných voláních — vždy pre-alokovat pool
type: feedback
---

Nikdy nevytvářet `new Material()`, `new Texture2D()` nebo `GameObject.CreatePrimitive()` v Update, opakovaných callbackech nebo metodách volaných každý frame. Vždy pre-alokovat pool objektů a recyklovat přes SetActive(true/false).

**Why:** HexHighlighter vytvářel nový Material + Cylinder pro každý highlight v každém frame. Po tisících volání to vyčerpalo GPU resource ID limit (max 1048575), způsobilo "Resource ID out of range" error a vizuální artefakty.

**How to apply:** Při vytváření vizuálních efektů (highlights, particles, overlays) vždy pre-alokovat pool v Awake/Start a recyklovat. V testech ověřit, že childCount se nemění po opakovaných cyklech. Přidat statickou analýzu zdrojového kódu na `new Material` v runtime metodách.
