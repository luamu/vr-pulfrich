using UnityEngine;

public class MovingBarStimulus : MonoBehaviour
{
    //[Header("Stimulus Bar")]
    private Transform bar;   // Assign the Quad/Cube that is the visible bar

    [Header("Visual Angle")]
    [Range(0.1f, 60f)]
    public float visualHeightDeg = 4f;   // Desired visual height in degrees
    public float barWidthDeg = 0.5f;     // Width in degrees

    [Header("Motion")]
    [Tooltip("Horizontal speed in deg/sec (converted to meters based on final Z).")]
    public float speedDegPerSec = 50f;
    [Tooltip("Extra padding outside FOV for respawn, in degrees.")]
    public float spawnPaddingDeg = 2f;

    [Header("Vertical Offset")]
    [Tooltip("Vertical offset of the bar in degrees.")]
    public float yOffsetDeg = 0f;

    float baseDistanceMeters = 15f; // same as reference checkerboard distance from player
    float signedOffsetMeters = 0f;
    int direction = 1;
    float halfHFovDeg;
    float halfVFovDeg;

    void Reset()
    {
        
    }

    void Start()
    {
        bar = transform;
        CacheFov();
        ApplySizeAndPosition();
        Respawn();
    }

    void Update()
    {
        if (bar == null) return;

        float z = GetFinalZ();

        // Move at constant angular speed converted to meters at current Z
        float dx = AngleToOffsetMeters(z, speedDegPerSec) * Time.deltaTime;
        float x = bar.localPosition.x + direction * dx;

        bar.localPosition = new Vector3(x, bar.localPosition.y, z);

        if (IsPastExitEdge(x, z))
        {
            Respawn();
        }
    }

    /// <summary>
    /// Called by the experiment manager to configure a new trial.
    /// newSignedOffsetMeters can be negative or positive.
    /// Final Z is computed as: baseDistanceMeters + signedOffsetMeters.
    /// </summary>
    public void Configure(float newSignedOffsetMeters, int newDirection, float newSpeedDegPerSec)
    {
        signedOffsetMeters = newSignedOffsetMeters;
        direction = (newDirection >= 0) ? 1 : -1;
        speedDegPerSec = Mathf.Max(0.01f, newSpeedDegPerSec);

        CacheFov();
        ApplySizeAndPosition();
        Respawn();
    }

    float GetFinalZ()
    {
        // Final distance in front of camera
        float z = baseDistanceMeters + signedOffsetMeters;

        // Safety: never allow at/behind the camera plane
        return Mathf.Max(0.05f, z);
    }

    void CacheFov()
    {
        // this is just a quick fix and should be reimplemented because it's a horrible way to do it
        Camera cam = GetComponentInParent<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("MovingBarStimulus: No parent Camera found.");
            halfVFovDeg = 45f;
            halfHFovDeg = 60f;
            return;
        }

        halfVFovDeg = cam.fieldOfView * 0.5f;

        // hFov = 2 * atan(tan(vFov/2) * aspect)
        float halfVRad = halfVFovDeg * Mathf.Deg2Rad;
        float halfHRad = Mathf.Atan(Mathf.Tan(halfVRad) * cam.aspect);
        halfHFovDeg = halfHRad * Mathf.Rad2Deg;
    }

    void ApplySizeAndPosition()
    {
        if (bar == null) return;

        float z = GetFinalZ();

        // Keep constant visual angle at this Z
        float heightMeters = VisualAngleToSize(z, visualHeightDeg);
        float widthMeters  = VisualAngleToSize(z, barWidthDeg);

        bar.localScale = new Vector3(widthMeters, heightMeters, 1f);

        float yMeters = AngleToOffsetMeters(z, yOffsetDeg);
        bar.localPosition = new Vector3(bar.localPosition.x, yMeters, z);
    }

    void Respawn()
    {
        if (bar == null) return;

        float z = GetFinalZ();

        float spawnXDeg = halfHFovDeg + spawnPaddingDeg;
        float spawnXMeters = AngleToOffsetMeters(z, spawnXDeg);

        float startX = (direction == 1) ? -spawnXMeters : +spawnXMeters;
        float yMeters = AngleToOffsetMeters(z, yOffsetDeg);

        bar.localPosition = new Vector3(startX, yMeters, z);
    }

    bool IsPastExitEdge(float xMeters, float z)
    {
        float exitXDeg = halfHFovDeg + spawnPaddingDeg;
        float exitXMeters = AngleToOffsetMeters(z, exitXDeg);

        return (direction == 1) ? (xMeters > exitXMeters) : (xMeters < -exitXMeters);
    }

    static float VisualAngleToSize(float distanceMeters, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return 2f * distanceMeters * Mathf.Tan(rad * 0.5f);
    }

    static float AngleToOffsetMeters(float distanceMeters, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return distanceMeters * Mathf.Tan(rad);
    }
}
