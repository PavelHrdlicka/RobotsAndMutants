using NUnit.Framework;
using UnityEngine;
using UnityEditor;

/// <summary>
/// EditMode tests verifying HexGridSetup produces a valid scene
/// with all required references (prefab, grid, factory, camera).
/// Guards against "hexPrefab is not assigned" after Reset+Setup.
/// </summary>
public class HexGridSetupTests
{
    [Test]
    public void ResetThenSetup_HexPrefabExists()
    {
        HexGridSetup.Reset();
        HexGridSetup.SetupScene();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HexTile.prefab");
        Assert.IsNotNull(prefab, "HexTile.prefab must exist after Reset + Setup.");
    }

    [Test]
    public void ResetThenSetup_HexGridHasPrefabAssigned()
    {
        HexGridSetup.Reset();
        HexGridSetup.SetupScene();

        var gridGo = GameObject.Find("HexGrid");
        Assert.IsNotNull(gridGo, "HexGrid GameObject must exist after Setup.");

        var grid = gridGo.GetComponent<HexGrid>();
        Assert.IsNotNull(grid, "HexGrid component must exist.");
        Assert.IsNotNull(grid.hexPrefab, "hexPrefab must be assigned on HexGrid after Reset + Setup.");
    }

    [Test]
    public void ResetThenSetup_PrefabHasRequiredComponents()
    {
        HexGridSetup.Reset();
        HexGridSetup.SetupScene();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HexTile.prefab");
        Assert.IsNotNull(prefab);

        Assert.IsNotNull(prefab.GetComponent<HexMeshGenerator>(), "Prefab must have HexMeshGenerator.");
        Assert.IsNotNull(prefab.GetComponent<HexTileData>(), "Prefab must have HexTileData.");
        Assert.IsNotNull(prefab.GetComponent<HexVisuals>(), "Prefab must have HexVisuals.");
        Assert.IsNotNull(prefab.GetComponent<MeshRenderer>(), "Prefab must have MeshRenderer.");
    }

    [Test]
    public void ResetThenSetup_UnitFactoryHasGridAssigned()
    {
        HexGridSetup.Reset();
        HexGridSetup.SetupScene();

        var factoryGo = GameObject.Find("UnitFactory");
        Assert.IsNotNull(factoryGo, "UnitFactory must exist after Setup.");

        var factory = factoryGo.GetComponent<UnitFactory>();
        Assert.IsNotNull(factory, "UnitFactory component must exist.");
        Assert.IsNotNull(factory.grid, "UnitFactory.grid must be assigned.");
    }

    [Test]
    public void ResetThenSetup_GameManagerExists()
    {
        HexGridSetup.Reset();
        HexGridSetup.SetupScene();

        var gmGo = GameObject.Find("GameManager");
        Assert.IsNotNull(gmGo, "GameManager must exist after Setup.");
        Assert.IsNotNull(gmGo.GetComponent<GameManager>(), "GameManager component must exist.");
    }

    [Test]
    public void ResetThenSetup_MaterialExists()
    {
        HexGridSetup.Reset();
        HexGridSetup.SetupScene();

        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/HexDefault.mat");
        Assert.IsNotNull(mat, "HexDefault.mat must exist after Reset + Setup.");
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up scene objects created by tests.
        var names = new[] { "HexGrid", "UnitFactory", "GameManager" };
        foreach (var name in names)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
