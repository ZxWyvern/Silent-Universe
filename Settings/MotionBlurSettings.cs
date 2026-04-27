using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MotionBlurSettings : MonoBehaviour
{
    [SerializeField] private Volume globalvolume;
    private MotionBlur motionBlur;

    private void Start()
    {
        if (globalvolume != null)
        {
            globalvolume.profile.TryGet<MotionBlur>(out motionBlur);
        }
    }

    public void MotionBlurOnOff()
    {
        if (motionBlur != null)
        {
            motionBlur.active = !motionBlur.active;
            Debug.Log("Motion Blur " + (motionBlur.active ? "enabled" : "disabled"));
        }
    }
}