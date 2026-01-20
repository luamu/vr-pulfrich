using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Endless horizontal band of bars that loops seamlessly.
/// Bars are all present immediately at the start of each trial.
/// </summary>
public class MovingBarBandStimulus : MonoBehaviour
{
    [Header("Bar Prefab")]
    [Tooltip("A Quad/Cube prefab used as one bar (e.g. a Quad with a material).")]
    public Transform barPrefab;

    [Header("Camera (REQUIRED)")]
    [Tooltip("Camera used for distance placement + FOV computations.")]
    public Camera stimulusCamera;

    [Header("Visual Angle")]
    [Range(0.1f, 60f)]
    public float visualHeightDeg = 4f;
    public float barWidthDeg = 0.5f;

    [Header("Band Spacing")]
    [Tooltip("Gap between bars in degrees (visual angle). 0 means bars touch.")]
    public float gapDeg = 0.5f;

    [Header("Motion")]
    [Tooltip("Horizontal speed in deg/sec (converted to meters based on viewing distance).")]
    public float speedDegPerSec = 20f;

    [Tooltip("Extra padding outside FOV for wrap/reposition, in degrees.")]
    public float spawnPaddingDeg = 2f;

    [Header("Vertical Offset")]
    [Tooltip("Vertical offset of the entire band in degrees.")]
    public float yOffsetDeg = 0f;

    [Header("Distance")]
    [Tooltip("Reference distance (same as checkerboard distance), in meters from the camera.")]
    public float baseDistanceMeters = 15f;

    float signedOffsetMeters = 0f;
    int direction = 1;

    float halfHFovDeg;
    float halfVFovDeg;

    float currentDistanceMeters;

    readonly List<Transform> bars = new List<Transform>();

    void Start()
    {
        if (barPrefab == null)
        {
            Debug.LogError("MovingBarBandStimulus: barPrefab not assigned.");
            enabled = false;
            return;
        }

        if (stimulusCamera == null)
        {
            // Fallback: try parent camera, but you should really assign it explicitly.
            stimulusCamera = GetComponentInParent<Camera>();
        }

        if (stimulusCamera == null)
        {
            Debug.LogError("MovingBarBandStimulus: stimulusCamera not assigned and no parent camera found.");
            enabled = false;
            return;
        }

        CacheFov();
        RebuildForCurrentTrial();
    }

    void Update()
    {
        if (bars.Count == 0 || stimulusCamera == null) return;

        // Move at constant angular speed converted to meters at the current viewing distance.
        float dx = AngleToOffsetMeters(currentDistanceMeters, speedDegPerSec) * Time.deltaTime * direction;

        for (int i = 0; i < bars.Count; i++)
        {
            Vector3 p = bars[i].localPosition;
            p.x += dx;
            bars[i].localPosition = p;
        }

        WrapBarsIfNeeded();
    }

    /// <summary>
    /// Called by ExperimentManager at each trial.
    /// newSignedOffsetMeters can be negative or positive.
    /// </summary>
    public void Configure(float newSignedOffsetMeters, int newDirection, float newSpeedDegPerSec)
    {
        signedOffsetMeters = newSignedOffsetMeters;
        direction = (newDirection >= 0) ? 1 : -1;
        speedDegPerSec = Mathf.Max(0.01f, newSpeedDegPerSec);

        CacheFov();
        RebuildForCurrentTrial(); // ensures "already filled" at trial start
    }

    void RebuildForCurrentTrial()
    {
        // 1) Compute true viewing distance (meters from camera)
        currentDistanceMeters = Mathf.Max(0.05f, baseDistanceMeters + signedOffsetMeters);

        // 2) Place the entire band object exactly that far in front of the camera
        //    This is the key fix: distance is now REAL distance from camera, not "world z".
        Transform camT = stimulusCamera.transform;
        Vector3 center = camT.position + camT.forward * currentDistanceMeters;
        transform.position = center;
        transform.rotation = camT.rotation; // face the camera, keeps "x = horizontal, y = vertical"

        // 3) Now build bars in LOCAL space on the plane (z=0), sized by visual degrees at that distance
        BuildOrRebuildBandLocal();
    }

