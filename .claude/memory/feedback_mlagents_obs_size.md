---
name: feedback_mlagents_obs_size
description: Při změně počtu observací v CollectObservations vždy synchronně aktualizovat VectorObservationSize
type: feedback
---

Při každé změně počtu `sensor.AddObservation()` volání aktualizovat VectorObservationSize na všech místech kde je nastaven (kód nebo prefaby).

**Why:** Přidal jsem fortification + respawn cooldown (56→63 obs), ale `UnitFactory.cs` měl `VectorObservationSize = 56` hardcoded. Výsledek: "More observations (63) made than vector observation size (56). The observations will be truncated." — agenti dostávali ořezaná data.

**How to apply:**
1. Při každé změně CollectObservations: grep `VectorObservationSize` a `vector_observation_size` v celém projektu
2. Aktualizovat všechna místa ve stejném commitu jako změna observací
3. V Unity projektech zkontrolovat: UnitFactory.cs, prefaby, YAML config
4. Zero-fill fallback v CollectObservations (pro případ null grid) musí sedět na stejné číslo
