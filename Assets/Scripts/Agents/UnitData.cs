using UnityEngine;

/// <summary>
/// Per-unit runtime state: team, energy, position, alive/dead status, respawn cooldown.
/// Energy is the universal resource — serves as HP and action currency.
/// </summary>
public class UnitData : MonoBehaviour
{
    [Header("Identity")]
    public Team team;
    public int unitIndex;

    [Header("Stats")]
    public int maxEnergy = 15;
    [SerializeField] private int energy = 15;

    [Header("Position")]
    public HexCoord currentHex;

    [Header("Status")]
    public bool isAlive = true;
    public int respawnCooldown;

    [Header("Action")]
    public UnitAction lastAction = UnitAction.Move; // Move = "no indicator" — idle spinner shown only on explicit idle choice
    public HexCoord moveFrom;
    public HexCoord moveTo;

    /// <summary>Set by HexMovement.TryAttack — the enemy that was attacked this turn.</summary>
    [HideInInspector] public UnitData lastAttackTarget;
    /// <summary>True if lastAttackTarget died from the attack.</summary>
    [HideInInspector] public bool lastAttackKilled;

    /// <summary>
    /// Set by HexAgent.OnActionReceived to signal GameManager that this unit's
    /// turn action has been executed and post-turn processing can proceed.
    /// </summary>
    [HideInInspector]
    public bool hasPendingTurnResult;

    /// <summary>
    /// Set by GameManager to indicate this unit is the active turn unit.
    /// Only the active unit executes its action; others observe only.
    /// </summary>
    [HideInInspector]
    public bool isMyTurn;

    public int Energy
    {
        get => energy;
        set => energy = Mathf.Clamp(value, 0, maxEnergy);
    }

    /// <summary>Kill this unit: hide it, start respawn cooldown.</summary>
    public void Die(int cooldownSteps = 6)
    {
        isAlive = false;
        energy = 0;
        lastAction = UnitAction.Dead;
        respawnCooldown = cooldownSteps;
        gameObject.SetActive(false);
    }

    /// <summary>Respawn at given hex with full energy.</summary>
    public void Respawn(HexCoord hex, Vector3 worldPos)
    {
        ApplyConfigEnergy();
        isAlive = true;
        energy = maxEnergy;
        respawnCooldown = 0;
        currentHex = hex;
        transform.position = worldPos + Vector3.up * 0.3f;
        gameObject.SetActive(true);
    }

    /// <summary>Tick respawn cooldown. Returns true when ready to respawn.</summary>
    public bool TickCooldown()
    {
        if (isAlive) return false;
        respawnCooldown--;
        return respawnCooldown <= 0;
    }

    /// <summary>Reset unit to initial state for a new episode.</summary>
    public void ResetUnit()
    {
        ApplyConfigEnergy();
        energy = maxEnergy;
        isAlive = true;
        respawnCooldown = 0;
        gameObject.SetActive(true);
    }

    /// <summary>Sync maxEnergy from GameConfig if available.</summary>
    public void ApplyConfigEnergy()
    {
        var cfg = GameConfig.Instance;
        if (cfg != null)
            maxEnergy = cfg.unitMaxEnergy;
    }
}
