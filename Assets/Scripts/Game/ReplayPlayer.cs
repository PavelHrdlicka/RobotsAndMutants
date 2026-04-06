using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

/// <summary>
/// Plays back a recorded JSONL replay file on the existing hex grid.
/// Disables ML-Agents and GameManager's game loop, then drives unit state
/// and tile ownership from replay data. Existing visual systems (HexVisuals,
/// UnitHealthBar3D, UnitActionIndicator3D) react automatically.
/// </summary>
public class ReplayPlayer : MonoBehaviour
{
    /// <summary>Set by Editor before entering Play mode. Cleared after loading.</summary>
    public static string PendingReplayPath;

    public enum PlaybackState { Stopped, Playing, Paused, Finished }

    [Header("Playback")]
    public PlaybackState state = PlaybackState.Stopped;
    public float turnDelay = 1.0f; // seconds between turns (default: 1 turn/sec)
    public int currentRound;
    public int currentTurnIndex;

    // Parsed replay data.
    private ReplayData.ReplayFile replay;
    private string replayFileName;

    // Unit lookup.
    private Dictionary<string, UnitData> unitMap = new Dictionary<string, UnitData>();
    private Dictionary<string, HexMovement> movementMap = new Dictionary<string, HexMovement>();

    // References.
    private GameManager gm;
    private HexGrid grid;
    private UnitFactory unitFactory;

    private float nextTurnTime;

    // ── Public API ──────────────────────────────────────────────────────

    public ReplayData.ReplayFile Replay => replay;
    public string FileName => replayFileName;
    public int TotalRounds => replay?.maxRound ?? 0;
    public int TotalTurns => replay?.turns.Count ?? 0;

    /// <summary>Human-readable title: "06.04 14:20 — You win! (20 rounds)".</summary>
    public string DisplayTitle
    {
        get
        {
            string date = ParseDateFromFileName(replayFileName);
            string winner = replay?.summary.winner ?? "";
            int rounds = replay?.maxRound ?? 0;
            string winText = FormatWinner(winner, replay?.header.humanTeam);
            return $"{date} — {winText} ({rounds} rounds)";
        }
    }

    /// <summary>Format winner as human-friendly: "You win!" / "AI wins" / "Draw".</summary>
    public static string FormatWinner(string winner, string humanTeam)
    {
        if (string.IsNullOrEmpty(winner) || winner == "None") return "Draw";
        if (!string.IsNullOrEmpty(humanTeam) && winner == humanTeam) return "You win!";
        if (!string.IsNullOrEmpty(humanTeam)) return "AI wins";
        return $"{winner}s win";
    }

