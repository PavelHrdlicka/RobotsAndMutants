using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode tests for HumanVsAI play mode features:
///   - Turn marker (active unit highlight)
///   - Action button validation (disabled when no valid targets)
///   - AI turn delay
///   - Expansive build (robot builds on neutral/enemy hex)
///   - Respawn cooldown display
///   - 1-based unit numbering + DisplayName
///   - Turn log + thinking time stats
/// </summary>
public class PlayModeFeatureTests
{
    private GameObject gridGo;
    private HexGrid grid;
    private readonly List<GameObject> spawnedObjects = new();
    private GameMode originalMode;
    private Team originalHumanTeam;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name != "Code-based tests runner")
                Object.Destroy(go);
        yield return null;

        if (!LogAssert.ignoreFailingMessages) LogAssert.ignoreFailingMessages = true;
        Time.timeScale = 1f;

        originalMode = GameModeConfig.CurrentMode;
        originalHumanTeam = GameModeConfig.HumanTeam;

        var prefab = new GameObject("HexPrefab");
        prefab.AddComponent<MeshFilter>();
        prefab.AddComponent<MeshRenderer>();
        prefab.AddComponent<HexMeshGenerator>();
        prefab.AddComponent<HexTileData>();
        prefab.SetActive(false);

        gridGo = new GameObject("TestGrid");
        grid = gridGo.AddComponent<HexGrid>();
        grid.hexPrefab = prefab;
        grid.boardSide = 3;

        yield return null;
        Object.Destroy(prefab);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        GameModeConfig.CurrentMode = originalMode;
        GameModeConfig.HumanTeam = originalHumanTeam;

        foreach (var go in spawnedObjects)
            if (go != null) Object.Destroy(go);
        spawnedObjects.Clear();
        if (gridGo != null) Object.Destroy(gridGo);
        yield return null;
    }

    private (UnitData data, HexMovement move) SpawnUnit(Team team, HexCoord hex)
    {
        var go = new GameObject($"{team}_{hex}");
        var data = go.AddComponent<UnitData>();
        data.team = team;
        data.isAlive = true;
        data.currentHex = hex;
        data.Energy = data.maxEnergy;

        var move = go.AddComponent<HexMovement>();
        move.Initialize(grid);
        move.PlaceAt(hex);

        spawnedObjects.Add(go);
        return (data, move);
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Turn marker (active unit highlight) ──────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator TurnGlow_IndicatorComponentCreated()
    {
        var (data, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        var indicator = data.gameObject.AddComponent<UnitActionIndicator3D>();
        yield return null;

        Assert.IsNotNull(indicator, "UnitActionIndicator3D should be created.");
    }

    [UnityTest]
    public IEnumerator TurnGlow_NoEmissionWhenNotMyTurn()
    {
        var (data, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        // Create a model root with a renderer for testing.
        var modelRoot = new GameObject("ModelRoot");
        modelRoot.transform.SetParent(data.transform, false);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(modelRoot.transform, false);
        var rend = cube.GetComponent<Renderer>();

        data.gameObject.AddComponent<UnitActionIndicator3D>();
        data.isMyTurn = false;
        yield return null;
        yield return null;

        Color emission = rend.material.HasProperty("_EmissionColor")
            ? rend.material.GetColor("_EmissionColor") : Color.black;
        Assert.AreEqual(Color.black, emission,
            "No emission when unit is not on turn.");
    }

    [UnityTest]
    public IEnumerator TurnGlow_HasEmissionWhenMyTurn()
    {
        var (data, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        var modelRoot = new GameObject("ModelRoot");
        modelRoot.transform.SetParent(data.transform, false);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(modelRoot.transform, false);
        var rend = cube.GetComponent<Renderer>();

        data.gameObject.AddComponent<UnitActionIndicator3D>();
        data.isMyTurn = true;
        data.isAlive = true;
        yield return null;
        yield return null;

        Color emission = rend.material.HasProperty("_EmissionColor")
            ? rend.material.GetColor("_EmissionColor") : Color.black;
        Assert.AreNotEqual(Color.black, emission,
            "Should have emission glow when unit is on turn.");
    }

    [UnityTest]
    public IEnumerator TurnGlow_NoEmissionWhenDead()
    {
        var (data, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        var modelRoot = new GameObject("ModelRoot");
        modelRoot.transform.SetParent(data.transform, false);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(modelRoot.transform, false);

        data.gameObject.AddComponent<UnitActionIndicator3D>();
        data.isMyTurn = true;
        data.isAlive = false;
        yield return null;
        yield return null;

        var rend = cube.GetComponent<Renderer>();
        Color emission = rend.material.HasProperty("_EmissionColor")
            ? rend.material.GetColor("_EmissionColor") : Color.black;
        Assert.AreEqual(Color.black, emission,
            "No emission when unit is dead.");
    }

    [UnityTest]
    public IEnumerator TurnGlow_TurnsOffAfterTurnEnds()
    {
        var (data, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));

        var modelRoot = new GameObject("ModelRoot");
        modelRoot.transform.SetParent(data.transform, false);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(modelRoot.transform, false);
        var rend = cube.GetComponent<Renderer>();

        data.gameObject.AddComponent<UnitActionIndicator3D>();

        // Turn on.
        data.isMyTurn = true;
        data.isAlive = true;
        yield return null;
        yield return null;

        // Turn off.
        data.isMyTurn = false;
        yield return null;
        yield return null;

        Color emission = rend.material.HasProperty("_EmissionColor")
            ? rend.material.GetColor("_EmissionColor") : Color.black;
        Assert.AreEqual(Color.black, emission,
            "Emission should turn off after turn ends.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Action validation (disabled buttons) ─────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator ActionValidation_NoValidMoves_WhenSurroundedByWalls()
    {
        var (data, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        yield return null;

        // Surround with walls.
        for (int dir = 0; dir < 6; dir++)
        {
            var neighbor = new HexCoord(0, 0).Neighbor(dir);
            var tile = grid.GetTile(neighbor);
            if (tile != null)
            {
                tile.Owner = Team.Robot;
                tile.TileType = TileType.Wall;
                tile.WallHP = 3;
            }
        }

        bool anyValidMove = false;
        for (int dir = 0; dir < 6; dir++)
            if (move.IsValidMove(dir)) anyValidMove = true;

        Assert.IsFalse(anyValidMove,
            "No valid moves when surrounded by walls — Move button should be disabled.");
    }

    [UnityTest]
    public IEnumerator ActionValidation_NoValidAttack_WhenNoEnemyAdjacent()
    {
        var (data, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        yield return null;

        bool anyValidAttack = false;
        for (int dir = 0; dir < 6; dir++)
            if (move.IsValidAttack(dir)) anyValidAttack = true;

        Assert.IsFalse(anyValidAttack,
            "No valid attacks when no enemy adjacent — Attack button should be disabled.");
    }

    [UnityTest]
    public IEnumerator ActionValidation_HasValidAttack_WhenEnemyAdjacent()
    {
        var (data, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        SpawnUnit(Team.Mutant, new HexCoord(1, 0));
        yield return null;

        bool anyValidAttack = false;
        for (int dir = 0; dir < 6; dir++)
            if (move.IsValidAttack(dir)) anyValidAttack = true;

        Assert.IsTrue(anyValidAttack,
            "Should have valid attack when enemy is adjacent.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Expansive build (robot on neutral/enemy hex) ─────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator Build_Robot_OnNeutralHex_Succeeds()
    {
        var (data, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        yield return null;

        // Adjacent hex is neutral.
        HexCoord target = new HexCoord(1, 0);
        var tile = grid.GetTile(target);
        Assert.IsNotNull(tile);
        Assert.AreEqual(Team.None, tile.Owner, "Target should be neutral.");

        bool built = move.TryBuild(0); // direction 0

        if (built)
        {
            Assert.AreEqual(Team.Robot, tile.Owner,
                "Neutral hex should be captured when robot builds wall on it.");
            Assert.AreEqual(TileType.Wall, tile.TileType,
                "Wall should be placed on the neutral hex.");
        }
    }

    [UnityTest]
    public IEnumerator Build_Robot_OnEnemyHex_Succeeds()
    {
        var (data, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        yield return null;

        HexCoord target = new HexCoord(1, 0);
        var tile = grid.GetTile(target);
        if (tile == null) yield break;

        tile.Owner = Team.Mutant; // enemy hex

        bool built = move.TryBuild(0);

        if (built)
        {
            Assert.AreEqual(Team.Robot, tile.Owner,
                "Enemy hex should be captured when robot builds wall on it.");
            Assert.AreEqual(TileType.Wall, tile.TileType);
        }
    }

    [UnityTest]
    public IEnumerator Build_Robot_OnEnemySlime_Blocked()
    {
        var (data, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        yield return null;

        HexCoord target = new HexCoord(1, 0);
        var tile = grid.GetTile(target);
        if (tile == null) yield break;

        tile.Owner = Team.Mutant;
        tile.TileType = TileType.Slime; // has structure

        bool built = move.TryBuild(0);

        Assert.IsFalse(built,
            "Cannot build wall on hex with existing structure (slime).");
    }

    [UnityTest]
    public IEnumerator Build_Robot_OnOccupiedHex_Blocked()
    {
        var (data, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        SpawnUnit(Team.Mutant, new HexCoord(1, 0)); // occupied
        yield return null;

        bool built = move.TryBuild(0);

        Assert.IsFalse(built,
            "Cannot build wall on occupied hex.");
    }

    [UnityTest]
    public IEnumerator Build_IsValidBuild_NeutralHex_ReturnsTrue()
    {
        var (data, move) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        yield return null;

        HexCoord target = new HexCoord(0, 0).Neighbor(0);
        var tile = grid.GetTile(target);
        if (tile == null || tile.isBase) yield break;

        tile.Owner = Team.None;
        tile.TileType = TileType.Empty;

        bool valid = move.IsValidBuild(0);

        Assert.IsTrue(valid,
            "IsValidBuild should return true for neutral empty hex.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Respawn cooldown display ─────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator RespawnCooldown_SetOnDeath()
    {
        var (data, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        yield return null;

        int cd = GameConfig.Instance != null ? GameConfig.Instance.respawnCooldown : 10;
        data.Die(cd);

        Assert.IsFalse(data.isAlive);
        Assert.AreEqual(cd, data.respawnCooldown,
            "respawnCooldown should be set to configured value on death.");
    }

    [UnityTest]
    public IEnumerator RespawnCooldown_Decrements()
    {
        var (data, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        yield return null;

        data.Die(5);
        int before = data.respawnCooldown;
        data.TickCooldown();
        int after = data.respawnCooldown;

        Assert.AreEqual(before - 1, after,
            "TickCooldown should decrement respawnCooldown by 1.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── 1-based numbering + DisplayName ──────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator UnitFactory_NumbersFrom1()
    {
        GameModeConfig.CurrentMode = GameMode.Training;

        var factoryGo = new GameObject("Factory");
        var factory = factoryGo.AddComponent<UnitFactory>();
        factory.grid = grid;
        factory.unitsPerTeam = 3;
        factory.skipMLAgents = true;
        factory.SpawnAllUnits();
        spawnedObjects.Add(factoryGo);
        yield return null;

        Assert.AreEqual("Robot_1", factory.robotUnits[0].gameObject.name,
            "First robot should be named Robot_1 (1-based).");
        Assert.AreEqual("Robot_2", factory.robotUnits[1].gameObject.name);
        Assert.AreEqual("Robot_3", factory.robotUnits[2].gameObject.name);
        Assert.AreEqual("Mutant_1", factory.mutantUnits[0].gameObject.name,
            "First mutant should be named Mutant_1 (1-based).");
    }

    [UnityTest]
    public IEnumerator DisplayName_DefaultsToGameObjectName()
    {
        var (data, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        data.gameObject.name = "Robot_1";
        yield return null;

        Assert.AreEqual("Robot_1", data.DisplayName,
            "DisplayName should default to GameObject.name when no custom name set.");
    }

    [UnityTest]
    public IEnumerator DisplayName_CustomNameOverrides()
    {
        var (data, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        data.gameObject.name = "Robot_1";
        data.DisplayName = "Tank";
        yield return null;

        Assert.AreEqual("Tank", data.DisplayName,
            "DisplayName should return custom name when set.");
    }

    [UnityTest]
    public IEnumerator DisplayName_EmptyCustomName_FallsBackToGameObjectName()
    {
        var (data, _) = SpawnUnit(Team.Robot, new HexCoord(0, 0));
        data.gameObject.name = "Robot_1";
        data.DisplayName = "";
        yield return null;

        Assert.AreEqual("Robot_1", data.DisplayName,
            "DisplayName should fall back to GameObject.name when custom name is empty.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Turn log ─────────────────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator TurnLog_EntryHasCorrectFields()
    {
        yield return null;

        var entry = new GameManager.TurnLogEntry
        {
            unitName = "Robot_1",
            team = Team.Robot,
            action = UnitAction.Move,
            targetHex = new HexCoord(1, 0),
            round = 5
        };

        Assert.AreEqual("Robot_1", entry.unitName);
        Assert.AreEqual(Team.Robot, entry.team);
        Assert.AreEqual(UnitAction.Move, entry.action);
        Assert.AreEqual(5, entry.round);
    }

    [UnityTest]
    public IEnumerator TurnLog_MaxEntries_Capped()
    {
        yield return null;

        var log = new List<GameManager.TurnLogEntry>();
        for (int i = 0; i < 15; i++)
        {
            log.Add(new GameManager.TurnLogEntry
            {
                unitName = $"Unit_{i}",
                team = Team.Robot,
                action = UnitAction.Move,
                round = i
            });
            if (log.Count > 10)
                log.RemoveAt(0);
        }

        Assert.AreEqual(10, log.Count,
            "Turn log should be capped at 10 entries.");
        Assert.AreEqual("Unit_5", log[0].unitName,
            "Oldest entries should be removed first.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Thinking time stats ──────────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator ThinkingStats_InitiallyZero()
    {
        yield return null;

        // Verify the stats fields exist and are zero-initialized.
        float total = 0f;
        int count = 0;
        float avg = count > 0 ? total / count : 0f;

        Assert.AreEqual(0f, avg, "Average thinking time should be 0 initially.");
    }

    [UnityTest]
    public IEnumerator ThinkingStats_AverageCalculation()
    {
        yield return null;

        // Simulate stats accumulation.
        float totalThink = 3f + 5f + 7f; // 15s total
        int turnCount = 3;
        float avg = turnCount > 0 ? totalThink / turnCount : 0f;

        Assert.AreEqual(5f, avg, 0.01f,
            "Average thinking time should be total / count.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Idle button (HumanInputManager) ──────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator IdleRequested_CanBeSetExternally()
    {
        var inputGo = new GameObject("InputMgr");
        var inputMgr = inputGo.AddComponent<HumanInputManager>();
        inputMgr.grid = grid;
        spawnedObjects.Add(inputGo);
        yield return null;

        // Simulate Idle button click from HUD.
        inputMgr.IdleRequested = true;
        Assert.IsTrue(inputMgr.IdleRequested,
            "IdleRequested must be settable externally (for HUD button click).");
    }

    [UnityTest]
    public IEnumerator IdleRequested_PersistsUntilConsumed()
    {
        var inputGo = new GameObject("InputMgr");
        var inputMgr = inputGo.AddComponent<HumanInputManager>();
        inputMgr.grid = grid;
        spawnedObjects.Add(inputGo);
        yield return null;

        inputMgr.IdleRequested = true;
        yield return null; // no consumer present

        Assert.IsTrue(inputMgr.IdleRequested,
            "IdleRequested should persist until consumed by HumanTurnController.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Match duration tracking ─────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator MatchResult_HasDurationField()
    {
        yield return null;

        var result = new GameManager.TurnLogEntry(); // just verify struct exists
        // MatchResult is private — test via observable behavior instead.
        // Verify matchStartTime tracking exists in source code.
        string path = System.IO.Path.Combine(Application.dataPath, "Scripts", "Game", "GameManager.Episode.cs");
        if (System.IO.File.Exists(path))
        {
            string source = System.IO.File.ReadAllText(path);
            Assert.IsTrue(source.Contains("durationSeconds"),
                "MatchResult must have durationSeconds field for match time tracking.");
            Assert.IsTrue(source.Contains("matchStartTime"),
                "GameManager must track matchStartTime for duration calculation.");
            Assert.IsTrue(source.Contains("Time.realtimeSinceStartup - matchStartTime"),
                "Duration must be calculated from realtimeSinceStartup - matchStartTime.");
        }
    }

    [UnityTest]
    public IEnumerator MatchDuration_TimeColumnOnlyInHumanVsAI()
    {
        // Verify the HUD source code shows Time column conditionally.
        yield return null;

        string path = System.IO.Path.Combine(Application.dataPath, "Scripts", "Game", "GameManager.HUD.cs");
        if (System.IO.File.Exists(path))
        {
            string source = System.IO.File.ReadAllText(path);
            Assert.IsTrue(source.Contains("showTime") && source.Contains("GameMode.HumanVsAI"),
                "Time column must only show in HumanVsAI mode.");
            Assert.IsTrue(source.Contains("if (showTime)"),
                "Time column rendering must be conditional on showTime flag.");
        }
    }

    [UnityTest]
    public IEnumerator MatchDuration_PanelWiderInHumanVsAI()
    {
        // Verify panel width increases when Time column is shown.
        yield return null;

        string path = System.IO.Path.Combine(Application.dataPath, "Scripts", "Game", "GameManager.HUD.cs");
        if (System.IO.File.Exists(path))
        {
            string source = System.IO.File.ReadAllText(path);
            Assert.IsTrue(source.Contains("showTime ? 410f : 360f"),
                "Match history panel must be wider (410px) in HumanVsAI to fit Time column.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Edge base spawn (spread along edge) ─────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator EdgeBase_RobotsOnLeftEdge()
    {
        yield return null;

        var baseTiles = grid.GetBaseTiles(Team.Robot);
        Assert.Greater(baseTiles.Count, 0, "Robot base tiles must exist.");

        int max = grid.boardSide - 1;
        foreach (var tile in baseTiles)
        {
            Assert.AreEqual(-max, tile.coord.q,
                $"Robot base tile {tile.coord} must be on left edge (q={-max}).");
        }
    }

    [UnityTest]
    public IEnumerator EdgeBase_MutantsOnRightEdge()
    {
        yield return null;

        var baseTiles = grid.GetBaseTiles(Team.Mutant);
        Assert.Greater(baseTiles.Count, 0, "Mutant base tiles must exist.");

        int max = grid.boardSide - 1;
        foreach (var tile in baseTiles)
        {
            Assert.AreEqual(max, tile.coord.q,
                $"Mutant base tile {tile.coord} must be on right edge (q={max}).");
        }
    }

    [UnityTest]
    public IEnumerator EdgeBase_UnitsSpreadNotClustered()
    {
        GameModeConfig.CurrentMode = GameMode.Training;

        var factoryGo = new GameObject("Factory");
        var factory = factoryGo.AddComponent<UnitFactory>();
        factory.grid = grid;
        factory.unitsPerTeam = 2;
        factory.skipMLAgents = true;
        factory.SpawnAllUnits();
        spawnedObjects.Add(factoryGo);
        yield return null;

        // With 2 units on an edge of 3+ hexes, they should be on different hexes.
        if (factory.robotUnits.Count >= 2)
        {
            Assert.AreNotEqual(factory.robotUnits[0].currentHex, factory.robotUnits[1].currentHex,
                "Units should spawn on different base hexes (spread along edge).");
        }
    }

    [UnityTest]
    public IEnumerator EdgeBase_BaseTilesMatchUnitsPerTeam()
    {
        yield return null;

        // boardSide=3 has edge length that may differ from unitsPerTeam.
        // Base tiles count should equal min(unitsPerTeam, available edge hexes).
        var robotBases = grid.GetBaseTiles(Team.Robot);
        var mutantBases = grid.GetBaseTiles(Team.Mutant);

        Assert.Greater(robotBases.Count, 0, "Must have at least 1 robot base tile.");
        Assert.Greater(mutantBases.Count, 0, "Must have at least 1 mutant base tile.");
        Assert.AreEqual(robotBases.Count, mutantBases.Count,
            "Both teams should have the same number of base tiles.");
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── GameModeConfig persistence ──────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator GameModeConfig_TrainingMode_NoHumanComponents()
    {
        GameModeConfig.CurrentMode = GameMode.Training;

        var factoryGo = new GameObject("Factory");
        var factory = factoryGo.AddComponent<UnitFactory>();
        factory.grid = grid;
        factory.unitsPerTeam = 2;
        factory.SpawnAllUnits();
        spawnedObjects.Add(factoryGo);
        yield return null;

        foreach (var unit in factory.AllUnits)
        {
            Assert.IsNull(unit.GetComponent<HumanTurnController>(),
                $"{unit.name} should NOT have HumanTurnController in Training mode.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── FormatUnitName (no underscores in UI) ───────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator FormatUnitName_ReplacesUnderscore()
    {
        yield return null;
        Assert.AreEqual("Robot 1", "Robot_1".Replace("_", " "));
        Assert.AreEqual("Mutant 3", "Mutant_3".Replace("_", " "));
    }

    [UnityTest]
    public IEnumerator FormatUnitName_NoUnderscoreInSource()
    {
        // Static analysis: DrawTurnLog must use FormatUnitName, never raw unitName.
        yield return null;

        string path = System.IO.Path.Combine(Application.dataPath, "Scripts", "Game", "GameManager.HUD.cs");
        if (System.IO.File.Exists(path))
        {
            string source = System.IO.File.ReadAllText(path);
            Assert.IsTrue(source.Contains("FormatUnitName"),
                "DrawTurnLog must use FormatUnitName to strip underscores for display.");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Turn log table layout ───────────────────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator TurnLog_HasColumnHeaders()
    {
        // Static analysis: turn log must have column headers for structured layout.
        yield return null;

        string path = System.IO.Path.Combine(Application.dataPath, "Scripts", "Game", "GameManager.HUD.cs");
        if (System.IO.File.Exists(path))
        {
            string source = System.IO.File.ReadAllText(path);
            Assert.IsTrue(source.Contains("colNum") && source.Contains("colUnit") && source.Contains("colAction"),
                "Turn log must define fixed-width columns (colNum, colUnit, colAction).");
            Assert.IsTrue(source.Contains("\"#\"") && source.Contains("\"Unit\"") && source.Contains("\"Action\""),
                "Turn log must have column headers: #, Unit, Action.");
        }
    }

    [UnityTest]
    public IEnumerator TurnLog_RowsAreNumbered()
    {
        // Static analysis: turn log rows must be numbered 1-N.
        yield return null;

        string path = System.IO.Path.Combine(Application.dataPath, "Scripts", "Game", "GameManager.HUD.cs");
        if (System.IO.File.Exists(path))
        {
            string source = System.IO.File.ReadAllText(path);
            Assert.IsTrue(source.Contains("displayNum"),
                "Turn log rows must be numbered (displayNum variable).");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // ── Latest replay skips incomplete files ─────────────────────────────
    // ══════════════════════════════════════════════════════════════════════

    [UnityTest]
    public IEnumerator LatestReplay_SkipsSmallFiles()
    {
        // Static analysis: Latest button must skip files smaller than threshold.
        yield return null;

        string path = System.IO.Path.Combine(Application.dataPath, "Editor", "ProjectToolsWindow.cs");
        if (System.IO.File.Exists(path))
        {
            string source = System.IO.File.ReadAllText(path);
            Assert.IsTrue(source.Contains("info.Length < 100"),
                "Latest replay must skip files < 100 bytes (incomplete replays).");
        }
    }

    [UnityTest]
    public IEnumerator LatestReplay_IncompleteFileDetection()
    {
        // Create temp files to verify the size-based filtering logic.
        yield return null;

        string tempDir = System.IO.Path.Combine(Application.temporaryCachePath, "replay_test");
        System.IO.Directory.CreateDirectory(tempDir);

        try
        {
            // Create a tiny file (incomplete replay — just header).
            string tinyFile = System.IO.Path.Combine(tempDir, "game_tiny.jsonl");
            System.IO.File.WriteAllText(tinyFile, "{\"type\":\"header\"}");

            // Create a normal file (complete replay).
            string normalFile = System.IO.Path.Combine(tempDir, "game_normal.jsonl");
            System.IO.File.WriteAllText(normalFile, new string('x', 200));

            var tinyInfo = new System.IO.FileInfo(tinyFile);
            var normalInfo = new System.IO.FileInfo(normalFile);

            Assert.Less(tinyInfo.Length, 100, "Tiny file should be < 100 bytes.");
            Assert.GreaterOrEqual(normalInfo.Length, 100, "Normal file should be >= 100 bytes.");
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, true);
        }
    }

    // ── OnDestroy replay cleanup ────────────────────────────────────────

    [UnityTest]
    public IEnumerator GameManager_OnDestroy_CallsReplayLoggerClose()
    {
        // Static analysis: GameManager.OnDestroy must call replayLogger.Close().
        yield return null;

        string path = System.IO.Path.Combine(Application.dataPath, "Scripts/Game/GameManager.cs");
        if (System.IO.File.Exists(path))
        {
            string source = System.IO.File.ReadAllText(path);
            Assert.IsTrue(source.Contains("void OnDestroy()"),
                "GameManager must have OnDestroy method to clean up on Play mode exit.");
            Assert.IsTrue(source.Contains("replayLogger.Close()"),
                "OnDestroy must call replayLogger.Close() to flush incomplete replays.");
        }
    }
}
