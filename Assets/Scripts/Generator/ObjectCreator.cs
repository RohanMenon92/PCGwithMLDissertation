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

    private void OnValidate()
    {
        RegeneratePoints();
    }

    private void RegeneratePoints()
    {
        points = PoissonDiscSampling.GeneratePoints(radius, regionSize, rejectionSamples);
    }

    public void OnDrawGizmos()//TerrainChunk chunk)
    {
        Gizmos.DrawWireCube(regionSize / 2, regionSize);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(regionSize.x, 0, regionSize.y));
        if(points != null)
        {
            foreach(Vector2 point in points)
            {
                Gizmos.DrawSphere(point, displayRadius);
            }
        }
    }

    public void OnCreateObjectsForChunk(TerrainChunk chunk, Vector2 terrainChunkBounds)
    {
        float newSizeX = terrainChunkBounds.x / regionSize.x;
        float newSizeY = terrainChunkBounds.y / regionSize.y;

        Debug.Log("Bounds Size - X:" + terrainChunkBounds.x + " y:" + terrainChunkBounds.y);
        points = PoissonDiscSampling.GeneratePoints(radius, regionSize, rejectionSamples);

        foreach(Vector2 point in points)
        {
            RaycastHit raycastHit;
            // Fire a ray going down
            if (Physics.Raycast(new Vector3(chunk.chunkPosition.x + (point.x * newSizeX), 200, chunk.chunkPosition.y + (point.y * newSizeY)), new Vector3(0, -1, 0), out raycastHit)) {
                //Debug.DrawRay(new Vector3(chunk.chunkPosition.x + point.x, 200, chunk.chunkPosition.y + point.y), new Vector3(0, -1, 0));

                // If it collides with a terrainChunk
                if (raycastHit.transform.name.Contains("TerrainChunk"))
                {
                    Debug.Log("Creating Object for chunk " + raycastHit.collider.gameObject.name);
                    GameObject.Instantiate(spawnObjectPrefab, raycastHit.point, Quaternion.identity, raycastHit.collider.transform);
                }
            }
        }
    }
}
