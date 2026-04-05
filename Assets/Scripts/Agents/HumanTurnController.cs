using UnityEngine;

/// <summary>
/// Replaces HexAgent for human-controlled units.
/// Waits for player click input, converts to action, executes via HexMovement,
/// then signals GameManager that the turn is complete (same protocol as HexAgent).
/// </summary>
public class HumanTurnController : MonoBehaviour
{
    private UnitData unitData;
    private HexMovement movement;
    private HumanInputManager inputManager;

    private void Awake()
    {
        unitData = GetComponent<UnitData>();
        movement = GetComponent<HexMovement>();
    }

    private void Start()
    {
        inputManager = FindFirstObjectByType<HumanInputManager>();
    }

    private void Update()
    {
        if (unitData == null || !unitData.isMyTurn || !unitData.isAlive)
            return;

        if (inputManager == null)
        {
            inputManager = FindFirstObjectByType<HumanInputManager>();
            if (inputManager == null) return;
        }

        // Idle via Space or button.
        if (inputManager.IdleRequested)
        {
            inputManager.IdleRequested = false; // consume
            unitData.lastAction = UnitAction.Idle;
            CompleteTurn();
            return;
        }

        // Click on hex.
        if (!inputManager.HasClick)
            return;
        inputManager.HasClick = false; // consume

        HexCoord targetHex = inputManager.ClickedHex;
        HexCoord currentHex = unitData.currentHex;

        // Must be adjacent (distance 1).
        int distance = HexCoord.Distance(currentHex, targetHex);

        bool actionExecuted = false;

        switch (inputManager.ActionMode)
        {
            case HumanActionMode.Move:
                if (distance == 1)
                    actionExecuted = movement.TryMoveTo(targetHex);
                break;

            case HumanActionMode.Attack:
                if (distance == 1)
                {
                    int dir = GetDirection(currentHex, targetHex);
                    if (dir >= 0)
                        actionExecuted = movement.TryAttack(dir);
                }
                break;

            case HumanActionMode.Build:
                if (unitData.team == Team.Mutant)
                {
                    // Mutants build slime on their own hex (direction ignored).
                    actionExecuted = movement.TryBuild(0);
                }
                else if (distance == 1)
                {
                    int dir = GetDirection(currentHex, targetHex);
                    if (dir >= 0)
                        actionExecuted = movement.TryBuild(dir);
                }
                break;

            case HumanActionMode.DestroyWall:
                if (distance == 1)
                {
                    int dir = GetDirection(currentHex, targetHex);
                    if (dir >= 0)
                        actionExecuted = movement.TryDestroyWall(dir);
                }
                break;
        }

        if (actionExecuted)
            CompleteTurn();
        // If action failed (invalid target), don't end turn — let player try again.
    }

    private void CompleteTurn()
    {
        unitData.isMyTurn = false;
        unitData.hasPendingTurnResult = true;
    }

    /// <summary>
    /// Find the hex direction (0-5) from source to adjacent target.
    /// Returns -1 if not adjacent.
    /// </summary>
    private static int GetDirection(HexCoord from, HexCoord to)
    {
        for (int dir = 0; dir < 6; dir++)
        {
            if (from.Neighbor(dir) == to)
                return dir;
        }
        return -1;
    }
}
