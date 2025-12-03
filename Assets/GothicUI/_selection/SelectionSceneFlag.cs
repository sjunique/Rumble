// SelectionSceneFlag.cs
using UnityEngine;

// Run before most things
[DefaultExecutionOrder(-100000)]
public class SelectionSceneFlag : MonoBehaviour
{
    void Awake()  { PreviewMode.IsActive = true; }
    void OnDestroy() { PreviewMode.IsActive = false; }
}

public static class PreviewMode { public static bool IsActive = false; }

