using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for procedural hex mesh generation.
/// Validates geometry, winding order and normals without entering Play mode.
/// Run via: Window > General > Test Runner > EditMode
/// </summary>
public class HexMeshTests
{
    private GameObject hexGo;
    private HexMeshGenerator generator;
    private Mesh mesh;

    [SetUp]
    public void SetUp()
    {
        hexGo = new GameObject("TestHex");
        hexGo.AddComponent<MeshFilter>();
        hexGo.AddComponent<MeshRenderer>();
        generator = hexGo.AddComponent<HexMeshGenerator>();
        // Awake does not run in EditMode, so call GenerateMesh explicitly.
        generator.GenerateMesh();
        mesh = hexGo.GetComponent<MeshFilter>().sharedMesh;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(hexGo);
    }

    [Test]
    public void Mesh_IsGenerated()
    {
        Assert.IsNotNull(mesh, "Mesh should be generated after calling GenerateMesh.");
    }

    [Test]
    public void Mesh_HasCorrectVertexCount()
    {
        // 1 center + 6 corners = 7 vertices.
        Assert.AreEqual(7, mesh.vertexCount, "Hex mesh should have 7 vertices (center + 6 corners).");
    }

    [Test]
    public void Mesh_HasCorrectTriangleCount()
    {
        // 6 triangles × 3 indices = 18.
        Assert.AreEqual(18, mesh.triangles.Length, "Hex mesh should have 18 triangle indices (6 triangles).");
    }

    [Test]
    public void Mesh_NormalsPointUp()
    {
        Vector3[] normals = mesh.normals;
        Assert.IsTrue(normals.Length > 0, "Mesh should have normals.");

        for (int i = 0; i < normals.Length; i++)
        {
            float dot = Vector3.Dot(normals[i], Vector3.up);
            Assert.Greater(dot, 0.9f,
                $"Normal at vertex {i} should point up (dot with Vector3.up = {dot}).");
        }
    }

    [Test]
    public void Mesh_WindingOrderIsClockwiseFromAbove()
    {
        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 a = verts[tris[i]];
            Vector3 b = verts[tris[i + 1]];
            Vector3 c = verts[tris[i + 2]];

            Vector3 cross = Vector3.Cross(b - a, c - a);
            Assert.Greater(cross.y, 0f,
                $"Triangle {i / 3} winding should produce upward-facing normal (cross.y = {cross.y}).");
        }
    }

    [Test]
    public void Mesh_BoundsAreNotZero()
    {
        Vector3 size = mesh.bounds.size;
        Assert.Greater(size.x, 0f, "Mesh bounds width should be > 0.");
        Assert.Greater(size.z, 0f, "Mesh bounds depth should be > 0.");
    }

    [Test]
    public void Mesh_VerticesLieInXZPlane()
    {
        foreach (Vector3 v in mesh.vertices)
        {
            Assert.AreEqual(0f, v.y, 0.001f,
                $"All hex vertices should have Y=0 (flat on ground), got {v.y}.");
        }
    }
}
