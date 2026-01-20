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

    [Header("Visual Angle")]
    [Range(0.1f, 60f)]
    public float visualHeightDeg = 4f;
    public float barWidthDeg = 0.5f;

    [Header("Band Spacing")]
    [Tooltip("Gap between bars in degrees (visual angle). 0 means bars touch.")]
    public float gapDeg = 0.5f;

    [Header("Motion")]
    [Tooltip("Horizontal speed in deg/sec (converted to meters based on final Z).")]
    public float speedDegPerSec = 20f;

    [Tooltip("Extra padding outside FOV for wrap/reposition, in degrees.")]
    public float spawnPaddingDeg = 2f;

    [Header("Vertical Offset")]
    [Tooltip("Vertical offset of the entire band in degrees.")]
    public float yOffsetDeg = 0f;

    [Header("Distance")]
    [Tooltip("Reference distance (same as checkerboard distance).")]
    public float baseDistanceMeters = 15f;

    float signedOffsetMeters = 0f;
    int direction = 1;

    float halfHFovDeg;
    float halfVFovDeg;

    readonly List<Transform> bars = new List<Transform>();

    void Start()
    {
        if (barPrefab == null)
        {
            Debug.LogError("MovingBarBandStimulus: barPrefab not assigned.");
            enabled = false;
            return;
        }

        CacheFov();
        BuildOrRebuildBand();
    }

    void Update()
    {
        if (bars.Count == 0) return;

        float z = GetFinalZ();

        // Move at constant angular speed converted to meters at current Z
        float dx = AngleToOffsetMeters(z, speedDegPerSec) * Time.deltaTime * direction;

        for (int i = 0; i < bars.Count; i++)
        {
            Vector3 p = bars[i].localPosition;
            p.x += dx;
            p.z = z; // keep z updated per trial
            bars[i].localPosition = p;
        }

        WrapBarsIfNeeded(z);
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
        BuildOrRebuildBand();   // ensures "already filled" at trial start
    }

    void BuildOrRebuildBand()
    {
        float z = GetFinalZ();

        // Convert bar size and spacing to meters at this z
        float barH = VisualAngleToSize(z, visualHeightDeg);
        float barW = VisualAngleToSize(z, barWidthDeg);

        float spacingDeg = Mathf.Max(0.0001f, barWidthDeg + gapDeg);
        float spacingM = VisualAngleToSize(z, spacingDeg);

        // Horizontal visible extent (+ padding) in meters
        float extentDeg = halfHFovDeg + spawnPaddingDeg;
        float extentM = AngleToOffsetMeters(z, extentDeg);

        float yM = AngleToOffsetMeters(z, yOffsetDeg);

        // How many bars do we need to cover from -extentM to +extentM?
        // Add a couple extras so wrapping is seamless.
        int needed = Mathf.CeilToInt((2f * extentM) / spacingM) + 3;
        needed = Mathf.Max(1, needed);

        // Ensure we have exactly that many instances
        EnsureBarCount(needed);

        // Apply size and lay them out to fully fill at trial start
        for (int i = 0; i < bars.Count; i++)
        {
            Transform t = bars[i];
            t.localScale = new Vector3(barW, barH, 1f);

            float x = -extentM + i * spacingM;
            t.localPosition = new Vector3(x, yM, z);
        }
    }

    void EnsureBarCount(int needed)
    {
        // Create missing
        while (bars.Count < needed)
        {
            Transform t = Instantiate(barPrefab, transform);
            t.name = $"{barPrefab.name}_{bars.Count:D2}";
            bars.Add(t);
        }

        // Remove extras
        while (bars.Count > needed)
        {
            Transform t = bars[bars.Count - 1];
            bars.RemoveAt(bars.Count - 1);
            if (t != null) Destroy(t.gameObject);
        }
    }

    void WrapBarsIfNeeded(float z)
    {
        float extentDeg = halfHFovDeg + spawnPaddingDeg;
        float extentM = AngleToOffsetMeters(z, extentDeg);

        float spacingDeg = Mathf.Max(0.0001f, barWidthDeg + gapDeg);
        float spacingM = VisualAngleToSize(z, spacingDeg);

        if (direction == 1)
        {
            // Moving left -> right: if a bar passes +extent, move it behind the leftmost bar
            float leftmostX = float.PositiveInfinity;
            for (int i = 0; i < bars.Count; i++)
                leftmostX = Mathf.Min(leftmostX, bars[i].localPosition.x);

            for (int i = 0; i < bars.Count; i++)
            {
                if (bars[i].localPosition.x > extentM)
                {
                    Vector3 p = bars[i].localPosition;
                    p.x = leftmostX - spacingM;
                    p.z = z;
                    bars[i].localPosition = p;

                    leftmostX = p.x; // update so multiple can wrap cleanly
                }
            }
        }
        else
        {
            // Moving right -> left: if a bar passes -extent, move it behind the rightmost bar
            float rightmostX = float.NegativeInfinity;
            for (int i = 0; i < bars.Count; i++)
                rightmostX = Mathf.Max(rightmostX, bars[i].localPosition.x);

            for (int i = 0; i < bars.Count; i++)
            {
                if (bars[i].localPosition.x < -extentM)
                {
                    Vector3 p = bars[i].localPosition;
                    p.x = rightmostX + spacingM;
                    p.z = z;
                    bars[i].localPosition = p;

                    rightmostX = p.x;
                }
            }
        }
    }

    float GetFinalZ()
    {
        float z = baseDistanceMeters + signedOffsetMeters;
        return Mathf.Max(0.05f, z);
    }

    void CacheFov()
    {
        Camera cam = GetComponentInParent<Camera>();
        if (cam == null)
        {
            Debug.LogWarning("MovingBarBandStimulus: No parent Camera found. Using fallback FOV.");
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