    private static string ParseDateFromFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "";
        // Format: game_N_YYYYMMDD_HHMMSS.jsonl
        string name = System.IO.Path.GetFileNameWithoutExtension(fileName);
        var parts = name?.Split('_');
        if (parts == null || parts.Length < 4) return "";
        try
        {
            string d = parts[2]; // YYYYMMDD
            string t = parts[3]; // HHMMSS
            return $"{d.Substring(6, 2)}.{d.Substring(4, 2)}.{d.Substring(0, 4)} {t.Substring(0, 2)}:{t.Substring(2, 2)}";
        }
        catch { return fileName; }
    }

    /// <summary>Current turn description for HUD display.</summary>
    public string CurrentTurnDescription
    {
        get
        {
            if (replay == null || currentTurnIndex >= replay.turns.Count) return "";
            return FormatTurnDescription(replay.turns[currentTurnIndex]);
        }
    }

    /// <summary>Previous turn description (the last applied turn).</summary>
    public string PreviousTurnDescription
    {
        get
        {
            if (replay == null || currentTurnIndex <= 0) return "";
            return FormatTurnDescription(replay.turns[currentTurnIndex - 1]);
        }
    }

    private static string FormatTurnDescription(ReplayData.Turn t)
    {
        if (t.action == "Capture" && t.hasCaptured)
            return $"{t.unitName}: Capture ({t.capturedQ},{t.capturedR})";
        if ((t.action == "BuildWall" || t.action == "PlaceSlime" || t.action == "DestroyWall") && t.hasBuilt)
            return $"{t.unitName}: {t.action} → ({t.builtQ},{t.builtR})";
        if (t.action == "Attack" && t.hasAttackHex)
        {
            if (t.targetUnit != null)
                return $"{t.unitName}: Attack {t.targetUnit} → ({t.attackHexQ},{t.attackHexR})";
            // Wall attack — show remaining HP.
            string hpInfo = t.wallHP >= 0 ? $" HP:{t.wallHP}" : "";
            return $"{t.unitName}: Attack wall → ({t.attackHexQ},{t.attackHexR}){hpInfo}";
        }
        return $"{t.unitName}: {t.action} at ({t.q},{t.r})";
    }

    /// <summary>
    /// Initialize replay player for testing — bypasses Start() coroutine.
    /// Call after grid and units are ready.
    /// </summary>
    public void TestInitialize(ReplayData.ReplayFile replayFile, HexGrid hexGrid, UnitFactory factory)
    {
        replay = replayFile;
        grid = hexGrid;
        unitFactory = factory;
        replayFileName = "test_replay";
        BuildUnitMap();
        ResetToStart();
        state = PlaybackState.Paused;
    }

    // ── Lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        // Recover path from SessionState before domain-reload wipes static fields.
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(PendingReplayPath))
            PendingReplayPath = UnityEditor.SessionState.GetString("ReplayPlayer_PendingPath", "");
        UnityEditor.SessionState.EraseString("ReplayPlayer_PendingPath");
