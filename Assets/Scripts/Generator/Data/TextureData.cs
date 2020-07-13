using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class TextureData : UpdatableData
{
    //public void ApplyMaterial(Material material)
    //{
    //    //ApplyToMa
    //}
    public void UpdateMeshHeights(Material material, float minHeight, float maxHeight)
    {
        Debug.Log("Update Mesh Height : " + minHeight + " : " + maxHeight);
        material.SetFloat("MinHeight", minHeight);
        material.SetFloat("MaxHeight", maxHeight);
    }
}
