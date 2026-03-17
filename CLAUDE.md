# Hex Territory Control — Roboti vs. Mutanti

## Tech stack
- Unity 6 (6000.3 LTS) + Universal Render Pipeline (URP)
- ML-Agents 4.0.2 (com.unity.ml-agents)
- C# skripty v Assets/Scripts/
- Python 3.10 + mlagents pro trénink

## Projekt
AI simulace na hexagonálním hřišti 10×10.
Dva asymetrické týmy po 6 jednotkách: Roboti (staví krabice, mají štít na vlastním území) vs. Mutanti (šíří sliz, mají bonus rychlosti na slizu).
Territory control s reinforcement learning (MA-POCA).
Herní mechaniky: zabírání území, boj, pěstování/zpevnění, adjacency bonus za sousední spojence.
Smrt → 30 kroků cooldown → respawn na základně.

## Konvence
- Skripty v Assets/Scripts/, podsložky podle funkce (Grid, Agents, Game)
- Prefaby v Assets/Prefabs/
- ML-Agents konfigurace v Assets/ML-Agents/
- Komentáře v kódu pouze anglicky
- kód a proměnné pouze anglicky
