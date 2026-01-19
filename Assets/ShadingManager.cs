using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShadingManager : MonoBehaviour
{
    void Start()
    {
    }
    
    public void SetEyePosition(int eyePosition)
    {
        // where 0 is left eye and 1 is right eye
        if (eyePosition == 1)
        {
            transform.localPosition = new Vector3(0.2f, 0, 0.012f);
        }
        else if (eyePosition == 0)
        {
            transform.localPosition = new Vector3(-0.2f, 0, 0.012f);
        }
    }
}
