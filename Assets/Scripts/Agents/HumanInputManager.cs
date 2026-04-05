using UnityEngine;

/// <summary>
/// Handles mouse/click input for human player.
/// Converts screen clicks to hex coordinates via raycasting.
/// Singleton — one per scene during HumanVsAI mode.
/// </summary>
public class HumanInputManager : MonoBehaviour
{
    public HexGrid grid;

    /// <summary>Currently selected action mode.</summary>
    public HumanActionMode ActionMode { get; set; } = HumanActionMode.Move;

    /// <summary>True when a valid hex was clicked this frame.</summary>
    public bool HasClick { get; private set; }

    /// <summary>The hex coordinate that was clicked.</summary>
    public HexCoord ClickedHex { get; private set; }

    /// <summary>True when the player pressed Space or clicked Idle button.</summary>
    public bool IdleRequested { get; set; }

    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        HasClick = false;
        IdleRequested = false;

        // Action mode hotkeys.
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.M))
            ActionMode = HumanActionMode.Move;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.A))
            ActionMode = HumanActionMode.Attack;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.B))
            ActionMode = HumanActionMode.Build;
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.D))
            ActionMode = HumanActionMode.DestroyWall;

        // Idle: Space key.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            IdleRequested = true;
            return;
        }

        // Click on hex — intersect with y=0 plane (hex tiles have no colliders).
        if (Input.GetMouseButtonDown(0) && mainCamera != null && grid != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance))
            {
                Vector3 worldPoint = ray.GetPoint(distance);
                HexCoord hex = grid.WorldToHex(worldPoint);
                if (grid.IsValidCoord(hex))
                {
                    ClickedHex = hex;
                    HasClick = true;
                }
            }
        }
    }
}

/// <summary>
/// Action modes for human player input.
/// </summary>
public enum HumanActionMode
{
    Move,
    Attack,
    Build,
    DestroyWall
}
