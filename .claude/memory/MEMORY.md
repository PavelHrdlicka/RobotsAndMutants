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
- [feedback_ongui_stable_controls.md](feedback_ongui_stable_controls.md) — OnGUI: nikdy podmíněně přidávat/odebírat controls; použít GUI.enabled=false místo if/skip
- [feedback_mlagents_training_guards.md](feedback_mlagents_training_guards.md) — Před --resume/--initialize-from vždy validovat prerekvizity (checkpoint/ONNX existence)
- [feedback_no_write_to_assets.md](feedback_no_write_to_assets.md) — Nikdy za běhu nezapisovat soubory do Assets/ (Asset Pipeline import zpomaluje hru)
- [feedback_explicit_null_cast.md](feedback_explicit_null_cast.md) — Při předávání null jako argumentu vždy explicitně přetypovat: (Type)null
- [feedback_replace_all_review.md](feedback_replace_all_review.md) — Po replace_all vždy zkontrolovat všechny výskyty — mohou mít jiný kontext
- [feedback_preallocate_collections.md](feedback_preallocate_collections.md) — V hot-path kódu nikdy new kolekce — pre-alokovat a Clear()
- [feedback_no_save_during_playmode.md](feedback_no_save_during_playmode.md) — Nikdy neukládat .cs soubory zatímco Unity běží v Play mode (domain reload freeze)
- [feedback_mlagents_training_perf.md](feedback_mlagents_training_perf.md) — ML-Agents trénink: threaded:true, batch/buffer dle HW, device:cpu pro editor training
- [feedback_pytorch_cuda_unity.md](feedback_pytorch_cuda_unity.md) — PyTorch CUDA nelze s Unity na jedné GPU (device mismatch, VRAM konflikt, auto-detect past)
- [feedback_test_visibility.md](feedback_test_visibility.md) — Metody volané z testů musí být public, ne internal (Unity asmdef boundary)
- [feedback_button_full_text.md](feedback_button_full_text.md) — Tlačítka musí být dostatečně široká pro celý text labelu
- [feedback_no_hardcoded_values.md](feedback_no_hardcoded_values.md) — Nikdy nehardcodovat herní hodnoty v testech — vždy číst z GameConfig
- [feedback_no_physics_raycast_without_colliders.md](feedback_no_physics_raycast_without_colliders.md) — Nikdy Physics.Raycast bez colliderů — použít Plane.Raycast
- [feedback_pool_gpu_resources.md](feedback_pool_gpu_resources.md) — Nikdy new Material/Texture za běhu — pre-alokovat pool
- [feedback_ui_display_names.md](feedback_ui_display_names.md) — V UI nikdy podtržítka — Robot_1 → Robot 1
- [feedback_ui_table_alignment.md](feedback_ui_table_alignment.md) — UI tabulky: zarovnané sloupce, číslování řádků, header
- [feedback_no_backward_compat.md](feedback_no_backward_compat.md) — Žádná zpětná kompatibilita — vždy aplikovat nová pravidla
- [feedback_sessionstate_gamemode.md](feedback_sessionstate_gamemode.md) — Každý Play mode entry point musí nastavit SessionState("GameMode")
- [feedback_static_material_cleanup.md](feedback_static_material_cleanup.md) — Každý static Material musí mít GetStaticMaterials() v StaticResourceCleanup
- [feedback_ui_empty_state.md](feedback_ui_empty_state.md) — Prázdný seznam: schovat akční tlačítka, zobrazit placeholder, Back vždy viditelné
- [feedback_runtime_scene_bootstrap.md](feedback_runtime_scene_bootstrap.md) — Herní scéna musí fungovat z Editoru i z runtime menu (GameBootstrap)
- [feedback_deferred_play_mode.md](feedback_deferred_play_mode.md) — Editor Play mode launch: vždy čekat na EnteredEditMode, nikdy isPlaying přímo
- [feedback_no_aftersceneload_for_transitions.md](feedback_no_aftersceneload_for_transitions.md) — AfterSceneLoad = jen 1×; pro scene transitions použít SceneManager.sceneLoaded
- [feedback_shared_material_in_builders.md](feedback_shared_material_in_builders.md) — Model builders: static cached materiály + .sharedMaterial, nikdy per-unit kopie
- [feedback_replay_start_in_all_modes.md](feedback_replay_start_in_all_modes.md) — replayLogger.StartGame() musí být v Start(), ne jen v ResetGame()
- [feedback_new_input_system_only.md](feedback_new_input_system_only.md) — Pouze nový Input System (InputAction), starý Input API zakázán

## Project
- [project_youtube_goal.md](project_youtube_goal.md) — Cíl: YouTube video ve stylu AI Warehouse / MrBeast (YOUTUBE-PLAN.md)
- [project_two_modes.md](project_two_modes.md) — Dva módy: TRAINING (max speed, no graphics) a REPLAY (krásné vizuály pro video)
- [project_playable_version.md](project_playable_version.md) — Plán na hratelnou verzi pro lidi (human vs human / vs AI)

## Reference
- [reference_unity_log.md](reference_unity_log.md) — Unity Editor log: C:\Users\mail\AppData\Local\Unity\Editor\Editor.log
