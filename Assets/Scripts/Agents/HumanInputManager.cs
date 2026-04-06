using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles mouse/keyboard input for human player using the new Input System.
/// Converts screen clicks to hex coordinates via plane raycasting.
/// Singleton — one per scene during HumanVsAI mode.
/// </summary>
public class HumanInputManager : MonoBehaviour
{
    public HexGrid grid;

    /// <summary>Currently selected action mode.</summary>
    public HumanActionMode ActionMode { get; set; } = HumanActionMode.Move;

    /// <summary>True when a valid hex was clicked. Consumed by HumanTurnController.</summary>
    public bool HasClick { get; set; }

    /// <summary>The hex coordinate that was clicked.</summary>
    public HexCoord ClickedHex { get; private set; }

    /// <summary>True when the player pressed Space or clicked Idle button.</summary>
    public bool IdleRequested { get; set; }

    private Camera mainCamera;
    private InputAction clickAction;
    private InputAction positionAction;
    private InputAction hotkeyMove, hotkeyAttack, hotkeyBuild, hotkeyDestroy, hotkeyIdle;

    private void Start()
    {
        mainCamera = Camera.main;

        // Mouse click and position.
        clickAction = new InputAction("Click", InputActionType.Button, "<Mouse>/leftButton");
        positionAction = new InputAction("Position", InputActionType.Value, "<Mouse>/position");
        clickAction.Enable();
        positionAction.Enable();

        // Action mode hotkeys.
        hotkeyMove    = new InputAction("Move",    InputActionType.Button, "<Keyboard>/1");
        hotkeyAttack  = new InputAction("Attack",  InputActionType.Button, "<Keyboard>/2");
        hotkeyBuild   = new InputAction("Build",   InputActionType.Button, "<Keyboard>/3");
        hotkeyDestroy = new InputAction("Destroy", InputActionType.Button, "<Keyboard>/4");
        hotkeyIdle    = new InputAction("Idle",    InputActionType.Button, "<Keyboard>/space");

        // Alternate letter bindings.
        hotkeyMove.AddBinding("<Keyboard>/m");
        hotkeyAttack.AddBinding("<Keyboard>/a");
        hotkeyBuild.AddBinding("<Keyboard>/b");
        hotkeyDestroy.AddBinding("<Keyboard>/d");

        hotkeyMove.Enable();
        hotkeyAttack.Enable();
        hotkeyBuild.Enable();
        hotkeyDestroy.Enable();
        hotkeyIdle.Enable();
    }

    private void OnDestroy()
    {
        clickAction?.Dispose();
        positionAction?.Dispose();
        hotkeyMove?.Dispose();
        hotkeyAttack?.Dispose();
        hotkeyBuild?.Dispose();
        hotkeyDestroy?.Dispose();
        hotkeyIdle?.Dispose();
    }

    private void Update()
    {
        // Action mode hotkeys.
        if (hotkeyMove.WasPressedThisFrame())
            ActionMode = HumanActionMode.Move;
        if (hotkeyAttack.WasPressedThisFrame())
            ActionMode = HumanActionMode.Attack;
        if (hotkeyBuild.WasPressedThisFrame())
            ActionMode = HumanActionMode.Build;
        if (hotkeyDestroy.WasPressedThisFrame())
            ActionMode = HumanActionMode.DestroyWall;

        // Idle: Space key.
        if (hotkeyIdle.WasPressedThisFrame())
        {
            IdleRequested = true;
            return;
        }

        // Click on hex — intersect with y=0 plane (hex tiles have no colliders).
        if (clickAction.WasPressedThisFrame() && mainCamera != null && grid != null)
        {
            Vector2 screenPos = positionAction.ReadValue<Vector2>();
            Ray ray = mainCamera.ScreenPointToRay(screenPos);
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
