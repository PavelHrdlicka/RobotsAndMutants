---
name: Žádná emise s HDR hodnotami — způsobuje bloom
description: _EmissionColor s multiplikátorem > 1.0 triggeruje URP bloom, zakázáno pro runtime vizuály
type: feedback
---

Nikdy nepoužívat `_EmissionColor` s hodnotami > 1.0 na runtime vizuálních objektech.

**Why:** URP bloom (DefaultVolumeProfile, threshold 0.9) zesiluje jakýkoliv pixel nad threshold. Emission color násobená 1.5-3.0× vytvářela masivní bílou záři přes celou obrazovku. Turn glow na jednotkách byl odstraněn z tohoto důvodu.

**How to apply:**
- Žádný `_EmissionColor` multiplikátor > 1.0
- Pro vizuální zvýraznění použít opaque Unlit materiál s team barvou (ne emission)
- Pro adjacency: hex obrys (ring mesh), ne glow
- Pro active turn: jiný vizuální indikátor než emission (např. outline, icon)
