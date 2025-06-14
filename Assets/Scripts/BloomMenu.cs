using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// HUB: put this on the central bubble
[RequireComponent(typeof(MeshRenderer))]
public class BloomMenu : MonoBehaviour
{
    [Header("Layout")]
    public float radius = 0.25f;                
    public float animDuration = 0.25f;          
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Audio")]
    public AudioSource audioSource;             
    public AudioClip whooshClip;               

    readonly List<Transform> items = new();
    bool expanded, busy;
    MeshRenderer hubRenderer;

    void Awake()
    {
        hubRenderer = GetComponent<MeshRenderer>();

        /* collect direct children (menu bubbles) */
        foreach (Transform child in transform)
        {
            items.Add(child);
            child.gameObject.SetActive(false);      // hidden at start
        }
    }

    /* hook this to the hub’s Poke-Start event */
    public void ToggleMenu()
    {
        if (busy) return;
        StartCoroutine(expanded ? Collapse() : Expand());
    }

    /* called by individual bubbles (RadialMenuItem.Choose) */
    public void MenuItemChosen(Transform chosen)
    {
        if (busy) return;
        StartCoroutine(SwapAndCollapse(chosen));
    }

    /*──────────────────── expand / collapse ────────────────────*/
    IEnumerator Expand()
    {
        Debug.Log("expanding menu");
        busy = true;  PlayWhoosh();

        /* activate children & pre-calc target positions */
        int n = items.Count;
        foreach (var t in items) t.gameObject.SetActive(true);
        Vector3[] target = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            target[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
        }

        /* animate out */
        for (float dt = 0; dt < 1f; dt += Time.deltaTime / animDuration)
        {
            float k = ease.Evaluate(dt);
            for (int i = 0; i < n; i++)
                items[i].localPosition = Vector3.Lerp(Vector3.zero, target[i], k);
            yield return null;
        }

        expanded = true;  busy = false;
    }

    IEnumerator Collapse()
    {
        Debug.Log("collapsing menu");
        busy = true;  PlayWhoosh();

        int n = items.Count;
        Vector3[] origin = new Vector3[n];
        for (int i = 0; i < n; i++) origin[i] = items[i].localPosition;

        for (float dt = 0; dt < 1f; dt += Time.deltaTime / animDuration)
        {
            float k = ease.Evaluate(dt);
            for (int i = 0; i < n; i++)
                items[i].localPosition = Vector3.Lerp(origin[i], Vector3.zero, k);
            yield return null;
        }

        foreach (var t in items) t.gameObject.SetActive(false);
        expanded = false;  busy = false;
    }

    /*──────────────────── swap hub material & autocollapse ────────────────────*/
    IEnumerator SwapAndCollapse(Transform chosen)
    {
        if (expanded) yield return Collapse();

        /* swap looks (optional) */
        MeshRenderer rChosen = chosen.GetComponent<MeshRenderer>();
        if (hubRenderer && rChosen)
        {
            Material tmp = hubRenderer.sharedMaterial;
            hubRenderer.sharedMaterial = rChosen.sharedMaterial;
            rChosen.sharedMaterial = tmp;
        }
    }

    /*──────────────────────── helpers ────────────────────────*/
    void PlayWhoosh()
    {
        if (audioSource && whooshClip) audioSource.PlayOneShot(whooshClip);
    }
}