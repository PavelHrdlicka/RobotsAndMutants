---
name: feedback_pytorch_cuda_unity
description: PyTorch CUDA nelze používat současně s Unity na jedné GPU — device mismatch, VRAM konflikt, auto-detect past
type: feedback
---

NIKDY neinstalovat PyTorch s CUDA pokud se trénink spouští z Unity Editoru na stejné GPU.

**Problémy:**
1. **Device mismatch crash** — mlagents `torch.py` řádek 63 volá `set_torch_config(device=None)` při IMPORTU modulu, což s CUDA PyTorchem nastaví `torch.set_default_device("cuda")`. I když YAML config pak říká `device: cpu`, model tensory (normalizer `running_mean`) se vytvoří na CUDA, vstupní data přijdou na CPU → `RuntimeError: Expected all tensors to be on the same device, cuda:0 and cpu!`
2. **`device: null` je past** — znamená auto-detect, ne CPU. S CUDA PyTorchem vybere CUDA automaticky.
3. **VRAM konflikt** — Unity rendering a PyTorch CUDA sdílejí jednu GPU → deadlock nebo OOM.
4. **`CUDA_VISIBLE_DEVICES=""` nefunguje na Windows** — `torch.cuda.is_available()` stále vrací True. Musí být `CUDA_VISIBLE_DEVICES=-1`.
5. **`pip install torch==X.X.X` nerozlišuje CPU/CUDA** — pip vidí stejné číslo verze, `--force-reinstall` je nutný pro přechod mezi CPU↔CUDA buildem.

**Why:** Série pokusů: `device: cuda` → exit code 120 (VRAM), `device: null` → auto-detect zvolí CUDA → crash, `device: cpu` → stále crash kvůli import-time auto-init, `CUDA_VISIBLE_DEVICES=""` → nefunguje na Windows. Řešení: vrátit `torch+cpu` build.

**How to apply:**
- Pro editor training (Unity + Python na jednom stroji s jednou GPU): vždy `torch+cpu` build, `device: cpu`
- Pro headless/standalone build (bez Unity renderingu): lze `torch+cuda`, `device: cuda`
- Při změně torch buildu: `pip install torch==X.X.X+cpu --index-url https://download.pytorch.org/whl/cpu --force-reinstall`
- Nikdy nepoužívat `--force-reinstall` na CUDA torch v projektu kde se trénuje z Unity Editoru
