using UnityEngine;

public class LayerToggleOnTrigger : MonoBehaviour
{
    [SerializeField] private int newLayer = 0;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Object entered trigger: {other.gameObject.name}");
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            return;
        }

        Debug.LogWarning($"Object exited trigger: {other.gameObject.name}");

        // Change layer
        other.gameObject.layer = 0;
        foreach (Transform child in other.transform)
        {
            child.gameObject.layer = 0;
        }

        // Unparent from hierarchy but keep its own children
        //
        //other.transform.SetParent(null);

        //Debug.Log($"Changed {other.gameObject.name} to layer {newLayer} and unparented it");
    }
}
