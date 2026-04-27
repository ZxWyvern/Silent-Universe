using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Renderimage : MonoBehaviour
{
    [SerializeField]private Volume globalVolume;

    public void Renderimageonandoff()
    {
        if (globalVolume == null)
        {
            return;
        }
        globalVolume.enabled = !globalVolume.enabled;
    }
}
