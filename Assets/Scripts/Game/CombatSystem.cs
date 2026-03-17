using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves combat when two enemy units occupy the same hex.
/// Defender takes 1 damage. Attacker takes 1 damage unless shielded.
/// Unit at 0 HP dies with respawn cooldown.
/// </summary>
public class CombatSystem
{
    public int respawnCooldown = 30;

    /// <summary>Number of kills by Robots this step.</summary>
    public int robotKills { get; private set; }

    /// <summary>Number of kills by Mutants this step.</summary>
    public int mutantKills { get; private set; }

    /// <summary>
    /// Check all units for collisions and resolve combat. Call once per step.
    /// </summary>
    public void ResolveCombat(List<UnitData> allUnits)
    {
        robotKills = 0;
        mutantKills = 0;

        // Find pairs of enemy units on the same hex.
        for (int i = 0; i < allUnits.Count; i++)
        {
            var a = allUnits[i];
            if (!a.isAlive) continue;

            for (int j = i + 1; j < allUnits.Count; j++)
            {
                var b = allUnits[j];
                if (!b.isAlive) continue;
                if (a.team == b.team) continue;
                if (a.currentHex != b.currentHex) continue;

                // Combat! Determine attacker (the one who moved onto the tile).
                // For simplicity, both take damage simultaneously.
                Fight(a, b);
            }
        }
    }

    private void Fight(UnitData a, UnitData b)
    {
        a.lastAction = UnitAction.Attack;
        b.lastAction = UnitAction.Attack;

        int damageToA = b.hasShield ? 0 : 1;
        int damageToB = 1;

        // Shield protects the holder from incoming damage.
        if (a.hasShield) damageToA = 0;
        if (b.hasShield) damageToB = 0;

        // If neither is shielded, or both are, both take 1 damage.
        // If only one is shielded, only the unshielded one takes damage.

        a.Health -= damageToA > 0 ? 1 : 0;
        b.Health -= damageToB > 0 ? 1 : 0;

        if (a.Health <= 0)
        {
            a.Die(respawnCooldown);
            if (b.team == Team.Robot) robotKills++;
            else mutantKills++;
        }

        if (b.Health <= 0)
        {
            b.Die(respawnCooldown);
            if (a.team == Team.Robot) robotKills++;
            else mutantKills++;
        }
    }
}
