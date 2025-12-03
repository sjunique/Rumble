 
// CameraGuard.cs
using UnityEngine;
using System;

[DefaultExecutionOrder(-999998)]
public class CameraGuard : MonoBehaviour
{
    void Awake()
    {
        if (!PreviewMode.IsActive) return;
        gameObject.tag = "Untagged"; // don't advertise MainCamera in preview
        StripRogueComponents();
    }

    void LateUpdate()
    {
        if (!PreviewMode.IsActive) return;
        StripRogueComponents();
    }

    void StripRogueComponents()
    {
        TryRemoveByFullName("Invector.vCamera.vThirdPersonCamera");
        TryRemoveByFullName("Cinemachine.CinemachineBrain");
    }

    void TryRemoveByFullName(string fullName)
    {
        var all = GetComponents<MonoBehaviour>();
        foreach (var c in all)
        {
            if (c == null) continue;
            var t = c.GetType();
            if (t.FullName == fullName)
                DestroyImmediate(c);
        }
    }
}




// [DefaultExecutionOrder(-99999)] // before other Awakes
// public class CameraGuard : MonoBehaviour
// {
//     void Awake()
//     {
//         if (!PreviewMode.IsActive) return;

//         // Stop Invector from “finding” this as the gameplay camera
//         gameObject.tag = "Untagged";   // don't present a MainCamera to Invector in Selection

//         // Remove any stray components if something added them already
//         RemoveInvectorBits();
//     }

//     void LateUpdate()
//     {
//         if (!PreviewMode.IsActive) return;
//         // If something adds them later in the frame, strip them again
//         RemoveInvectorBits();
//     }

//     void RemoveInvectorBits()
//     {
//         // vThirdPersonCamera
//         var tpc = GetComponent("Invector.vCamera.vThirdPersonCamera"); // fully-qualified name
//         if (tpc) DestroyImmediate(tpc);

//         // Cinemachine Brain (if it gets added)
//         var brain = GetComponent("Cinemachine.CinemachineBrain");
//         if (brain) DestroyImmediate(brain);
//     }
// }

