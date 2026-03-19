---
name: feedback_mlagents_training_perf
description: ML-Agents training config — threaded:true, batch/buffer dle GPU, ale device:cpu pokud Unity sdílí GPU
type: feedback
---

Při konfiguraci ML-Agents tréninku VŽDY zkontrolovat a nastavit tyto klíčové parametry:

1. **`threaded: true`** — Bez tohoto Unity blokuje během gradient update (viditelné freeze). S threaded trainer běží trénink paralelně se simulací.
2. **`batch_size`** — Přizpůsobit HW. RTX 4070 (12GB) zvládne batch_size 2048-4096 pro malé sítě (512×2).
3. **`buffer_size`** — Typicky 10× batch_size.
4. **`num_epoch`** — S větším batchem stačí méně epoch (3 místo 5) pro stejnou kvalitu.
5. **`torch_settings.device: cpu`** — Pokud Unity běží na stejné GPU (editor training), MUSÍ být `cpu`. Viz `feedback_pytorch_cuda_unity.md`.

**Why:** Uživatel s RTX 4070 + i7-14700KF + 64GB RAM zažíval pravidelné freeze během tréninku. Příčina: `threaded: false` (Unity čeká na gradient update). S `device: cuda` padal trénink kvůli VRAM konfliktu s Unity.

**How to apply:** Při vytváření nového ML-Agents training configu: 1) Zapnout `threaded: true`, 2) Přizpůsobit batch/buffer velikosti HW, 3) Nastavit `device: cpu` pro editor training (CUDA jen pro headless/standalone build).
