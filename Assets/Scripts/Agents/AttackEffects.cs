using UnityEngine;

/// <summary>
/// Triggers attack animations and particle effects based on UnitData.lastAction changes.
/// Robot: hammer swing + orange sparks.
/// Mutant: lean forward + green slime stream.
/// Death: grey poof. Respawn: sparkle.
/// </summary>
[RequireComponent(typeof(UnitData))]
public class AttackEffects : MonoBehaviour
{
    private UnitData unitData;
    private RobotModelBuilder robotModel;
    private MutantModelBuilder mutantModel;
    private HexGrid grid;

    private bool wasAlive;

    // Tile flash state — only active during the attack turn.
    private HexTileData flashedAttackerTile;
    private HexTileData flashedTargetTile;
    private Color savedAttackerColor;
    private Color savedTargetColor;
    private bool isFlashing;

    private static readonly Color AttackerFlashColor = new Color(0.9f, 0.15f, 0.1f);  // red
    private static readonly Color TargetFlashColor   = new Color(1f, 0.55f, 0.1f);     // orange

    // Particle system references (created once, reused).
    private ParticleSystem sparkPS;
    private ParticleSystem slimePS;
    private ParticleSystem deathPS;
    private ParticleSystem respawnPS;

    private void Awake()
    {
        unitData    = GetComponent<UnitData>();
        robotModel  = GetComponent<RobotModelBuilder>();
        mutantModel = GetComponent<MutantModelBuilder>();
    }

    private void Start()
    {
        grid = Object.FindFirstObjectByType<HexGrid>();
        wasAlive = unitData.isAlive;

        if (robotModel != null) CreateSparkParticles();
        if (mutantModel != null) CreateSlimeParticles();
        CreateDeathParticles();
        CreateRespawnParticles();
    }

    /// <summary>
    /// Call directly from HexAgent when a combat action is performed.
    /// This ensures the flash triggers even at high time scales where
    /// Update() can't detect the lastAction transition.
    /// </summary>
    public void TriggerCombatFlash()
    {
        UnflashTiles();
        OnAttack();
    }

