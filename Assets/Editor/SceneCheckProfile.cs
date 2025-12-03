using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Tools/Scene Check/Profile", fileName="SceneCheckProfile")]
public class SceneCheckProfile : ScriptableObject
{
    [System.Serializable]
    public class Tracked
    {
        public string label;
        public GameObject target;
        public bool includeChildren = false;
    }

    public List<Tracked> items = new();
}

