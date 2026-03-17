using UnityEngine;

/// <summary>
/// Per-unit runtime state: team, health, position, alive/dead status, respawn cooldown.
/// </summary>
public class UnitData : MonoBehaviour
{
    [Header("Identity")]
    public Team team;
    public int unitIndex;

    [Header("Stats")]
    public int maxHealth = 3;
    [SerializeField] private int health = 3;

    [Header("Position")]
    public HexCoord currentHex;

    [Header("Status")]
    public bool isAlive = true;
    public int respawnCooldown;

    [Header("Buffs")]
    public bool hasShield;
    public float speedMultiplier = 1f;

    [Header("Action")]
    public UnitAction lastAction = UnitAction.Move; // Move = "no indicator" — idle spinner shown only on explicit idle choice
    public HexCoord moveFrom;
    public HexCoord moveTo;

    /// <summary>
    /// Set by HexAgent.OnActionReceived to signal GameManager that this unit's
    /// turn action has been executed and post-turn processing can proceed.
    /// </summary>
    [HideInInspector]
    public bool hasPendingTurnResult;

    public int Health
    {
        get => health;
        set => health = Mathf.Clamp(value, 0, maxHealth);
    }

    /// <summary>Kill this unit: hide it, start respawn cooldown.</summary>
    public void Die(int cooldownSteps = 30)
    {
        isAlive = false;
        health = 0;
        lastAction = UnitAction.Dead;
        respawnCooldown = cooldownSteps;
        gameObject.SetActive(false);
    }

    /// <summary>Respawn at given hex with full health.</summary>
    public void Respawn(HexCoord hex, Vector3 worldPos)
    {
        isAlive = true;
        health = maxHealth;
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
        health = maxHealth;
        isAlive = true;
        respawnCooldown = 0;
        hasShield = false;
        speedMultiplier = 1f;
        gameObject.SetActive(true);
    }
}