#endif

        // Immediately block GameManager's game loop so it doesn't start episodes.
        if (!string.IsNullOrEmpty(PendingReplayPath))
        {
            var gm = GetComponent<GameManager>();
            if (gm != null)
            {
                gm.gameOver = true;
                gm.autoRestart = false;
            }
        }
    }

    private IEnumerator Start()
    {
        if (string.IsNullOrEmpty(PendingReplayPath))
        {
            enabled = false;
            yield break;
        }

        string path = PendingReplayPath;
        PendingReplayPath = null;

        // Wait for grid and units to initialize.
        yield return null;
        yield return null;
        yield return null;

        gm = GetComponent<GameManager>();
        grid = Object.FindFirstObjectByType<HexGrid>();
        unitFactory = Object.FindFirstObjectByType<UnitFactory>();

        if (grid == null || unitFactory == null)
        {
            Debug.LogError("[Replay] HexGrid or UnitFactory not found.");
            enabled = false;
            yield break;
        }

        // Parse replay file.
        try
        {
            replay = ReplayData.Parse(path);
            replayFileName = System.IO.Path.GetFileName(path);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Replay] Failed to parse {path}: {ex.Message}");
            enabled = false;
            yield break;
        }

        if (replay.turns.Count == 0)
        {
            Debug.LogWarning("[Replay] Replay file has no turns.");
            enabled = false;
            yield break;
        }

        Debug.Log($"[Replay] Loaded {replayFileName}: match #{replay.header.match}, " +
                  $"{replay.turns.Count} turns, {replay.maxRound} rounds, winner: {replay.summary.winner}");

        // Disable GameManager's game loop and sync maxRounds for HUD.
        if (gm != null)
        {
            gm.gameOver = true;
            gm.autoRestart = false;
            gm.maxRounds = replay.maxRound;
        }

        // Disable all ML-Agents.
        DisableAllAgents();

        // Build unit lookup.
        BuildUnitMap();

        // Reset all tiles to neutral.
        foreach (var tile in grid.Tiles.Values)
        {
            if (!tile.isBase)
            {
                tile.Owner = Team.None;
                tile.TileType = TileType.Empty;
                tile.WallHP = 0;
            }
        }

        // Reset all units to alive at base, full HP.
        foreach (var unit in unitFactory.AllUnits)
            unit.ResetUnit();

        // Ensure normal speed for replay (may inherit timeScale=20 from Training).
        Time.timeScale = 1f;

        state = PlaybackState.Paused;
        currentTurnIndex = 0;
        currentRound = 0;
        nextTurnTime = Time.time;
    }

    private void Update()
    {
        if (state != PlaybackState.Playing) return;
        if (replay == null || currentTurnIndex >= replay.turns.Count)
        {
            state = PlaybackState.Finished;
            return;
        }

        if (Time.time >= nextTurnTime)
        {
            StepOneTurn();
            nextTurnTime = Time.time + turnDelay;
        }
    }

    private void LateUpdate()
    {
        // Pulse flashed tiles between base color and white for visibility.
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
        PulseTile(flashedTile, flashedTileColor, pulse);
        PulseTile(flashedSecondaryTile, flashedSecondaryColor, pulse);
    }

    private static void PulseTile(HexTileData tile, Color baseColor, float pulse)
    {
        if (tile == null) return;
        var meshGen = tile.GetComponent<HexMeshGenerator>();
        if (meshGen == null) return;
        Color bright = Color.Lerp(baseColor, Color.white, pulse * 0.35f);
        meshGen.SetColor(bright);
    }

    // ── Playback controls ───────────────────────────────────────────────

    public void Play()
    {
        if (state == PlaybackState.Finished) return; // Use Restart() to replay from beginning.
        state = PlaybackState.Playing;
        nextTurnTime = Time.time;
    }

    public void Restart()
    {
        JumpToStart();
        state = PlaybackState.Playing;
        nextTurnTime = Time.time;
    }

    public void Pause() => state = PlaybackState.Paused;

    public void TogglePlayPause()
    {
        if (state == PlaybackState.Playing) Pause();
        else Play();
    }

    public void StepOneTurn()
    {
        if (replay == null || currentTurnIndex >= replay.turns.Count)
        {
            state = PlaybackState.Finished;
            return;
        }

        ApplyTurn(replay.turns[currentTurnIndex]);
        currentTurnIndex++;

        if (currentTurnIndex >= replay.turns.Count)
            state = PlaybackState.Finished;
    }

    public void StepOneRound()
    {
        if (replay == null || currentTurnIndex >= replay.turns.Count) return;

        int targetRound = replay.turns[currentTurnIndex].round + 1;
        while (currentTurnIndex < replay.turns.Count && replay.turns[currentTurnIndex].round < targetRound)
        {
            ApplyTurn(replay.turns[currentTurnIndex]);
            currentTurnIndex++;
        }

        if (currentTurnIndex >= replay.turns.Count)
            state = PlaybackState.Finished;
    }

    public void JumpToRound(int targetRound)
    {
        ResetToStart();

        // Apply all turns up to targetRound.
        while (currentTurnIndex < replay.turns.Count && replay.turns[currentTurnIndex].round <= targetRound)
        {
            ApplyTurn(replay.turns[currentTurnIndex]);
            currentTurnIndex++;
        }

        if (currentTurnIndex >= replay.turns.Count)
            state = PlaybackState.Finished;
        else
            state = PlaybackState.Paused;
    }

    public void JumpToTurn(int targetTurnIndex)
    {
        targetTurnIndex = Mathf.Clamp(targetTurnIndex, 0, replay?.turns.Count ?? 0);
        ResetToStart();

        while (currentTurnIndex < targetTurnIndex)
        {
            ApplyTurn(replay.turns[currentTurnIndex]);
            currentTurnIndex++;
        }

        if (currentTurnIndex >= replay.turns.Count)
            state = PlaybackState.Finished;
        else
            state = PlaybackState.Paused;
    }

    public void StepBackOneTurn()
    {
        if (replay == null || currentTurnIndex <= 0) return;
        JumpToTurn(currentTurnIndex - 1);
    }

    public void StepBackOneRound()
    {
        if (replay == null || currentRound <= 0) return;
        JumpToRound(currentRound - 1);
    }

    public void JumpToStart()
    {
        ResetToStart();
        state = PlaybackState.Paused;
    }

    public void JumpToEnd()
    {
        if (replay == null) return;
        JumpToTurn(replay.turns.Count);
    }

    private void ResetToStart()
    {
        foreach (var tile in grid.Tiles.Values)
        {
            if (!tile.isBase)
            {
                tile.Owner = Team.None;
                tile.TileType = TileType.Empty;
                tile.WallHP = 0;
            }
        }
        // Reset units to alive + full energy, then place back on base hexes.
        var robotBases = grid.GetBaseTiles(Team.Robot);
        var mutantBases = grid.GetBaseTiles(Team.Mutant);

        for (int i = 0; i < unitFactory.robotUnits.Count; i++)
        {
            var unit = unitFactory.robotUnits[i];
            unit.ResetUnit();
            var baseTile = robotBases[i % robotBases.Count];
            unit.currentHex = baseTile.coord;
            if (movementMap.TryGetValue(unit.gameObject.name, out var move))
                move.PlaceAt(baseTile.coord);
        }
        for (int i = 0; i < unitFactory.mutantUnits.Count; i++)
        {
            var unit = unitFactory.mutantUnits[i];
            unit.ResetUnit();
            var baseTile = mutantBases[i % mutantBases.Count];
            unit.currentHex = baseTile.coord;
            if (movementMap.TryGetValue(unit.gameObject.name, out var move))
                move.PlaceAt(baseTile.coord);
        }

        currentTurnIndex = 0;
        currentRound = 0;
    }

    // ── Turn application ────────────────────────────────────────────────

    private void ApplyTurn(ReplayData.Turn turn)
    {
        currentRound = turn.round;

        // Clear previous turn marker, set on current unit.
        foreach (var u in unitFactory.AllUnits)
            u.isMyTurn = false;

        // Update GameManager's round counter so HUD shows correct value.
        if (gm != null)
            gm.currentRound = turn.round;

        if (!unitMap.TryGetValue(turn.unitName, out var unit)) return;

        // Mark this unit as active (for turn glow visual).
        unit.isMyTurn = true;

        // If unit was dead and now appears with hp > 0, respawn it.
        if (!unit.isAlive && turn.energy > 0)
        {
            var worldPos = grid.HexToWorld(new HexCoord(turn.q, turn.r));
            unit.Respawn(new HexCoord(turn.q, turn.r), worldPos);
        }

        // Set HP.
        unit.Energy = turn.energy;

        // Dead unit: force Dead action, teleport to base, skip all actions.
        if (turn.energy <= 0 || !unit.isAlive)
        {
            if (unit.isAlive)
            {
                int replayCd = GameConfig.Instance != null ? GameConfig.Instance.respawnCooldown : 10;
                unit.Die(replayCd);
            }
            unit.lastAction = UnitAction.Dead;
            unit.lastAttackTarget = null;
            unit.lastAttackKilled = false;
            TeleportDeadUnitToBase(unit);
            return;
        }

        // Set position.
        var coord = new HexCoord(turn.q, turn.r);
        if (unit.currentHex != coord && movementMap.TryGetValue(turn.unitName, out var movement))
        {
            unit.moveFrom = unit.currentHex;
            unit.moveTo = coord;
            movement.PlaceAt(coord);
        }

        // Set action.
        unit.lastAction = ParseAction(turn.action);

        // Apply attack target info.
        if (turn.hasTarget && !string.IsNullOrEmpty(turn.targetUnit))
        {
            unit.lastAttackTarget = unitMap.TryGetValue(turn.targetUnit, out var target) ? target : null;
            unit.lastAttackKilled = turn.killed;

            // Kill target if needed.
            if (turn.killed && unit.lastAttackTarget != null && unit.lastAttackTarget.isAlive)
            {
                int respawnCD = GameConfig.Instance != null ? GameConfig.Instance.respawnCooldown : 10;
                unit.lastAttackTarget.Die(respawnCD);
                TeleportDeadUnitToBase(unit.lastAttackTarget);
            }
        }
        else
        {
            unit.lastAttackTarget = null;
            unit.lastAttackKilled = false;
        }

        // Apply tile changes from actions.
        Team team = turn.team == "Robot" ? Team.Robot : Team.Mutant;

        if (turn.action == "BuildWall")
        {
            var buildCoord = turn.hasBuilt
                ? new HexCoord(turn.builtQ, turn.builtR)
                : coord;
            var buildTile = grid.GetTile(buildCoord);
            if (buildTile != null && !buildTile.isBase)
            {
                buildTile.Owner = team;
                buildTile.TileType = TileType.Wall;
                buildTile.WallHP = 3;
            }
        }
        else if (turn.action == "PlaceSlime")
        {
            var buildCoord = turn.hasBuilt
                ? new HexCoord(turn.builtQ, turn.builtR)
                : coord;
            var buildTile = grid.GetTile(buildCoord);
            if (buildTile != null && !buildTile.isBase)
            {
                buildTile.Owner = team;
                buildTile.TileType = TileType.Slime;
            }
        }
        else if (turn.action == "Attack" && turn.hasAttackHex && turn.wallHP >= 0)
        {
            // Wall attack — reduce HP, destroy if 0.
            var wallCoord = new HexCoord(turn.attackHexQ, turn.attackHexR);
            var wallTile = grid.GetTile(wallCoord);
            if (wallTile != null && wallTile.TileType == TileType.Wall)
            {
                wallTile.WallHP = turn.wallHP;
                if (wallTile.WallHP <= 0)
                {
                    wallTile.TileType = TileType.Empty;
                    wallTile.WallHP = 0;
                }
            }
        }
        else if (turn.action == "DestroyWall" && turn.hasBuilt)
        {
            var destroyCoord = new HexCoord(turn.builtQ, turn.builtR);
            var destroyTile = grid.GetTile(destroyCoord);
            if (destroyTile != null)
            {
                destroyTile.TileType = TileType.Empty;
                destroyTile.WallHP = 0;
            }
        }
        else if (turn.action == "Capture" && turn.hasCaptured)
        {
            var capTile = grid.GetTile(new HexCoord(turn.capturedQ, turn.capturedR));
            if (capTile != null && !capTile.isBase)
            {
                capTile.Owner = team;
                capTile.TileType = TileType.Empty;
            }
        }

        // Trigger visual effects for replay.
        TriggerReplayVisuals(unit, turn, coord);
    }

    private void TriggerReplayVisuals(UnitData unit, ReplayData.Turn turn, HexCoord coord)
    {
        // Clear previous turn's visuals before showing new ones.
        ClearReplayVisuals();

        // Attack: flash attacker hex red, target hex orange (persists until next turn).
        if (turn.action == "Attack")
        {
            // Flash attacker tile red.
            FlashTile(coord, new Color(0.9f, 0.15f, 0.1f));

            // Flash attack target tile orange.
            if (turn.hasAttackHex)
            {
                var targetCoord = new HexCoord(turn.attackHexQ, turn.attackHexR);
                FlashSecondaryTile(targetCoord, new Color(1f, 0.55f, 0.1f));
            }
        }

        // Move: show arrow from previous position to current.
        if (turn.action == "Move" || turn.action == "Capture")
        {
            if (unit.moveFrom != unit.moveTo)
                ShowMoveArrow(unit.moveFrom, unit.moveTo, unit.team);
        }

        // Build: flash the built tile with team color (persists until next turn).
        if ((turn.action == "BuildWall" || turn.action == "PlaceSlime") && turn.hasBuilt)
        {
            var builtCoord = new HexCoord(turn.builtQ, turn.builtR);
            FlashTile(builtCoord, unit.team == Team.Robot
                ? new Color(0.4f, 0.6f, 1f) : new Color(0.4f, 1f, 0.4f));
        }
    }

    // ── Replay visual helpers ───────────────────────────────────────────

    private GameObject moveArrowObj;
    private GameObject arrowHeadObj;
    private static Material arrowUnlitMaterial;
    private MaterialPropertyBlock arrowMpb;
    private HexTileData flashedTile;
    private Color flashedTileColor;
    private HexTileData flashedSecondaryTile;
    private Color flashedSecondaryColor;

    private void ShowMoveArrow(HexCoord from, HexCoord to, Team team)
    {
        var fromWorld = grid.HexToWorld(from);
        var toWorld = grid.HexToWorld(to);

        if (moveArrowObj == null)
        {
            EnsureArrowMaterial();
            if (arrowMpb == null) arrowMpb = new MaterialPropertyBlock();

            // Shaft (thin cube).
            moveArrowObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            moveArrowObj.name = "ReplayMoveArrow";
            Object.Destroy(moveArrowObj.GetComponent<Collider>());
            var shaftMr = moveArrowObj.GetComponent<MeshRenderer>();
            shaftMr.sharedMaterial = arrowUnlitMaterial;
            shaftMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Arrowhead (cone approximation via flattened mesh).
            arrowHeadObj = new GameObject("ArrowHead");
            var mf = arrowHeadObj.AddComponent<MeshFilter>();
            var mr = arrowHeadObj.AddComponent<MeshRenderer>();
            mr.sharedMaterial = arrowUnlitMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mf.mesh = CreateArrowHeadMesh();
        }

        Vector3 dir = toWorld - fromWorld;
        float length = dir.magnitude;
        Quaternion rot = Quaternion.LookRotation(dir);
        float headLen = 0.15f;
        float shaftLen = length - headLen;

        // Shaft: from start to just before the arrowhead.
        Vector3 shaftMid = fromWorld + dir.normalized * (shaftLen * 0.5f) + Vector3.up * 0.15f;
        moveArrowObj.transform.position = shaftMid;
        moveArrowObj.transform.localScale = new Vector3(0.05f, 0.02f, Mathf.Max(shaftLen, 0.01f));
        moveArrowObj.transform.rotation = rot;
        moveArrowObj.SetActive(true);

        // Arrowhead: at the target end.
        Vector3 headPos = toWorld - dir.normalized * (headLen * 0.3f) + Vector3.up * 0.15f;
        arrowHeadObj.transform.position = headPos;
        arrowHeadObj.transform.rotation = rot;
        arrowHeadObj.transform.localScale = new Vector3(0.15f, 0.02f, headLen);
        arrowHeadObj.SetActive(true);

        Color arrowColor = team == Team.Robot ? new Color(0.3f, 0.55f, 1f) : new Color(0.3f, 0.9f, 0.3f);
        arrowMpb.SetColor("_BaseColor", arrowColor);
        moveArrowObj.GetComponent<MeshRenderer>().SetPropertyBlock(arrowMpb);
        arrowHeadObj.GetComponent<MeshRenderer>().SetPropertyBlock(arrowMpb);
    }

    private void FlashTile(HexCoord coord, Color flashColor)
    {
        // Unflash previous.
        if (flashedTile != null)
        {
            flashedTile.GetComponent<HexVisuals>()?.UpdateColor();
            flashedTile = null;
        }

        var tile = grid.GetTile(coord);
        if (tile == null) return;

        flashedTile = tile;
        flashedTileColor = flashColor;
        var meshGen = tile.GetComponent<HexMeshGenerator>();
        if (meshGen != null)
            meshGen.SetColor(flashColor);
    }

    private void FlashSecondaryTile(HexCoord coord, Color flashColor)
    {
        if (flashedSecondaryTile != null)
        {
            flashedSecondaryTile.GetComponent<HexVisuals>()?.UpdateColor();
            flashedSecondaryTile = null;
        }

        var tile = grid.GetTile(coord);
        if (tile == null) return;

        flashedSecondaryTile = tile;
        flashedSecondaryColor = flashColor;
        var meshGen = tile.GetComponent<HexMeshGenerator>();
        if (meshGen != null)
            meshGen.SetColor(flashColor);
    }

    private static void EnsureArrowMaterial()
    {
        if (arrowUnlitMaterial != null) return;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        arrowUnlitMaterial = new Material(shader);
        arrowUnlitMaterial.SetColor("_BaseColor", Color.white);
    }

    private static Mesh CreateArrowHeadMesh()
    {
        // Triangle pointing forward (+Z), flat on Y.
        var verts = new Vector3[]
        {
            new Vector3(0, 0, 1),     // tip
            new Vector3(-1, 0, 0),    // left base
            new Vector3(1, 0, 0),     // right base
        };
        var tris = new int[] { 0, 2, 1, 0, 1, 2 }; // double-sided
        var mesh = new Mesh { name = "ArrowHead" };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }

    /// <summary>Clear all replay visual indicators (called at start of next turn).</summary>
    private void ClearReplayVisuals()
    {
        if (moveArrowObj != null)
            moveArrowObj.SetActive(false);
        if (arrowHeadObj != null)
            arrowHeadObj.SetActive(false);

        if (flashedTile != null)
        {
            flashedTile.GetComponent<HexVisuals>()?.UpdateColor();
            flashedTile = null;
        }
        if (flashedSecondaryTile != null)
        {
            flashedSecondaryTile.GetComponent<HexVisuals>()?.UpdateColor();
            flashedSecondaryTile = null;
        }
    }

    /// <summary>
    /// Teleport a dead unit to its team's base hex (first free slot).
    /// Mirrors GameManager.TeleportDeadToBase() logic.
    /// </summary>
    private void TeleportDeadUnitToBase(UnitData unit)
    {
        if (unit.isAlive || grid == null) return;

        // Already on own base hex? Skip.
        var currentTile = grid.GetTile(unit.currentHex);
        if (currentTile != null && currentTile.isBase && currentTile.baseTeam == unit.team)
            return;

        var baseTiles = grid.GetBaseTiles(unit.team);
        foreach (var bt in baseTiles)
        {
            if (!IsHexOccupiedByOther(bt.coord, unit))
            {
                unit.currentHex = bt.coord;
                if (movementMap.TryGetValue(unit.gameObject.name, out var mov))
                    mov.PlaceAt(bt.coord);
                return;
            }
        }
    }

    private bool IsHexOccupiedByOther(HexCoord coord, UnitData exclude)
    {
        foreach (var u in unitFactory.AllUnits)
        {
            if (u == exclude) continue;
            if (u.currentHex == coord && u.gameObject.activeInHierarchy)
                return true;
        }
        return false;
    }

    private static UnitAction ParseAction(string action)
    {
        return action switch
        {
            "Move" => UnitAction.Move,
            "Attack" => UnitAction.Attack,
            "Idle" => UnitAction.Idle,
            "BuildWall" => UnitAction.BuildWall,
            "PlaceSlime" => UnitAction.PlaceSlime,
            "DestroyWall" => UnitAction.DestroyWall,
            "Capture" => UnitAction.Capture,
            "Dead" => UnitAction.Dead,
            _ => UnitAction.Idle,
        };
    }

    // ── Setup helpers ───────────────────────────────────────────────────

    private void BuildUnitMap()
    {
        unitMap.Clear();
        movementMap.Clear();

        foreach (var unit in unitFactory.AllUnits)
        {
            string name = unit.gameObject.name;
            unitMap[name] = unit;
            var movement = unit.GetComponent<HexMovement>();
            if (movement != null)
                movementMap[name] = movement;
        }
    }

    private void DisableAllAgents()
    {
        foreach (var unit in unitFactory.AllUnits)
        {
            var agent = unit.GetComponent<HexAgent>();
            if (agent != null) agent.enabled = false;
            var dr = unit.GetComponent<DecisionRequester>();
            if (dr != null) dr.enabled = false;
            unit.isMyTurn = false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void CleanupStaticMaterials()
    {
        if (arrowUnlitMaterial != null)
        {
            Object.DestroyImmediate(arrowUnlitMaterial);
            arrowUnlitMaterial = null;
        }
    }

    public static Material[] GetStaticMaterials()
    {
        var mats = new[] { arrowUnlitMaterial };
        arrowUnlitMaterial = null;
        return mats;
    }
}
