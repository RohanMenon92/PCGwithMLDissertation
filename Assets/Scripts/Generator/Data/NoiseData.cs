using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class NoiseData : UpdatableData
{
    [Header("Noise Parameters")]
    // ML STUFF: These Values will be modified by the ML agent to create different terrain maps
    // Generate Multiple Terrain Chunks after setting noiseNormalized to GLOBAL
    // The ML agent will define which terrain is better based on % that is navigable, sloping, less percentage of areas accessible, etc
    // This can be done by just the "noiseEstimatorVariable" or changing all the NoiseMap parameters as well
    // Make sure there aren't too many plateaus or cut offs
    // Will ensure generated areas are better for navigation and also to showcase all regions
    public Noise.NormalizeMode noiseNormalized;
    public float noiseEstimatorVariable;
    public float noiseScale;

    [Min(0)]
    public int octaves;
    [Range(0, 1)]
    public float persistence;
    [Min(1)]
    public float lacunarity;
    public int seed;
    public Vector2 offset;

#if (UNITY_EDITOR)
    // For updatable data Onvalidate
    protected override void OnValidate()
    {
        base.OnValidate();
    }
#endif
}
