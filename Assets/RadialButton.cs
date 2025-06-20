using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class RadialButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Image icon;
    public Color normalColor = Color.white;
    public Color highlightColor = Color.cyan;

    void Start()
    {
        icon.color = normalColor;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        icon.color = highlightColor;
    }

    public void OnPointerExit(PointerEventData e)
    {
        icon.color = normalColor;
    }

    public void OnPointerClick(PointerEventData e)
    {
        Debug.Log($"Clicked {gameObject.name}");
        // Add your callback logic here
    }
}