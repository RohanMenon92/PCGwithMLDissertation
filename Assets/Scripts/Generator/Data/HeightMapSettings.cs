using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class HeightMapSettings : UpdatableData
{
    public float minHeight
    {
        get
        {
            return heightMultiplier * heightCurve.Evaluate(0);
        }
    }

    public float maxHeight
    {
        get
        {
            return heightMultiplier * heightCurve.Evaluate(1);
        }
    }

    [Header("Height Curve Parameters")]
    public float heightMultiplier;
    public AnimationCurve heightCurve;

    public bool useFalloff;
    public AnimationCurve falloffCurve;

    public NoiseSettings noiseSettings;

#if (UNITY_EDITOR)
    // For updatable data Onvalidate
    protected override void OnValidate()
    {
        noiseSettings.ValidateValues();
        base.OnValidate();
    }
#endif
}
