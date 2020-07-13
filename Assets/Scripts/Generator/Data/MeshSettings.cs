using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class MeshSettings : UpdatableData
{
    public const int numSupportedLODs = 5;
    public const int numSupportedChunkSizes = 9;
    public const int numSupportedFlatShadedChunkSizes = 3;

    // Chunk Size for regular terrain
    // 241 - 1 = 240 is divisible by a lot more factors than the unity limit for vertex chunk size (255)
    // 239 Because we are adding 2 vertices for the padding
    public static readonly int[] supportedMeshSizes = { 48, 72, 96, 120, 144, 168, 192, 216, 240 };

    // Minimum Chunk size for flat shading
    // 96 - 1 = 95 is not divisible by 5,  
    // limit for vertex chunk size (255) but we create a lot more vertexes when using flat shading(each triangle is independent)
    //public static readonly int[] supportedFlatShadedMeshSizes = { 48, 72, 96 };

    public float terrainScale = 1f;
    public bool useFlatShading;

    [Range(0, numSupportedChunkSizes - 1)]
    public int chunkSizeIndex;
    [Range(0, numSupportedFlatShadedChunkSizes - 1)]
    public int flatShadedChunkSizeIndex;

    // Return different values for mapChunkSize when using flatShading
    // Size includes 2 extra vertices for calculating normals at the edge hence + 1 instead of - 1 like earlier
    public int numVertexesPerLine
    {
        get
        {
            return supportedMeshSizes[useFlatShading ? flatShadedChunkSizeIndex : chunkSizeIndex] + 1;
        }
    }

    public float meshWorldSize
    {
        get
        {
            return (numVertexesPerLine - 3) * terrainScale;
        }
    }



#if (UNITY_EDITOR)
    // For updatable data Onvalidate
    protected override void OnValidate()
    {
        base.OnValidate();
    }
#endif
}
