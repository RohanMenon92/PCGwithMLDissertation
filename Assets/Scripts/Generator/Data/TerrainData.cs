using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class TerrainData : UpdatableData
{
    public float terrainScale = 1f;

    [Header("Curve Parameters")]
    public float heightMultiplier;
    public AnimationCurve heightCurve;

    public bool useFalloff;
    public AnimationCurve falloffCurve;

    public bool useFlatShading;

#if (UNITY_EDITOR)
    // For updatable data Onvalidate
    protected override void OnValidate()
    {
        base.OnValidate();
    }
#endif
    public float minHeight
    {
        get
        {
            return terrainScale * heightMultiplier * heightCurve.Evaluate(0);
        }
    }

    public float maxHeight
    {
        get
        {
            return terrainScale * heightMultiplier * heightCurve.Evaluate(1);
        }
    }
}
