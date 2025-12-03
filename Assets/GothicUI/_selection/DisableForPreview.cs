using UnityEngine;
 
using System.Linq;

public class DisableForPreview : MonoBehaviour
{
    [Tooltip("If true, all found components will be disabled on Awake.")]
    public bool autoApplyOnAwake = true;

    // Types to disable in preview
    static readonly string[] typeNamesToDisable = {
        // Invector character + inputs
        "vThirdPersonInput","vThirdPersonController","vThirdPersonMotor",
        "vMeleeCombatInput","vShooterMeleeInput","vShooterManager","vLockOnTargetControl",
        // Invector health/inventory/items
        "vHealthController","vItemManager","vInventory","vAmmoManager","vMeleeManager",
        // Cameras
        "vThirdPersonCamera",
        // Common Unity components that cause simulation
        "NavMeshAgent",
    };

    public void Apply()
    {
        // 1) Disable behaviours by type name (no direct assembly refs needed)
        var behaviours = GetComponentsInChildren<Behaviour>(true);
        foreach (var b in behaviours)
        {
           // Debug.Log(b.GetType().Name);
            if (!b) continue;
            var tname = b.GetType().Name;
            Debug.Log(" applying " + tname);
            if (typeNamesToDisable.Contains(tname))
                b.enabled = false;
        }

        // 2) Physics off (so previews don't push each other or fall)
        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        // 3) Optional: keep Animator playing idle (nice rotation)
        var anim = GetComponentInChildren<Animator>(true);
        if (anim)
        {
            anim.applyRootMotion = false;
            anim.updateMode = AnimatorUpdateMode.Normal;
            anim.enabled = true; // leave on to show idle pose
        }

        // 4) (If there is any audio) mute AudioSources
        foreach (var a in GetComponentsInChildren<AudioSource>(true))
            a.mute = true;
    }

    void Awake()
    {
        if (autoApplyOnAwake) Apply();
    }
}
