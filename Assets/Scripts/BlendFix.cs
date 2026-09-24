using UnityEngine;

public class BlendFix : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
	    OVRManager.eyeFovPremultipliedAlphaModeEnabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
