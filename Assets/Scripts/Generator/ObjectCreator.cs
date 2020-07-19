using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectCreator : MonoBehaviour
{
    public float radius = 2;
    public Vector2 regionSize = Vector2.one * 10;
    public int rejectionSamples = 300;
    public float displayRadius = 1;

    public GameObject spawnObjectPrefab;

    List<Vector2> points;
    MapPreview mapPreview;

    private void OnValidate()
    {
        RegeneratePoints();
        if (mapPreview == null)
        {
            mapPreview = FindObjectOfType<MapPreview>();
        }

        if (mapPreview != null)
        {
            // So that destroy Immediate can be called here
            UnityEditor.EditorApplication.delayCall += () =>
            {
                OnCreateObjectsForPreviewChunk();
            };
        }
    }

    private void RegeneratePoints()
    {
        points = PoissonDiscSampling.GeneratePoints(radius, regionSize, rejectionSamples);
    }

    public void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(regionSize / 2, regionSize);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(regionSize.x, 0, regionSize.y));
        if (points != null)
        {
            foreach (Vector2 point in points)
            {
                Gizmos.DrawSphere(point, displayRadius);
            }
        }
    }

#if UNITY_EDITOR
    public void OnCreateObjectsForPreviewChunk()
    {
        if (Application.isPlaying)
        {
            return;
        }
        // Find MapPreview
        if (mapPreview == null)
        {
            mapPreview = FindObjectOfType<MapPreview>();
        }
        if (mapPreview == null)
        {
            return;
        }

        Bounds terrarinBounds = new Bounds(new Vector2(mapPreview.meshFilter.transform.position.x, mapPreview.meshFilter.transform.position.z), Vector2.one * mapPreview.meshSettings.meshWorldSize);

        float newSizeX = terrarinBounds.size.x / regionSize.x;
        float newSizeY = terrarinBounds.size.y / regionSize.y;

        // Delete Old Objects
        foreach(Transform trans in mapPreview.meshFilter.transform)
        {
            GameObject.DestroyImmediate(trans.gameObject);
        }

        // Create new objects
        foreach (Vector2 point in points)
        {
            RaycastHit raycastHit;
            // Fire a ray going down
            // multiply by 0.45f to allow some buffer space between chunks
            if (Physics.Raycast(new Vector3((mapPreview.meshFilter.transform.position.x - terrarinBounds.size.x * 0.45f) + (point.x * newSizeX), 200, (mapPreview.meshFilter.transform.position.z - terrarinBounds.size.y * 0.45f) + (point.y * newSizeY)), new Vector3(0, -1, 0), out raycastHit))
            {
                //Debug.DrawRay(new Vector3(chunk.chunkPosition.x + point.x, 200, chunk.chunkPosition.y + point.y), new Vector3(0, -1, 0));

                // If it collides with a terrainChunk
                if (raycastHit.transform.name.Contains("ExampleMesh"))
                {
                    GameObject.Instantiate(spawnObjectPrefab, raycastHit.point, Quaternion.identity, raycastHit.collider.transform);
                }
            }
        }
    }
#endif

    public void OnCreateObjectsForChunk(TerrainChunk chunk)
    {
        float newSizeX = chunk.bounds.size.x / regionSize.x;
        float newSizeY = chunk.bounds.size.y / regionSize.y;

        points = PoissonDiscSampling.GeneratePoints(radius, regionSize, rejectionSamples);

        foreach(Vector2 point in points)
        {
            RaycastHit raycastHit;
            // Fire a ray going down
            // multiply by 0.45f to allow some buffer space between chunks
            if (Physics.Raycast(new Vector3((chunk.chunkPosition.x - chunk.bounds.size.x * 0.45f) + (point.x * newSizeX), 200, (chunk.chunkPosition.y - chunk.bounds.size.y * 0.45f) + (point.y * newSizeY)), new Vector3(0, -1, 0), out raycastHit)) {
                //Debug.DrawRay(new Vector3(chunk.chunkPosition.x + point.x, 200, chunk.chunkPosition.y + point.y), new Vector3(0, -1, 0));

                // If it collides with a terrainChunk
                if (raycastHit.transform.name.Contains("TerrainChunk"))
                {
                    GameObject.Instantiate(spawnObjectPrefab, raycastHit.point, Quaternion.identity, raycastHit.collider.transform);
                }
            }
        }
    }
}
