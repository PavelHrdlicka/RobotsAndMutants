using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests that ensure static GPU resources (Material, Texture) are properly cleaned up
/// between Play sessions to prevent D3D11 Resource ID overflow.
///
/// KEY RULE: Every class with a "static Material" or "static Texture" field MUST:
///   1. Expose a public static GetStaticMaterials() method
///   2. Be registered in StaticResourceCleanup.Cleanup()
///
/// If you add a new static Material/Texture and these tests fail, add GetStaticMaterials()
/// to your class and register it in StaticResourceCleanup.
/// </summary>
public class StaticResourceCleanupTests
{
    private static readonly string ScriptsDir = Path.Combine(Application.dataPath, "Scripts");

    // ── Core: StaticResourceCleanup exists and has the right structure ───

    [Test]
    public void StaticResourceCleanup_Exists()
    {
        string path = Path.Combine(ScriptsDir, "Agents/StaticResourceCleanup.cs");
        Assert.IsTrue(File.Exists(path),
            "StaticResourceCleanup.cs must exist to clean up GPU resources between Play sessions.");
    }

    [Test]
    public void StaticResourceCleanup_HasSubsystemRegistrationAttribute()
    {
        string source = File.ReadAllText(Path.Combine(ScriptsDir, "Agents/StaticResourceCleanup.cs"));
        Assert.IsTrue(source.Contains("RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)"),
            "Cleanup must run at SubsystemRegistration to destroy materials before Play mode.");
    }

    [Test]
    public void StaticResourceCleanup_CallsDestroyImmediate()
    {
        string source = File.ReadAllText(Path.Combine(ScriptsDir, "Agents/StaticResourceCleanup.cs"));
        Assert.IsTrue(source.Contains("DestroyImmediate"),
            "Cleanup must use DestroyImmediate (not Destroy) for editor-time material cleanup.");
    }

    // ── Every class with static Material must have GetStaticMaterials() ──

