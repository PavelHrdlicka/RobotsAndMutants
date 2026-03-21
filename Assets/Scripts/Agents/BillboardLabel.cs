using UnityEngine;

/// <summary>
/// Makes a TextMesh always face the main camera (billboard effect).
/// Attached to unit number labels so they're readable from any angle.
/// </summary>
public class BillboardLabel : MonoBehaviour
{
    private Transform cam;

    private void Start()
    {
        var mainCam = Camera.main;
        if (mainCam != null) cam = mainCam.transform;
    }

    private void LateUpdate()
    {
        if (cam == null)
        {
            var mainCam = Camera.main;
            if (mainCam != null) cam = mainCam.transform;
            else return;
        }

        // Face the camera — same rotation as camera so text is always readable.
        transform.rotation = cam.rotation;
    }
}