    private void Update()
    {
        if (unitData == null) return;

        // Auto-unflash after a short duration (flashTimer).
        if (isFlashing)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f)
                UnflashTiles();
        }

        // Detect death.
        if (wasAlive && !unitData.isAlive)
        {
            UnflashTiles();
            OnDeath();
        }

        // Detect respawn.
        if (!wasAlive && unitData.isAlive)
            OnRespawn();

        wasAlive = unitData.isAlive;
    }

    private float flashTimer;

    private void OnAttack()
    {
        if (grid == null) return;

        // Target hex: Attack uses lastAttackHex, DestroyWall uses lastBuildTarget.
        HexCoord targetHex = unitData.lastAction == UnitAction.DestroyWall
            ? unitData.lastBuildTarget
            : unitData.lastAttackHex;

        // Direction toward the target hex.
        Vector3 targetWorld = grid.HexToWorld(targetHex);
        Vector3 dir = targetWorld - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.001f) dir = transform.forward;

        if (robotModel != null)
        {
            robotModel.SwingHammer(dir);
            if (sparkPS != null)
            {
                sparkPS.transform.position = transform.position + dir.normalized * 0.15f + Vector3.up * 0.15f;
                sparkPS.Play();
            }
        }

        if (mutantModel != null)
        {
            mutantModel.LeanForward(dir);
            if (slimePS != null)
            {
                slimePS.transform.position = transform.position + Vector3.up * 0.15f;
                slimePS.transform.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 0.001f ? dir : transform.forward);
                slimePS.Play();
            }
        }

        // Flash tiles: red under attacker, orange under defender.
        FlashTiles();
    }

    private void FlashTiles()
    {
        if (grid == null) return;

        // Attacker tile → red (under the attacking unit).
        var aTile = grid.GetTile(unitData.currentHex);
        if (aTile != null)
        {
            var meshGen = aTile.GetComponent<HexMeshGenerator>();
            if (meshGen != null)
            {
                savedAttackerColor = GetCurrentColor(aTile);
                meshGen.SetColor(AttackerFlashColor);
                flashedAttackerTile = aTile;
            }
        }

        // Target tile → orange (the adjacent hex being attacked or destroyed).
        HexCoord flashTarget = unitData.lastAction == UnitAction.DestroyWall
            ? unitData.lastBuildTarget
            : unitData.lastAttackHex;
        var tTile = grid.GetTile(flashTarget);
        if (tTile != null)
        {
            var meshGen2 = tTile.GetComponent<HexMeshGenerator>();
            if (meshGen2 != null)
            {
                savedTargetColor = GetCurrentColor(tTile);
                meshGen2.SetColor(TargetFlashColor);
                flashedTargetTile = tTile;
            }
        }

        isFlashing = true;
        // Flash duration scales with tick speed so it's visible even at high timeScale.
        var cfg = GameConfig.Instance;
        float tickMs = cfg != null ? cfg.msPerTick : 200;
        flashTimer = Mathf.Max(0.15f, tickMs / 1000f * 2f);
    }

    private void UnflashTiles()
    {
        if (flashedAttackerTile != null)
        {
            var meshGen = flashedAttackerTile.GetComponent<HexMeshGenerator>();
            if (meshGen != null) meshGen.SetColor(savedAttackerColor);
            flashedAttackerTile = null;
        }
        if (flashedTargetTile != null)
        {
            var meshGen = flashedTargetTile.GetComponent<HexMeshGenerator>();
            if (meshGen != null) meshGen.SetColor(savedTargetColor);
            flashedTargetTile = null;
        }
        isFlashing = false;
    }

    private static Color GetCurrentColor(HexTileData tile)
    {
        var visuals = tile.GetComponent<HexVisuals>();
        if (visuals != null)
            return HexVisuals.GetColorForState(tile.Owner, tile.TileType, tile.isBase, tile.baseTeam, tile.WallHP);
        return Color.grey;
    }

    private void OnDeath()
    {
        if (deathPS != null)
        {
            // Spawn at current position before GO is deactivated.
            deathPS.transform.position = transform.position + Vector3.up * 0.15f;
            // Detach so it plays even after GO deactivation.
            deathPS.transform.SetParent(null, true);
            deathPS.Play();
            // Re-parent after a delay (handled in OnRespawn or just leave detached).
        }
    }

    private void OnRespawn()
    {
        // Re-parent death PS if it was detached.
        if (deathPS != null && deathPS.transform.parent == null)
            deathPS.transform.SetParent(transform, false);

        if (respawnPS != null)
        {
            respawnPS.transform.position = transform.position + Vector3.up * 0.15f;
            respawnPS.Play();
        }
    }

    // ── Particle system creation ────────────────────────────────────────

    private void CreateSparkParticles()
    {
        sparkPS = CreatePS("Sparks", transform);
        var main = sparkPS.main;
        main.startLifetime = 0.3f;
        main.startSpeed    = 2f;
        main.startSize     = 0.015f;
        main.startColor    = new Color(1f, 0.7f, 0.2f);
        main.maxParticles  = 15;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = sparkPS.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

        var shape = sparkPS.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 35f;
        shape.radius    = 0.02f;
    }

    private void CreateSlimeParticles()
    {
        slimePS = CreatePS("SlimeStream", transform);
        var main = slimePS.main;
        main.startLifetime = 0.5f;
        main.startSpeed    = 1.5f;
        main.startSize     = 0.025f;
        main.startColor    = new Color(0.3f, 0.9f, 0.1f, 0.8f);
        main.maxParticles  = 25;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.5f;

        var emission = slimePS.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

        var shape = slimePS.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 15f;
        shape.radius    = 0.01f;
    }

    private void CreateDeathParticles()
    {
        deathPS = CreatePS("DeathPoof", transform);
        var main = deathPS.main;
        main.startLifetime = 0.6f;
        main.startSpeed    = 0.8f;
        main.startSize     = 0.04f;
        main.startColor    = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        main.maxParticles  = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.3f; // float upward

        var emission = deathPS.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var shape = deathPS.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.05f;

        // Fade over lifetime.
        var colorOverLifetime = deathPS.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = gradient;
    }

    private void CreateRespawnParticles()
    {
        respawnPS = CreatePS("RespawnSparkle", transform);
        var main = respawnPS.main;
        main.startLifetime = 0.8f;
        main.startSpeed    = 1.2f;
        main.startSize     = 0.012f;
        main.startColor    = new Color(1f, 1f, 0.6f, 0.9f);
        main.maxParticles  = 25;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.2f;

        var emission = respawnPS.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

        var shape = respawnPS.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 0.06f;
    }

    private static ParticleSystem CreatePS(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();

        // Disable auto-play.
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;

        // Default renderer with URP particle material.
        var psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null)
            psr.material = new Material(shader);

        return ps;
    }
}
