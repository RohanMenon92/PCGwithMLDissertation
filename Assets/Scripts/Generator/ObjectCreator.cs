using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectCreator : MonoBehaviour
{
    public enum TreeTypes
    {
        Low,
        Mid,
        High
    }

    [Header("Poisson Distribution Controller")]
    public float radius = 2;
    public Vector2 regionSize = Vector2.one * 10;
    public int rejectionSamples = 300;
    public float displayRadius = 1;

    [Header("Tree Data")]
    public int treesPoolSize = 50;

    public float treeLowHeight = 5;
    public float treeMidHeight = 10;

    public List<GameObject> lowTrees;
    public List<GameObject> midTrees;
    public List<GameObject> highTrees;

    public Transform unusedLowTreesPool;
    public Transform unusedMidTreesPool;
    public Transform unusedHighTreesPool;


    List<Vector2> points;
    MapPreview mapPreview;

    void Start()
    {
        for (int i = 0; i <= treesPoolSize; i++)
        {
            foreach (GameObject lowPrefab in lowTrees)
            {
                GameObject newTree = Instantiate(lowPrefab, unusedLowTreesPool);
                newTree.SetActive(false);
            }
            foreach (GameObject midPrefab in midTrees)
            {
                GameObject newTree = Instantiate(midPrefab, unusedMidTreesPool);
                newTree.SetActive(false);
            }
            foreach (GameObject highPrefab in highTrees)
            {
                GameObject newTree = Instantiate(highPrefab, unusedHighTreesPool);
                newTree.SetActive(false);
            }
        }

    }

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
                OnCreateTreesForPreviewChunk();
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
    public void OnCreateTreesForPreviewChunk()
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
                    GameObject spawnObjectPrefab = null;

                    TreeTypes treeTypeToSpawn = TreeTypes.Low;
                    if (raycastHit.point.y < treeLowHeight)
                    {
                        treeTypeToSpawn = TreeTypes.Low;
                        spawnObjectPrefab = lowTrees[UnityEngine.Random.Range(0, lowTrees.Count - 1)];
                    }
                    else if (raycastHit.point.y < treeMidHeight)
                    {
                        treeTypeToSpawn = TreeTypes.Mid;
                        spawnObjectPrefab = midTrees[UnityEngine.Random.Range(0, midTrees.Count - 1)];
                    }
                    else
                    {
                        treeTypeToSpawn = TreeTypes.High;
                        spawnObjectPrefab = highTrees[UnityEngine.Random.Range(0, highTrees.Count - 1)];
                    }

                    GameObject.Instantiate(spawnObjectPrefab, raycastHit.point, Quaternion.identity, raycastHit.collider.transform);
                }
            }
        }
    }
#endif

    public GameObject GetTree(TreeTypes treeLevel)
    {
        GameObject treeObject = null;
        // Get Tree From pool and return it
        
        switch (treeLevel)
        {

            // Get First Child, set parent to gunport (to remove from respective pool)
            case TreeTypes.Low:
                treeObject = unusedLowTreesPool.GetChild(UnityEngine.Random.Range(0, unusedLowTreesPool.childCount - 1)).gameObject;
                break;
            case TreeTypes.Mid:
                treeObject = unusedMidTreesPool.GetChild(UnityEngine.Random.Range(0, unusedMidTreesPool.childCount - 1)).gameObject;
                break;
            case TreeTypes.High:
                treeObject = unusedHighTreesPool.GetChild(UnityEngine.Random.Range(0, unusedHighTreesPool.childCount - 1)).gameObject;
                break;
        }

        // Return bullet and let GunPort handle how to fire and set initial velocities
        return treeObject;
    }

    // Returning Trees to pool
    public void ReturnTreeToPool(GameObject treeToStore, TreeTypes treeType)
    {
        if (treeType == TreeTypes.Low)
        {
            // Return to normal bullet pool
            treeToStore.transform.SetParent(unusedLowTreesPool);
        }
        else if (treeType == TreeTypes.Mid)
        {
            // Return to shotgun bullet pool
            treeToStore.transform.SetParent(unusedMidTreesPool);
        }
        else if (treeType == TreeTypes.High)
        {
            // Return to laser bullet pool
            treeToStore.transform.SetParent(unusedHighTreesPool);
        }
        treeToStore.gameObject.SetActive(false);
        treeToStore.transform.localScale = Vector3.one;
        treeToStore.transform.position = Vector3.one * -100;
    }

    public void OnCreateTreesForChunk(TerrainChunk chunk)
    {
        if(chunk.hasObjects)
        {
            return;
        }
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
                    TreeTypes treeTypeToSpawn = TreeTypes.Low;
                    if(raycastHit.point.y < treeLowHeight)
                    {
                        treeTypeToSpawn = TreeTypes.Low;
                    } else if(raycastHit.point.y < treeMidHeight)
                    {
                        treeTypeToSpawn = TreeTypes.Mid;
                    } else
                    {
                        treeTypeToSpawn = TreeTypes.High;
                    }

                    GameObject newTree = GetTree(treeTypeToSpawn);
                    newTree.transform.SetParent(raycastHit.collider.transform);
                    newTree.transform.position = raycastHit.point;
                    newTree.SetActive(true);
                    //GameObject.Instantiate(spawnObjectPrefab, raycastHit.point, Quaternion.identity, raycastHit.collider.transform);
                }
            }
        }

        chunk.hasObjects = true;
    }

    public void ReturnChunkObjectsToPool(TerrainChunk chunk)
    {
        if(!chunk.hasObjects)
        {
            return;
        }

        foreach (Transform trans in mapPreview.meshFilter.transform)
        {
            TreeTypes treeTypeToSpawn = TreeTypes.Low;

            if (trans.position.y < treeLowHeight)
            {
                treeTypeToSpawn = TreeTypes.Low;
            }
            else if (trans.position.y < treeMidHeight)
            {
                treeTypeToSpawn = TreeTypes.Mid;
            }
            else
            {
                treeTypeToSpawn = TreeTypes.High;
            }

            ReturnTreeToPool(trans.gameObject, treeTypeToSpawn);
        }

        chunk.hasObjects = false;
    }
}
