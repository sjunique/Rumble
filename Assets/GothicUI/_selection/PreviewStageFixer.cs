// Assets/_Selection/PreviewStageFixer.cs
using UnityEngine;

public class PreviewStageFixer : MonoBehaviour
{
    [SerializeField] Transform characterStage;
    [SerializeField] Transform vehicleStage;

    void LateUpdate()
    {
        // if something spawned without parenting, snap it under the stage once
        if (characterStage)
        {
            var child = FindTopMostWithTag("CharacterPreview"); // or find by name/pattern
            if (child && child.transform.parent != characterStage)
                Reparent(child.transform, characterStage);
        }
        if (vehicleStage)
        {
            var child = FindTopMostWithTag("VehiclePreview");
            if (child && child.transform.parent != vehicleStage)
                Reparent(child.transform, vehicleStage);
        }
    }

    GameObject FindTopMostWithTag(string tag)
    {
        var objs = GameObject.FindGameObjectsWithTag(tag);
        return objs.Length > 0 ? objs[0] : null;
    }

    void Reparent(Transform t, Transform parent)
    {
        t.SetParent(parent, false);
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale    = Vector3.one;
        enabled = false; // do once
    }
}

