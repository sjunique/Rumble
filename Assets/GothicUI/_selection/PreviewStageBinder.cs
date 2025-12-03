// PreviewStageBinder.cs
using UnityEngine;
using System.Collections;

public class PreviewStageBinder : MonoBehaviour
{
    [SerializeField] Transform characterStage;
    [SerializeField] Transform vehicleStage;
    [SerializeField] SelectionScreenController selection;

    void Awake()
    {
        if (!selection) selection = FindObjectOfType<SelectionScreenController>(true);
    }

    void OnEnable()
    {
        if (selection)
        {
            selection.OnCharacterChanged += _ => StartCoroutine(NextFrame(() => ReparentClosestTo(characterStage)));
            selection.OnVehicleChanged   += _ => StartCoroutine(NextFrame(() => ReparentClosestTo(vehicleStage)));
        }
        StartCoroutine(NextFrame(() => ReparentClosestTo(characterStage)));
        StartCoroutine(NextFrame(() => ReparentClosestTo(vehicleStage)));
    }

    IEnumerator NextFrame(System.Action a) { yield return null; a?.Invoke(); }

    void ReparentClosestTo(Transform stage)
    {
        if (!stage) return;

        // Find a likely preview root near the stage that isn't already under it
        GameObject best = null; float bestScore = float.NegativeInfinity;
        foreach (var tr in stage.root.GetComponentsInChildren<Transform>(false))
        {
            if (tr == stage || tr.parent == stage) continue;
            if (!tr.gameObject.activeInHierarchy) continue;

            float score = -Vector3.SqrMagnitude(tr.position - stage.position);
            if (score > bestScore) { bestScore = score; best = tr.gameObject; }
        }

        if (!best) return;

        var t = best.transform;
        t.SetParent(stage, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;

        // Ensure the preview-disabler is present even if prefab was missing it
        if (!best.GetComponent<PreviewBootstrap>())
            best.AddComponent<PreviewBootstrap>();
    }
}
