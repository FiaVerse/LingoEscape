using UnityEngine;

public class MiniBloomController : MonoBehaviour
{
    public GameObject radialMenu;
    
    
    public void ToggleRadialMenu()
    {
        radialMenu.SetActive(!radialMenu.activeSelf);
    }

}