    void BuildOrRebuildBandLocal()
    {
        // Convert bar size and spacing to meters at this viewing distance
        float barH = VisualAngleToSize(currentDistanceMeters, visualHeightDeg);
        float barW = VisualAngleToSize(currentDistanceMeters, barWidthDeg);

        float spacingDeg = Mathf.Max(0.0001f, barWidthDeg + gapDeg);
        float spacingM = VisualAngleToSize(currentDistanceMeters, spacingDeg);

        // Horizontal visible extent (+ padding) in meters at this distance
        float extentDeg = halfHFovDeg + spawnPaddingDeg;
        float extentM = AngleToOffsetMeters(currentDistanceMeters, extentDeg);

        float yM = AngleToOffsetMeters(currentDistanceMeters, yOffsetDeg);

        int needed = Mathf.CeilToInt((2f * extentM) / spacingM) + 3;
        needed = Mathf.Max(1, needed);

        EnsureBarCount(needed);

        for (int i = 0; i < bars.Count; i++)
        {
            Transform t = bars[i];

            // NOTE: if your prefab is a Unity Quad, its size is 1x1 in X/Y.
            // This scaling makes it the correct physical size at the plane.
            t.localScale = new Vector3(barW, barH, 0f);

            float x = -extentM + i * spacingM;
            t.localPosition = new Vector3(x, yM, 0f);
        }
    }

    void EnsureBarCount(int needed)
    {
        while (bars.Count < needed)
        {
            Transform t = Instantiate(barPrefab, transform);
            t.name = $"{barPrefab.name}_{bars.Count:D2}";
            bars.Add(t);
        }

        while (bars.Count > needed)
        {
            Transform t = bars[bars.Count - 1];
            bars.RemoveAt(bars.Count - 1);
            if (t != null) Destroy(t.gameObject);
        }
    }

    void WrapBarsIfNeeded()
    {
        float extentDeg = halfHFovDeg + spawnPaddingDeg;
        float extentM = AngleToOffsetMeters(currentDistanceMeters, extentDeg);

        float spacingDeg = Mathf.Max(0.0001f, barWidthDeg + gapDeg);
        float spacingM = VisualAngleToSize(currentDistanceMeters, spacingDeg);

        if (direction == 1)
        {
            float leftmostX = float.PositiveInfinity;
            for (int i = 0; i < bars.Count; i++)
                leftmostX = Mathf.Min(leftmostX, bars[i].localPosition.x);

            for (int i = 0; i < bars.Count; i++)
            {
                if (bars[i].localPosition.x > extentM)
                {
                    Vector3 p = bars[i].localPosition;
                    p.x = leftmostX - spacingM;
                    bars[i].localPosition = p;
                    leftmostX = p.x;
                }
            }
        }
        else
        {
            float rightmostX = float.NegativeInfinity;
            for (int i = 0; i < bars.Count; i++)
                rightmostX = Mathf.Max(rightmostX, bars[i].localPosition.x);

            for (int i = 0; i < bars.Count; i++)
            {
                if (bars[i].localPosition.x < -extentM)
                {
                    Vector3 p = bars[i].localPosition;
                    p.x = rightmostX + spacingM;
                    bars[i].localPosition = p;
                    rightmostX = p.x;
                }
            }
        }
    }

    void CacheFov()
    {
        if (stimulusCamera == null)
        {
            Debug.LogWarning("MovingBarBandStimulus: No camera. Using fallback FOV.");
            halfVFovDeg = 45f;
            halfHFovDeg = 60f;
            return;
        }

        halfVFovDeg = stimulusCamera.fieldOfView * 0.5f;

        // hFov = 2 * atan(tan(vFov/2) * aspect)
        float halfVRad = halfVFovDeg * Mathf.Deg2Rad;
        float halfHRad = Mathf.Atan(Mathf.Tan(halfVRad) * stimulusCamera.aspect);
        halfHFovDeg = halfHRad * Mathf.Rad2Deg;
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
