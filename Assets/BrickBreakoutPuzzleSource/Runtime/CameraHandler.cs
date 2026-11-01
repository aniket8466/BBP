using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class CameraHandler : MonoBehaviour
{
    [SerializeField] private Camera controlledCamera;
    [SerializeField, Min(1f)] private float boardPadding = 1.2f;


    public Camera ControlledCamera
    {
        get
        {
            if (controlledCamera == null) controlledCamera = GetComponent<Camera>();
            return controlledCamera;
        }
    }

    private void Awake()
    {
        _ = ControlledCamera;
    }

    /// <summary>Centers the camera on the generated board and fits its full XY bounds.</summary>
    public void FitBoard(int width, int height, float cellSize)
    {
        Camera cameraToFit = ControlledCamera;
        if (cameraToFit == null || !cameraToFit.orthographic || width <= 0 || height <= 0) return;

        // LevelGenerator places the grid symmetrically around the world origin.
        Vector3 boardCenter = Vector3.zero;
        Vector3 toBoard = boardCenter - transform.position;
        if (toBoard.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(toBoard, Vector3.up);

        float aspect = Mathf.Max(0.5f, cameraToFit.aspect);
        float halfHeight = height * cellSize * 0.5f;
        float halfWidthAsHeight = width * cellSize / (2f * aspect);
        cameraToFit.orthographicSize = Mathf.Max(1f, Mathf.Max(halfHeight, halfWidthAsHeight) * boardPadding);
    }
}
