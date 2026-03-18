---
name: feedback_read_all_logs
description: Při jakékoliv chybě nebo diagnostice automaticky číst VŠECHNY relevantní logy — Unity, ML-Agents, TensorBoard, i nově přidané nástroje
type: feedback
---

Při jakékoliv chybě, neočekávaném chování, nebo diagnostice AUTOMATICKY přečíst relevantní logy. Neptát se uživatele na screenshot ani neprosit o kopírování chyb — logy jsou dostupné přímo.

**Why:** Uživatel nechce ručně kopírovat chybové hlášky. Veškeré nástroje v projektu produkují logy, které jsou čitelné z disku. Aktivní čtení logů ušetří kolečka komunikace a zrychlí debug.

**How to apply:**

### 1. Při chybě v Unity
- Automaticky číst `C:\Users\mail\AppData\Local\Unity\Editor\Editor.log` (posledních ~200 řádek)
- Hledat: exceptions, errors, warnings, stack traces

### 2. Při problémech s ML-Agents tréninkem
- Číst `results/{runId}/run_logs/training_status.json` — stav tréninku, počet kroků
- Číst `results/{runId}/configuration.yaml` — aktuální konfigurace
- Hledat `[ML-Train]` v Unity Console (Editor.log)

### 3. Při problémech s TensorBoard
- Zkontrolovat existenci `results/{runId}/HexRobot/events.out.tfevents.*`
- Ověřit že tensorboard proces běží

### 4. Obecné pravidlo pro KAŽDÝ nový nástroj/skript
Kdykoli do projektu přidáme nový nástroj, skript, nebo proces:
- Zjistit kde zapisuje logy
- Přidat cestu k logům do tohoto seznamu (aktualizovat tuto memory)
- Při diagnostice automaticky číst i tyto nové logy

### Známé log cesty v projektu
| Zdroj | Cesta |
|-------|-------|
| Unity Editor | `C:\Users\mail\AppData\Local\Unity\Editor\Editor.log` |
| Unity project logs | `Logs/*.log` |
| ML-Agents training status | `results/{runId}/run_logs/training_status.json` |
| ML-Agents training config | `results/{runId}/configuration.yaml` |
| ML-Agents timers | `results/{runId}/run_logs/timers.json` |
| TensorBoard events | `results/{runId}/Hex*/events.out.tfevents.*` |
| Trained models | `results/{runId}/HexRobot.onnx`, `HexMutant.onnx` |
