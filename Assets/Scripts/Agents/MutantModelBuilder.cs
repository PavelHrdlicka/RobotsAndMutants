using System.Collections;
using UnityEngine;

/// <summary>
/// Builds a blobby mutant from spheres under a ModelRoot child.
/// Idle bob/pulse animation runs continuously. Tentacles wave gently.
/// </summary>
public class MutantModelBuilder : MonoBehaviour
{
    [HideInInspector] public Transform modelRoot;
    [HideInInspector] public Transform body;
    [HideInInspector] public Transform head;
    [HideInInspector] public Transform tentacle1;
    [HideInInspector] public Transform tentacle2;
    [HideInInspector] public Transform tentacle3;

    private static Material sharedMaterial;
    private static Material eyeMaterial;

    private float bobPhase;
    private const float BobAmp   = 0.012f;
    private const float BobSpeed = 2.5f;
    private const float PulseAmp = 0.015f;
    private const float PulseSpeed = 3f;

    // Tentacle wave offsets.
    private Vector3 t1Base, t2Base, t3Base;

    public void Build()
    {
        EnsureMaterial();

        modelRoot = new GameObject("ModelRoot").transform;
        modelRoot.SetParent(transform, false);

        body = Part("Body", PrimitiveType.Sphere,
            new Vector3(0f, 0.10f, 0f), new Vector3(0.18f, 0.14f, 0.18f));

        head = Part("Head", PrimitiveType.Sphere,
            new Vector3(0f, 0.22f, 0f), new Vector3(0.10f, 0.10f, 0.10f));

        tentacle1 = Part("Tent1", PrimitiveType.Capsule,
            new Vector3(-0.11f, 0.08f, 0.04f), new Vector3(0.03f, 0.06f, 0.03f));

        tentacle2 = Part("Tent2", PrimitiveType.Capsule,
            new Vector3( 0.11f, 0.08f, 0.04f), new Vector3(0.03f, 0.06f, 0.03f));

        tentacle3 = Part("Tent3", PrimitiveType.Capsule,
            new Vector3(0f, 0.06f, -0.10f), new Vector3(0.03f, 0.05f, 0.03f));

        t1Base = tentacle1.localPosition;
        t2Base = tentacle2.localPosition;
        t3Base = tentacle3.localPosition;

        // Two glowing eyes.
        var eyeL = Part("EyeL", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.025f, 0.03f, 0.02f));
        eyeL.SetParent(head, false);
        eyeL.localPosition = new Vector3(-0.025f, 0.01f, 0.04f);
        if (eyeMaterial == null) eyeMaterial = EyeMaterial();
        eyeL.GetComponent<Renderer>().sharedMaterial = eyeMaterial;

        var eyeR = Part("EyeR", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.025f, 0.03f, 0.02f));
        eyeR.SetParent(head, false);
        eyeR.localPosition = new Vector3( 0.025f, 0.01f, 0.04f);
        eyeR.GetComponent<Renderer>().sharedMaterial = eyeMaterial;

        // Scale up entire model for better visibility.
        modelRoot.localScale = Vector3.one * 1.8f;

        bobPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (modelRoot == null) return;

        float time = Time.time + bobPhase;

        // Bob up/down.
        float bob = Mathf.Sin(time * BobSpeed) * BobAmp;
        modelRoot.localPosition = new Vector3(0f, bob, 0f);

        // Body pulse (scale oscillation).
        if (body != null)
        {
            float pulse = 1f + Mathf.Sin(time * PulseSpeed) * PulseAmp;
            body.localScale = new Vector3(0.18f * pulse, 0.14f * pulse, 0.18f * pulse);
        }

        // Tentacle wave.
        float wave = Mathf.Sin(time * 3.2f) * 0.015f;
        if (tentacle1 != null) tentacle1.localPosition = t1Base + new Vector3(wave, 0, -wave);
        if (tentacle2 != null) tentacle2.localPosition = t2Base + new Vector3(-wave, 0, -wave);
        if (tentacle3 != null) tentacle3.localPosition = t3Base + new Vector3(0, wave * 0.5f, wave);
    }

    /// <summary>Lean toward target and "vomit" (visual lean, particles handled by AttackEffects).</summary>
    public void LeanForward(Vector3 worldDir, float duration = 0.5f)
    {
        StartCoroutine(LeanCo(worldDir, duration));
    }

    private IEnumerator LeanCo(Vector3 worldDir, float duration)
    {
        if (modelRoot == null) yield break;

        // Face direction.
        if (worldDir.sqrMagnitude > 0.001f)
        {
            worldDir.y = 0;
            modelRoot.rotation = Quaternion.LookRotation(worldDir, Vector3.up);
        }

        Quaternion startRot = modelRoot.localRotation;
        float leanAngle = 25f;

        // Lean forward.
        float t = 0f;
        float half = duration * 0.4f;
        while (t < half)
        {
            t += Time.deltaTime;
            float frac = t / half;
            modelRoot.localRotation = startRot * Quaternion.Euler(leanAngle * frac, 0, 0);
            yield return null;
        }

        // Hold briefly.
        yield return new WaitForSeconds(duration * 0.15f);

        // Lean back.
        Quaternion peak = modelRoot.localRotation;
        t = 0f;
        float back = duration * 0.45f;
        while (t < back)
        {
            t += Time.deltaTime;
            modelRoot.localRotation = Quaternion.Lerp(peak, startRot, t / back);
            yield return null;
        }
        modelRoot.localRotation = startRot;
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
        sharedMaterial.SetColor("_BaseColor", new Color(0.25f, 0.8f, 0.15f));
        sharedMaterial.SetFloat("_Metallic", 0.0f);
        sharedMaterial.SetFloat("_Smoothness", 0.8f); // glossy/slimy
    }

    public static Material[] GetStaticMaterials()
    {
        var mats = new[] { sharedMaterial, eyeMaterial };
        sharedMaterial = null;
        eyeMaterial = null;
        return mats;
    }

    private static Material EyeMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(1f, 0.2f, 0.1f)); // red glow
        return mat;
    }
}
