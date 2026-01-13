using UnityEngine;

public class PerEyeBrightnessController : MonoBehaviour
{
    [Header("Brightness Controls")]
    [Range(0f, 1f)]
    public float leftEyeBrightness = 1.0f;
    
    [Range(0f, 1f)]
    public float rightEyeBrightness = 1.0f;
    
    [Header("References")]
    public Material brightnessMaterial;
    
    void Start()
    {
        // If no material assigned, create one
        if (brightnessMaterial == null)
        {
            brightnessMaterial = new Material(Shader.Find("Custom/EyeBrightnessControl"));
        }
    }
    
    void Update()
    {
        // Update shader properties
        brightnessMaterial.SetFloat("_LeftBrightness", leftEyeBrightness);
        brightnessMaterial.SetFloat("_RightBrightness", rightEyeBrightness);
    }
    
    // This is called by Unity's camera after rendering
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination, brightnessMaterial);
    }
    
    /// <summary>
    /// Sets the individual eye brightness of VR camera
    /// </summary>
    /// <param name="eyeIndex">0 for left eye, 1 for right eye</param>
    /// <param name="brightness">Any value between 0 and 1, where 1 is full brightness and 0 is fully dimmed</param>
    public void SetEyeBrightness(int eyeIndex, float brightness)
    {
        if (eyeIndex == 0)
        {
            leftEyeBrightness = Mathf.Clamp(brightness, 0f, 1f);
        }
        else if (eyeIndex == 1)
        {
            rightEyeBrightness = Mathf.Clamp(brightness, 0f, 1f);
        }
    }
}