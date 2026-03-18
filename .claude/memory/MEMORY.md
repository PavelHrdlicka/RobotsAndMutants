# Memory Index

## Feedback
- [feedback_unity_editor_scripts.md](feedback_unity_editor_scripts.md) — Vždy vytvářet Editor skripty s MenuItem pro automatizaci Unity setupu
- [feedback_suggest_patterns.md](feedback_suggest_patterns.md) — Proaktivně navrhovat opakující se patterny a po schválení ukládat
- [feedback_mesh_validation.md](feedback_mesh_validation.md) — Při generování meshů vždy přidat EditMode test na winding/normály/bounds
- [feedback_asmdef_references.md](feedback_asmdef_references.md) — Při vytváření testů vždy zajistit asmdef řetězec (runtime → test reference)
- [feedback_project_tools_window.md](feedback_project_tools_window.md) — Všechny Editor akce přidávat do ProjectToolsWindow, v novém projektu vytvořit jako první
- [feedback_no_confirmation.md](feedback_no_confirmation.md) — Provádět akce bez ptaní, rovnou implementovat

- [feedback_read_unity_log.md](feedback_read_unity_log.md) — Při chybách v Unity automaticky číst Editor.log, neptat se na screenshot
- [feedback_read_all_logs.md](feedback_read_all_logs.md) — Při jakékoliv chybě automaticky číst VŠECHNY relevantní logy (Unity, ML-Agents, TensorBoard, nové nástroje)

- [feedback_playmode_tests.md](feedback_playmode_tests.md) — Vždy vytvářet i PlayMode testy vedle EditMode testů
- [feedback_all_from_unity.md](feedback_all_from_unity.md) — Vše ovládat z Unity UI, žádný terminál. Platí pro všechny Unity projekty
- [feedback_business_md.md](feedback_business_md.md) — Po každé logické změně aktualizovat BUSINESS.md v rootu projektu
- [feedback_git_workflow.md](feedback_git_workflow.md) — After every tested change, commit and push to GitHub (main branch)
- [feedback_mlagents_playmode_tests.md](feedback_mlagents_playmode_tests.md) — PlayMode testy: 4 záruky — HeuristicOnly guard, skip URP camera init, skip CreatePrimitive ve vizuálech, workflow (exit play mode)
- [feedback_ongui_throttle.md](feedback_ongui_throttle.md) — Unity OnGUI: nikdy nethrottlovat rendering přes early return, throttlovat pouze výpočty dat
- [feedback_save_before_play.md](feedback_save_before_play.md) — Vždy SaveAssets + SaveOpenScenes před programatickým vstupem do Play mode
- [feedback_dead_code.md](feedback_dead_code.md) — Při větších změnách aktivně vyhledávat a odstraňovat mrtvý kód
- [feedback_asmdef_cycle.md](feedback_asmdef_cycle.md) — Před přidáním asmdef reference zkontrolovat zpětnou závislost (prevence cyklů)
- [feedback_mlagents_obs_size.md](feedback_mlagents_obs_size.md) — Při změně počtu observací synchronizovat VectorObservationSize ve stejném commitu
- [feedback_post_fix_rules.md](feedback_post_fix_rules.md) — Po každé opravě chyby/warningu (testy OK) odvodit a uložit obecné pravidlo
- [feedback_isready_guard.md](feedback_isready_guard.md) — IsReady guard musí explicitně kontrolovat všechny závislosti; proxy-field smazání může skrytě rozbít garanci

## Reference
- [reference_unity_log.md](reference_unity_log.md) — Unity Editor log: C:\Users\mail\AppData\Local\Unity\Editor\Editor.log
