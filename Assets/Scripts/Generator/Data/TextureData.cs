using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class TextureData : UpdatableData
{
    public void ApplyToMaterial(Material material)
    {

    }

    public void UpdateMeshHeights(Material material, float minHeight, float maxHeight)
    {
        Debug.Log("MinHeight:" + minHeight + "::MaxHeight:" + maxHeight);
        material.SetFloat("MinHeight", minHeight);
        material.SetFloat("MaxHeight", maxHeight);
    }
}
