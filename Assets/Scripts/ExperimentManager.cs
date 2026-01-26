using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public enum DominantEye
{
    left,
    right
}

public class ExperimentManager : MonoBehaviour
{
    [Header("Stimuli")]
    public MovingBarBandStimulus movingBar;        // controller
    public int RepeatsPerCondition = 2;

    [Header("Stimulus Speed")]
    public float speedDegPerSec = 20f;

    [Header("Participant Name")]
    [Tooltip("Will be used for the output filename: Pulfrich_<contestantName>.csv")]
    public string contestantName = "test";

    [Header("Eye configuration")]
    public DominantEye dominantEye = DominantEye.right;
    public ShadingManager shadingManager;
    public Camera camera;

    [Header("Inter-Trial Screen")]
    public GameObject grayScreen;
    public float interTrialInterval = 1.0f;

    int trialIndex = -1;
    bool awaitingResponse = false;
    string outputFileName;
    
    float[] distancesMeters = new float[]
    {
        -0.5f,
        -0.1f,
        0f,
        1f,
        3f,
        6f,
        9f
    };

    ////////////////////////
    /// experiment control
    ////////////////////////

    void Start()
    {
        if (movingBar == null)
        {
            Debug.LogError("ExperimentManager: movingBar not assigned.");
            enabled = false;
            return;
        }

        // Ensure the stimulus uses THIS camera for correct distance placement & FOV
        if (camera != null)
        movingBar.stimulusCamera = camera;

        // Set position of eye shading
        shadingManager.SetEyePosition(Convert.ToInt32(dominantEye));
        
        SetupOutputFile();
        StartCoroutine(RunExperiment());
    }

    IEnumerator RunExperiment()
    {
        for (int i = 0; i < RepeatsPerCondition; i++)
        {
            ShuffleDistances();   //randomize trial order
            Debug.Log($"== Block {i + 1}/{RepeatsPerCondition} ==");
            for (trialIndex = 0; trialIndex < distancesMeters.Length; trialIndex++)
            {
                float distance = distancesMeters[trialIndex];

                // Hide gray screen, show stimulus
                if (grayScreen != null)
                    grayScreen.SetActive(false);

                movingBar.gameObject.SetActive(true);

                movingBar.Configure(
                    newSignedOffsetMeters: distance,
                    newDirection: GetStimulusDirection(),
                    newSpeedDegPerSec: speedDegPerSec
                );

                awaitingResponse = true;

                // Wait for response
                while (awaitingResponse)
                    yield return null;

                // ---- Inter-trial interval ----
                movingBar.gameObject.SetActive(false);

                if (grayScreen != null)
                    grayScreen.SetActive(true);

                yield return new WaitForSeconds(interTrialInterval);
            }

        }

        // End of experiment
        if (grayScreen != null)
            grayScreen.SetActive(true);

        Debug.Log("Experiment finished.");
    }


    void Update()
    {
        if (!awaitingResponse)
            return;

        // behind = Up Arrow
        if (Input.GetKeyDown(KeyCode.UpArrow))
            SubmitResponse(true);
        // before = Down Arrow
        else if (Input.GetKeyDown(KeyCode.DownArrow))
            SubmitResponse(false);
    }

    ///////////////////////
    /// manipulate stimuli
    ///////////////////////

    int GetStimulusDirection()
    {
        // Convention:
        //  1  = left → right
        // -1  = right → left

        return dominantEye == DominantEye.right ? 1 : -1;
    }

    void ShuffleDistances()
    {
        for (int i = 0; i < distancesMeters.Length; i++)
        {
            int j = UnityEngine.Random.Range(i, distancesMeters.Length);
            float temp = distancesMeters[i];
            distancesMeters[i] = distancesMeters[j];
            distancesMeters[j] = temp;
        }
    }

    ///////////////////////
    // data documentation
    ///////////////////////

    void SubmitResponse(bool positionBehind)
    {
        if (!awaitingResponse)
            return;

        awaitingResponse = false;

        float distance = distancesMeters[trialIndex];

        // Write response (distance + behind/front) to file
        WriteLine(distance, positionBehind);

        Debug.Log(
            $"Trial {trialIndex + 1}/{distancesMeters.Length} | " +
            $"Distance = {distance:F2} m | Response = {(positionBehind ? "BEHIND" : "FRONT")}"
        );
    }

    void SetupOutputFile()
    {
        string outputDir = Path.Combine(Application.dataPath, "..", "Measurements");
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        // Sanitize name for filesystem safety
        string safeName = string.IsNullOrWhiteSpace(contestantName) ? "unknown" : contestantName.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            safeName = safeName.Replace(c.ToString(), "_");

        // name with contestant and timestamp
        outputFileName = $"Pulfrich_{safeName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv";

        string path = Path.Combine(outputDir, outputFileName);

        // Header: distance + response (and trial number)
        File.WriteAllText(path, "trial,distance_m,response\n");

        Debug.Log($"Logging to: {path}");
    }

    void WriteLine(float distance, bool positionBehind)
    {
        string outputDir = Path.Combine(Application.dataPath, "..", "Measurements");
        string path = Path.Combine(outputDir, outputFileName);

        int trialNumber = trialIndex + 1;
        string response = positionBehind ? "BEHIND" : "BEFORE";

        File.AppendAllText(path, $"{trialNumber},{distance:F3},{response}\n");
    }

}
