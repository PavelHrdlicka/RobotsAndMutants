using UnityEngine;

/// <summary>
/// Destroys static Material/Texture references at Play mode start to prevent
/// GPU resource accumulation across sessions (D3D11 Resource ID overflow).
///
/// Static fields on MonoBehaviours survive domain reload in some configurations
/// but their GPU resources are never freed, causing texture SRV IDs to climb
/// past the D3D11 limit (~1M) after many Play sessions.
/// </summary>
public static class StaticResourceCleanup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Cleanup()
    {
        DestroyMaterials(
            UnitActionIndicator3D.GetStaticMaterials(),
            UnitHealthBar3D.GetStaticMaterials(),
            RobotModelBuilder.GetStaticMaterials(),
            MutantModelBuilder.GetStaticMaterials(),
            HexVisuals.GetStaticMaterials(),
            AdjacencyAura.GetStaticMaterials()
        );
    }

    private static void DestroyMaterials(params Material[][] groups)
    {
        foreach (var mats in groups)
        {
            if (mats == null) continue;
            foreach (var mat in mats)
            {
                if (mat != null)
                    Object.DestroyImmediate(mat);
            }
        }
    }
}
