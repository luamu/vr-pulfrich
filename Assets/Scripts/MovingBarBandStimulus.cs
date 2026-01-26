using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Endless horizontal band of bars that loops seamlessly.
/// Bars are all present immediately at the start of each trial.
/// Per-trial: each bar gets its own tilt + its own color.
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
    public float baseDistanceMeters = 1f;

    [Header("Per-Bar Tilt (degrees)")]
    [Tooltip("Each bar gets a random Z-tilt within this range every trial. -90..90 covers vertical/horizontal/all in-between.")]
    public Vector2 perBarTiltDegRange = new Vector2(-90f, 90f);

    [Header("Per-Bar Color (base + jitter)")]
    [Tooltip("If non-empty: pick ONE base color from palette per trial, then jitter each bar around it.")]
    public List<Color> colorPalette = new List<Color>();

    [Tooltip("If palette empty: random HSV saturation range for base color.")]
    public Vector2 hsvSaturationRange = new Vector2(0.6f, 1.0f);

    [Tooltip("If palette empty: random HSV value/brightness range for base color.")]
    public Vector2 hsvValueRange = new Vector2(0.6f, 1.0f);

    [Range(0f, 1f)] public float hueJitter = 0.05f;
    [Range(0f, 1f)] public float saturationJitter = 0.15f;
    [Range(0f, 1f)] public float valueJitter = 0.15f;

    [Header("Shader Color Property Names")]
    [Tooltip("Tried in order. Built-in Standard uses _Color. URP Lit uses _BaseColor. Custom shaders may differ.")]
    public string[] colorPropertyNames = new[] { "_BaseColor", "_Color" };

    float signedOffsetMeters = 0f;
    int direction = 1;
    int lastAppearanceBarCount = -1;
    float halfHFovDeg;
    float halfVFovDeg;
    float currentDistanceMeters;

    readonly List<Transform> bars = new List<Transform>();
    readonly List<float> barTiltsDeg = new List<float>();
    readonly List<Color> barColors = new List<Color>();

    MaterialPropertyBlock mpb;

    int trialCounter = 0; // internal seed so every Configure() creates a new look

    void Start()
    {
        if (barPrefab == null)
        {
            Debug.LogError("MovingBarBandStimulus: barPrefab not assigned.");
            enabled = false;
            return;
        }

        if (stimulusCamera == null)
            stimulusCamera = GetComponentInParent<Camera>();

        if (stimulusCamera == null)
        {
            Debug.LogError("MovingBarBandStimulus: stimulusCamera not assigned and no parent camera found.");
            enabled = false;
            return;
        }

        CacheFov();

        // Initial build (and initial random appearance)
        GenerateNewPerBarAppearanceSeeded(69); // keep constant with any wanted seed
        lastAppearanceBarCount = bars.Count;
        ApplyPerBarAppearance();
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

        // Rebuild at new distance (may change bar count)
        RebuildForCurrentTrial();

        // Only (re)generate appearance if bar count changed
        if (bars.Count != lastAppearanceBarCount)
        {
            GenerateNewPerBarAppearanceSeeded(12345); // same constant seed => stable look
            lastAppearanceBarCount = bars.Count;
        }

        // Always apply (so new/respawned bars get their saved color/tilt)
        ApplyPerBarAppearance();
    }

    void RebuildForCurrentTrial()
    {
        // 1) Compute true viewing distance (meters from camera)
        currentDistanceMeters = Mathf.Max(0.05f, baseDistanceMeters + signedOffsetMeters);

        // 2) Place the entire band object exactly that far in front of the camera
        Transform camT = stimulusCamera.transform;
        Vector3 center = camT.position + camT.forward * currentDistanceMeters;
        transform.position = center;

        // IMPORTANT: Keep the band facing the camera but DO NOT tilt the whole band.
        transform.rotation = camT.rotation;

        // 3) Build bars in LOCAL space on the plane (z=0), sized by visual degrees at that distance
        BuildOrRebuildBandLocal();
    }

    void BuildOrRebuildBandLocal()
    {
        float barH = VisualAngleToSize(currentDistanceMeters, visualHeightDeg);
        float barW = VisualAngleToSize(currentDistanceMeters, barWidthDeg);

        float spacingDeg = Mathf.Max(0.0001f, barWidthDeg + gapDeg);
        float spacingM = VisualAngleToSize(currentDistanceMeters, spacingDeg);

        float extentDeg = halfHFovDeg + spawnPaddingDeg;
        float extentM = AngleToOffsetMeters(currentDistanceMeters, extentDeg);

        float yM = AngleToOffsetMeters(currentDistanceMeters, yOffsetDeg);

        int needed = Mathf.CeilToInt((2f * extentM) / spacingM) + 3;
        needed = Mathf.Max(1, needed);

        EnsureBarCount(needed);

        for (int i = 0; i < bars.Count; i++)
        {
            Transform t = bars[i];

            // NOTE: Quads are 1x1 in X/Y. Cubes are 1x1x1; scaling Z to 0 can be problematic on cubes.
            // Use a tiny Z instead of 0 to avoid some renderer/shader edge cases.
            t.localScale = new Vector3(barW, barH, 0.001f);

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

            // Keep arrays aligned
            barTiltsDeg.Add(0f);
            barColors.Add(Color.white);
        }

        while (bars.Count > needed)
        {
            int last = bars.Count - 1;

            Transform t = bars[last];
            bars.RemoveAt(last);
            barTiltsDeg.RemoveAt(last);
            barColors.RemoveAt(last);

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
        halfVFovDeg = stimulusCamera.fieldOfView * 0.5f;

        // hFov = 2 * atan(tan(vFov/2) * aspect)
        float halfVRad = halfVFovDeg * Mathf.Deg2Rad;
        float halfHRad = Mathf.Atan(Mathf.Tan(halfVRad) * stimulusCamera.aspect);
        halfHFovDeg = halfHRad * Mathf.Rad2Deg;
    }

    // --------------------------
    // Per-bar appearance
    // --------------------------

    void GenerateNewPerBarAppearanceSeeded(int seed)
    {
        // Make deterministic per trial, but not affecting other Unity random use:
        var oldState = Random.state;
        Random.InitState(seed * 7919 + bars.Count * 104729);

        Color baseColor = PickTrialBaseColor();

        for (int i = 0; i < bars.Count; i++)
        {
            barTiltsDeg[i] = Random.Range(perBarTiltDegRange.x, perBarTiltDegRange.y);
            barColors[i] = JitterColorHSV(baseColor);
        }

        Random.state = oldState;
    }

    void ApplyPerBarAppearance()
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();

        for (int i = 0; i < bars.Count; i++)
        {
            Transform t = bars[i];

            // 1) Tilt each bar individually (local Z rotation)
            t.localRotation = Quaternion.Euler(0f, 0f, barTiltsDeg[i]);

            // 2) Color each bar individually via MaterialPropertyBlock
            // IMPORTANT: use Renderer on the bar object (or its children)
            Renderer r = t.GetComponent<Renderer>();
            if (r == null) r = t.GetComponentInChildren<Renderer>();
            if (r == null) continue;

            mpb.Clear();
            SetAnyColorProperty(mpb, barColors[i]);
            r.SetPropertyBlock(mpb);
        }
    }

    void SetAnyColorProperty(MaterialPropertyBlock block, Color c)
    {
        // Try the provided property names (works across pipelines/shaders if names match).
        for (int i = 0; i < colorPropertyNames.Length; i++)
        {
            string prop = colorPropertyNames[i];
            if (!string.IsNullOrEmpty(prop))
                block.SetColor(prop, c);
        }
    }

    Color PickTrialBaseColor()
    {
        if (colorPalette != null && colorPalette.Count > 0)
            return colorPalette[Random.Range(0, colorPalette.Count)];

        float h = Random.value;
        float s = Random.Range(hsvSaturationRange.x, hsvSaturationRange.y);
        float v = Random.Range(hsvValueRange.x, hsvValueRange.y);
        return Color.HSVToRGB(h, s, v);
    }

    Color JitterColorHSV(Color baseColor)
    {
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);

        h = Mathf.Repeat(h + Random.Range(-hueJitter, hueJitter), 1f);
        s = Mathf.Clamp01(s + Random.Range(-saturationJitter, saturationJitter));
        v = Mathf.Clamp01(v + Random.Range(-valueJitter, valueJitter));

        return Color.HSVToRGB(h, s, v);
    }

    // --------------------------
    // Angle conversions
    // --------------------------

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