    [Test]
    public void AllStaticMaterials_HaveGetStaticMaterials()
    {
        var filesWithStaticMat = new List<string>();
        var filesWithoutCleanup = new List<string>();

        foreach (string file in Directory.GetFiles(ScriptsDir, "*.cs", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(file);
            // Skip the cleanup class itself and test files.
            if (fileName == "StaticResourceCleanup.cs") continue;

            string source = File.ReadAllText(file);

            // Detect static Material/Texture2D FIELDS (not return types).
            // Fields: "static Material fieldName" — followed by identifier, comma, or semicolon.
            // Methods: "static Material MethodName(" — followed by parenthesis.
            bool hasStaticMaterial = Regex.IsMatch(source,
                @"\bstatic\s+Material\s+\w+\s*[;,=]");
            bool hasStaticTexture = Regex.IsMatch(source,
                @"\bstatic\s+Texture2D\s+\w+\s*[;,=]");

            if (hasStaticMaterial || hasStaticTexture)
            {
                filesWithStaticMat.Add(fileName);

                // Must have GetStaticMaterials method.
                if (!source.Contains("GetStaticMaterials"))
                    filesWithoutCleanup.Add(fileName);
            }
        }

        Assert.IsEmpty(filesWithoutCleanup,
            $"These files have static Material/Texture fields but no GetStaticMaterials() method: " +
            $"{string.Join(", ", filesWithoutCleanup)}. " +
            "Add 'public static Material[] GetStaticMaterials()' that returns and nulls all static materials.");
    }

    // ── Every GetStaticMaterials() class must be registered in Cleanup ───

    [Test]
    public void AllGetStaticMaterials_RegisteredInCleanup()
    {
        string cleanupSource = File.ReadAllText(
            Path.Combine(ScriptsDir, "Agents/StaticResourceCleanup.cs"));

        var unregistered = new List<string>();

        foreach (string file in Directory.GetFiles(ScriptsDir, "*.cs", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(file);
            if (fileName == "StaticResourceCleanup.cs") continue;

            string source = File.ReadAllText(file);
            if (!source.Contains("GetStaticMaterials")) continue;

            // Extract class name.
            var classMatch = Regex.Match(source, @"\bclass\s+(\w+)");
            if (!classMatch.Success) continue;

            string className = classMatch.Groups[1].Value;

            // Must appear in cleanup source as "ClassName.GetStaticMaterials()".
            string expected = $"{className}.GetStaticMaterials()";
            if (!cleanupSource.Contains(expected))
                unregistered.Add(className);
        }

        Assert.IsEmpty(unregistered,
            $"These classes have GetStaticMaterials() but are NOT registered in StaticResourceCleanup: " +
            $"{string.Join(", ", unregistered)}. " +
            "Add them to the Cleanup() method.");
    }

    // ── GetStaticMaterials must null out the fields (not just return them) ─

    [Test]
    public void GetStaticMaterials_NullsOutFields()
    {
        var filesWithoutNull = new List<string>();

        foreach (string file in Directory.GetFiles(ScriptsDir, "*.cs", SearchOption.AllDirectories))
        {
            string fileName = Path.GetFileName(file);
            if (fileName == "StaticResourceCleanup.cs") continue;

            string source = File.ReadAllText(file);
            if (!source.Contains("GetStaticMaterials")) continue;

            // Extract the GetStaticMaterials method body (with brace depth counting).
            int methodStart = source.IndexOf("GetStaticMaterials");
            if (methodStart < 0) continue;

            int bodyStart = source.IndexOf('{', methodStart);
            if (bodyStart < 0) continue;

            int depth = 1;
            int pos = bodyStart + 1;
            while (pos < source.Length && depth > 0)
            {
                if (source[pos] == '{') depth++;
                else if (source[pos] == '}') depth--;
                pos++;
            }

            string methodBody = source.Substring(bodyStart, pos - bodyStart);

            // Must contain "= null" to clear the static reference.
            if (!methodBody.Contains("= null"))
                filesWithoutNull.Add(fileName);
        }

        Assert.IsEmpty(filesWithoutNull,
            $"GetStaticMaterials() in these files does not null out static fields: " +
            $"{string.Join(", ", filesWithoutNull)}. " +
            "After returning materials, set each static field to null.");
    }

    // ── No new Material() in Update/LateUpdate/OnGUI (hot path) ─────────

    [Test]
    public void NoNewMaterialInHotPaths()
    {
        var violations = new List<string>();

        foreach (string file in Directory.GetFiles(ScriptsDir, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            // Find all Update/LateUpdate/OnGUI/FixedUpdate method bodies.
            var hotMethods = Regex.Matches(source,
                @"(?:void\s+(?:Update|LateUpdate|FixedUpdate|OnGUI)\s*\(\s*\))\s*\{");

            foreach (Match m in hotMethods)
            {
                int braceStart = source.IndexOf('{', m.Index);
                if (braceStart < 0) continue;

                // Find matching closing brace (simple depth count).
                int depth = 1;
                int pos = braceStart + 1;
                while (pos < source.Length && depth > 0)
                {
                    if (source[pos] == '{') depth++;
                    else if (source[pos] == '}') depth--;
                    pos++;
                }

                string body = source.Substring(braceStart, pos - braceStart);
                if (body.Contains("new Material("))
                    violations.Add($"{fileName} ({m.Value.Trim()})");
            }
        }

        Assert.IsEmpty(violations,
            $"new Material() found in hot-path methods (causes GPU resource leak): " +
            $"{string.Join(", ", violations)}. " +
            "Use MaterialPropertyBlock or pre-allocate materials in Awake/Start.");
    }

    // ── Specific class tests: verify known classes return correct count ──

    [Test]
    public void UnitActionIndicator3D_Returns6Materials()
    {
        // After cleanup, GetStaticMaterials should return an array and null the fields.
        var mats = UnitActionIndicator3D.GetStaticMaterials();
        Assert.AreEqual(6, mats.Length,
            "UnitActionIndicator3D has 6 static materials (red, blue, yellow, grey, cyan, orange).");
    }

    [Test]
    public void UnitHealthBar3D_Returns1Material()
    {
        var mats = UnitHealthBar3D.GetStaticMaterials();
        Assert.AreEqual(1, mats.Length,
            "UnitHealthBar3D has 1 static material (barMaterial).");
    }

    [Test]
    public void RobotModelBuilder_Returns1Material()
    {
        var mats = RobotModelBuilder.GetStaticMaterials();
        Assert.AreEqual(1, mats.Length,
            "RobotModelBuilder has 1 static material (sharedMaterial).");
    }

    [Test]
    public void MutantModelBuilder_Returns1Material()
    {
        var mats = MutantModelBuilder.GetStaticMaterials();
        Assert.AreEqual(1, mats.Length,
            "MutantModelBuilder has 1 static material (sharedMaterial).");
    }

    [Test]
    public void HexVisuals_Returns1Material()
    {
        var mats = HexVisuals.GetStaticMaterials();
        Assert.AreEqual(1, mats.Length,
            "HexVisuals has 1 static material (slimeOverlayMaterial).");
    }
}
