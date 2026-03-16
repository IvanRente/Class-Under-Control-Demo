using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp", sourceClassName: "PlayerItemSystem+ItemDefinition")]
[Serializable]
public class PlayerItemDefinition
{
    public PlayerItemSystem.ItemId itemId;
    public string displayName;
    public int price = 25;
    public GameObject equipPrefab;
    public Transform equipAnchorOverride;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale = Vector3.one;
    public Color uiColor = new Color(0.9f, 0.85f, 0.3f, 1f);
    public float maxDurability = -1f;
    public float durabilityCostPerUse = -1f;
    public float durabilityCostPerSecond = -1f;
}
