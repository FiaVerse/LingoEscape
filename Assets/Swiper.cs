using UnityEngine;
using UnityEngine.UI;   

public class Swiper : MonoBehaviour
{
    [SerializeField] private GameObject[] buttons; // 0 = Beginner, 1 = Intermediate, 2 = Advanced
    private int _index;

    void Awake()
    {
        // keep only the first button visible at start
        for (int i = 1; i < buttons.Length; i++) buttons[i].SetActive(false);
    }

    public void NextButton()
    {
        buttons[_index].SetActive(false);          // hide current
        _index = (_index + 1) % buttons.Length;    // advance & wrap
        buttons[_index].SetActive(true);           // show next
    }
}