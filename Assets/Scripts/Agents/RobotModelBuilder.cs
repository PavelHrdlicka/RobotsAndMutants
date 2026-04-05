using System.Collections;
using UnityEngine;

/// <summary>
/// Builds a blocky robot from Unity primitives.
/// All parts live under a ModelRoot child so HexMovement controls the root
/// while animations (walk, hammer swing) only affect ModelRoot children.
/// </summary>
public class RobotModelBuilder : MonoBehaviour
{
    [HideInInspector] public Transform modelRoot;
    [HideInInspector] public Transform torso;
    [HideInInspector] public Transform head;
    [HideInInspector] public Transform leftArm;
    [HideInInspector] public Transform rightArm;
    [HideInInspector] public Transform hammer;
    [HideInInspector] public Transform leftLeg;
    [HideInInspector] public Transform rightLeg;

    private static Material sharedMaterial;

    // Walk cycle state.
    private HexMovement movement;
    private float walkPhase;

    public void Build()
    {
        EnsureMaterial();

        modelRoot = new GameObject("ModelRoot").transform;
        modelRoot.SetParent(transform, false);

        torso    = Part("Torso",    PrimitiveType.Cube,     new Vector3(0f, 0.15f, 0f),     new Vector3(0.14f, 0.16f, 0.10f));
        head     = Part("Head",     PrimitiveType.Cube,     new Vector3(0f, 0.28f, 0f),     new Vector3(0.10f, 0.10f, 0.10f));
        leftArm  = Part("ArmL",    PrimitiveType.Cylinder, new Vector3(-0.10f, 0.15f, 0f), new Vector3(0.03f, 0.07f, 0.03f));
        rightArm = Part("ArmR",    PrimitiveType.Cylinder, new Vector3( 0.10f, 0.15f, 0f), new Vector3(0.03f, 0.07f, 0.03f));
        leftLeg  = Part("LegL",    PrimitiveType.Cube,     new Vector3(-0.04f, 0.04f, 0f), new Vector3(0.04f, 0.08f, 0.04f));
        rightLeg = Part("LegR",    PrimitiveType.Cube,     new Vector3( 0.04f, 0.04f, 0f), new Vector3(0.04f, 0.08f, 0.04f));

        // Hammer as child of right arm.
        hammer = Part("Hammer", PrimitiveType.Cube, Vector3.zero, new Vector3(0.06f, 0.04f, 0.04f));
        hammer.SetParent(rightArm, false);
        hammer.localPosition = new Vector3(0f, -0.07f, 0f);

        // Give hammer a darker metallic tint.
        var hammerMat = new Material(sharedMaterial);
        hammerMat.SetColor("_BaseColor", new Color(0.35f, 0.35f, 0.4f));
        hammerMat.SetFloat("_Metallic", 0.9f);
        hammer.GetComponent<Renderer>().material = hammerMat;

        // Two small "eyes" on the head.
        var eyeL = Part("EyeL", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.02f, 0.02f, 0.02f));
        eyeL.SetParent(head, false);
        eyeL.localPosition = new Vector3(-0.025f, 0.01f, 0.05f);
        eyeL.GetComponent<Renderer>().material = EyeMaterial();

        var eyeR = Part("EyeR", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.02f, 0.02f, 0.02f));
        eyeR.SetParent(head, false);
        eyeR.localPosition = new Vector3( 0.025f, 0.01f, 0.05f);
        eyeR.GetComponent<Renderer>().material = EyeMaterial();

        // Scale up entire model for better visibility.
        modelRoot.localScale = Vector3.one * 1.8f;

        movement = GetComponent<HexMovement>();
    }

    private void Update()
    {
        if (movement == null || modelRoot == null) return;

        // Simple walk cycle when moving (queue has hops).
        if (movement.QueueDepth > 0)
        {
            walkPhase += Time.deltaTime * 12f;
            float swing = Mathf.Sin(walkPhase) * 15f;
            if (leftLeg  != null) leftLeg.localRotation  = Quaternion.Euler(swing, 0, 0);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            if (leftArm  != null) leftArm.localRotation  = Quaternion.Euler(-swing * 0.5f, 0, 0);
        }
        else
        {
            walkPhase = 0f;
            if (leftLeg  != null) leftLeg.localRotation  = Quaternion.identity;
            if (rightLeg != null) rightLeg.localRotation = Quaternion.identity;
            if (leftArm  != null) leftArm.localRotation  = Quaternion.identity;
        }
    }

    /// <summary>Play hammer swing toward target direction.</summary>
    public void SwingHammer(Vector3 worldDir, float duration = 0.35f)
    {
        StartCoroutine(HammerSwingCo(worldDir, duration));
    }

    private IEnumerator HammerSwingCo(Vector3 worldDir, float duration)
    {
        if (rightArm == null) yield break;

        // Face the model root toward the target.
        if (worldDir.sqrMagnitude > 0.001f)
        {
            worldDir.y = 0;
            modelRoot.rotation = Quaternion.LookRotation(worldDir, Vector3.up);
        }

        Quaternion start = rightArm.localRotation;
        float half = duration * 0.35f;

        // Swing forward.
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            rightArm.localRotation = start * Quaternion.Euler(90f * (t / half), 0, 0);
            yield return null;
        }

        // Swing back.
        Quaternion peak = rightArm.localRotation;
        t = 0f;
        float back = duration * 0.65f;
        while (t < back)
        {
            t += Time.deltaTime;
            rightArm.localRotation = Quaternion.Lerp(peak, start, t / back);
            yield return null;
        }
        rightArm.localRotation = start;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private Transform Part(string name, PrimitiveType type, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(modelRoot, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        go.GetComponent<Renderer>().sharedMaterial = sharedMaterial;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        return go.transform;
    }

    private static void EnsureMaterial()
    {
        if (sharedMaterial != null) return;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        sharedMaterial = new Material(shader);
        sharedMaterial.SetColor("_BaseColor", new Color(0.15f, 0.35f, 0.9f));
        sharedMaterial.SetFloat("_Metallic", 0.6f);
        sharedMaterial.SetFloat("_Smoothness", 0.4f);
    }

    public static Material[] GetStaticMaterials()
    {
        var mats = new[] { sharedMaterial };
        sharedMaterial = null;
        return mats;
    }

    private static Material EyeMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(1f, 0.95f, 0.3f)); // yellow-white glow
        return mat;
    }
}
