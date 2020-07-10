using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapDisplay : MonoBehaviour
{
    public Renderer textureRender;

    public MeshFilter meshFilter;
    public MeshRenderer meshRednerer;

    private void Start()
    {
        // Hide preview Assets
        textureRender.gameObject.SetActive(false);
        meshRednerer.gameObject.SetActive(false);
    }

    public void DrawTexture(Texture2D texture)
    {
        textureRender.sharedMaterial.mainTexture = texture;
        textureRender.transform.localScale = new Vector3(texture.width, 1, texture.height);
    }

    public void DrawMesh(MeshData meshData, Texture2D texture)
    {
        meshFilter.sharedMesh = meshData.CreateMesh();
        meshRednerer.sharedMaterial.mainTexture = texture;
    }

    public void ShowTexturePreview()
    {
        if (!textureRender.gameObject.activeSelf)
        {
            textureRender.gameObject.SetActive(true);
            meshRednerer.gameObject.SetActive(false);
        }
    }

    public void ShowMeshPreview()
    {
        if (!meshRednerer.gameObject.activeSelf)
        {
            meshRednerer.gameObject.SetActive(true);
            textureRender.gameObject.SetActive(false);
        }
    }
}
