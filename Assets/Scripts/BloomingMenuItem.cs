using UnityEngine;

public class BloomingMenuItem : MonoBehaviour
{
    public BloomMenu hub;

    public void Choose()
    {
        if (hub) hub.MenuItemChosen(transform);
    }
}