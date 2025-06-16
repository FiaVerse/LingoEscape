using UnityEngine;
using UnityEngine.Events; 

public class TriggerVolume : MonoBehaviour
{
    public string triggerName = "Trigger Volume";

    [Header("Trigger Events")]
    public UnityEvent onPlayerEnter;
    public UnityEvent onPlayerExit;


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"The Player has entered the '{triggerName}' trigger volume.");
            
            onPlayerEnter.Invoke();
        }
    }




    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"The Player has exited the '{triggerName}' trigger volume.");
            
            onPlayerExit.Invoke();
        }
       
    }
}