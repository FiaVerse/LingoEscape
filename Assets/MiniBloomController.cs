using UnityEngine;

public class MiniBloomController : MonoBehaviour
{
    public GameObject radialMenu;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            radialMenu.SetActive(!radialMenu.activeSelf);
    }
    
    public void ToggleRadialMenu()
    {
        radialMenu.SetActive(!radialMenu.activeSelf);
    }

}