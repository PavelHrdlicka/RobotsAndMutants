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

            // Skip classes that handle their own cleanup via RuntimeInitializeOnLoadMethod.
            if (source.Contains("RuntimeInitializeOnLoadMethod") && source.Contains("CleanupStaticMaterials"))
                continue;

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
    public void RobotModelBuilder_Returns3Materials()
    {
        var mats = RobotModelBuilder.GetStaticMaterials();
        Assert.AreEqual(3, mats.Length,
            "RobotModelBuilder has 3 static materials (shared, hammer, eye).");
    }

    [Test]
    public void MutantModelBuilder_Returns2Materials()
    {
        var mats = MutantModelBuilder.GetStaticMaterials();
        Assert.AreEqual(2, mats.Length,
            "MutantModelBuilder has 2 static materials (shared, eye).");
    }

    [Test]
    public void HexVisuals_Returns1Material()
    {
        var mats = HexVisuals.GetStaticMaterials();
        Assert.AreEqual(1, mats.Length,
            "HexVisuals has 1 static material (slimeOverlayMaterial).");
    }

    // ── No per-unit .material = new Material() in model builders ────────

    [Test]
    public void ModelBuilders_NeverCreatePerUnitMaterials()
    {
        // Model builders must use static cached materials + .sharedMaterial,
        // never .material = new Material() per spawn (causes GPU resource leak).
        var violations = new List<string>();
        string[] modelFiles =
        {
            "Agents/RobotModelBuilder.cs",
            "Agents/MutantModelBuilder.cs"
        };

        foreach (string relPath in modelFiles)
        {
            string path = Path.Combine(ScriptsDir, relPath);
            if (!File.Exists(path)) continue;

            string source = File.ReadAllText(path);
            string fileName = Path.GetFileName(path);

            // Find Build() method body.
            var buildMatch = Regex.Match(source, @"void\s+Build\s*\(\s*\)\s*\{");
            if (!buildMatch.Success) continue;

            int braceStart = source.IndexOf('{', buildMatch.Index);
            int depth = 1;
            int pos = braceStart + 1;
            while (pos < source.Length && depth > 0)
            {
                if (source[pos] == '{') depth++;
                else if (source[pos] == '}') depth--;
                pos++;
            }
            string buildBody = source.Substring(braceStart, pos - braceStart);

            // Check for unguarded "new Material(" — allowed inside "if (x == null)" cache guards.
            var lines = buildBody.Split('\n');
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li].Trim();
                if (!line.Contains("new Material(")) continue;

                // Check if this line or the preceding line has a null guard.
                bool hasGuard = line.Contains("== null");
                if (li > 0)
                    hasGuard |= lines[li - 1].Trim().Contains("== null");
                if (li > 1)
                    hasGuard |= lines[li - 2].Trim().Contains("== null");

                if (!hasGuard)
                    violations.Add(fileName + ": " + line);
            }
        }

        Assert.IsEmpty(violations,
            $"new Material() found inside Build() in: {string.Join(", ", violations)}. " +
            "Model materials must be static cached and assigned via .sharedMaterial.");
    }

    [Test]
    public void ModelBuilders_UseSharedMaterialNotInstanceMaterial()
    {
        // In Build() methods, renderers must use .sharedMaterial, not .material
        // (which creates a copy and leaks GPU resources).
        var violations = new List<string>();
        string[] modelFiles =
        {
            "Agents/RobotModelBuilder.cs",
            "Agents/MutantModelBuilder.cs"
        };

        foreach (string relPath in modelFiles)
        {
            string path = Path.Combine(ScriptsDir, relPath);
            if (!File.Exists(path)) continue;

            string source = File.ReadAllText(path);
            string fileName = Path.GetFileName(path);

            // Find Build() method body.
            var buildMatch = Regex.Match(source, @"void\s+Build\s*\(\s*\)\s*\{");
            if (!buildMatch.Success) continue;

            int braceStart = source.IndexOf('{', buildMatch.Index);
            int depth = 1;
            int pos = braceStart + 1;
            while (pos < source.Length && depth > 0)
            {
                if (source[pos] == '{') depth++;
                else if (source[pos] == '}') depth--;
                pos++;
            }
            string buildBody = source.Substring(braceStart, pos - braceStart);

            // Check for ".material =" (instance copy) — must be ".sharedMaterial =" instead.
            // Exclude lines with "sharedMaterial" to avoid false positives.
            var lines = buildBody.Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Contains(".material =") && !trimmed.Contains(".sharedMaterial =")
                    && !trimmed.Contains("//"))
                {
                    violations.Add($"{fileName}: {trimmed}");
                }
            }
        }

        Assert.IsEmpty(violations,
            $".material = (instance copy) found in Build(): {string.Join("; ", violations)}. " +
            "Use .sharedMaterial = to share materials and prevent GPU resource leaks.");
    }

    // ── No EyeMaterial()/similar called directly in Build (must be cached) ─

    [Test]
    public void ModelBuilders_EyeMaterialIsCached()
    {
        string[] files =
        {
            Path.Combine(ScriptsDir, "Agents/RobotModelBuilder.cs"),
            Path.Combine(ScriptsDir, "Agents/MutantModelBuilder.cs")
        };

        foreach (string path in files)
        {
            if (!File.Exists(path)) continue;
            string source = File.ReadAllText(path);
            string fileName = Path.GetFileName(path);

            // EyeMaterial() must be called via "if (eyeMaterial == null) eyeMaterial = EyeMaterial()"
            // not directly as ".material = EyeMaterial()".
            Assert.IsTrue(source.Contains("static Material eyeMaterial"),
                $"{fileName} must have static eyeMaterial field for caching.");
            Assert.IsFalse(
                Regex.IsMatch(source, @"\.material\s*=\s*EyeMaterial\(\)"),
                $"{fileName}: EyeMaterial() must not be assigned directly via .material — cache it in static field.");
        }
    }
}
