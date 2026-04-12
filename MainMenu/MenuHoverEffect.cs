using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class MenuHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TextMeshProUGUI text;
    private string originalText;

    void Start()
    {
        originalText = text.text;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        text.text = ">> " + originalText;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        text.text = originalText;
    }
}