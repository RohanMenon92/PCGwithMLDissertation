﻿using System.Collections;
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

    // For updatable data Onvalidate
    protected override void OnValidate()
    {
        base.OnValidate();
    }
}
