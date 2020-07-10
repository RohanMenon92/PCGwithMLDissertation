using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MeshGenerator
{
    public static MeshData GenerateTerrainMesh(float[,] heightMap, float heightMultiplier, AnimationCurve heightCurve, int levelOfDetail)
    {
        // Create a new height Curve because curve evaluation will cause problems when accessed from different threads
        // Alternatively, lock the thread but it is slower
        AnimationCurve threadHeightCurve = new AnimationCurve(heightCurve.keys);

        // mesh LOD increment to ignore vertices
        int meshLODIncrement = (levelOfDetail == 0) ? 1 : levelOfDetail * 2;

        // For calculating vertex normals on the outer side of the mesh
        int borderedSize = heightMap.GetLength(0);
        // meshSize based on LOD Increment (for UVs, etc) with LOD
        int meshSize = borderedSize - 2 * meshLODIncrement;
        // meshSize without LOD Increment (for positions, etc)
        int meshSizeUnSimplified = borderedSize - 2;

        int[,] vertexIndicesMap = new int[borderedSize, borderedSize];
        int meshVertexIndex = 0;
        int borderVertexIndex = -1;

        float topLeftX = (meshSizeUnSimplified - 1) / -2f;
        float topLeftZ = (meshSizeUnSimplified - 1) / 2f;

        int verticesPerLine = (meshSize - 1) / meshLODIncrement + 1;

        MeshData meshData = new MeshData(verticesPerLine);

        for (int y = 0; y < borderedSize; y += meshLODIncrement)
        {
            for (int x = 0; x < borderedSize; x += meshLODIncrement)
            {
                bool isBorderVertex = y == 0 || y == borderedSize - 1 || x == 0 || x == borderedSize - 1;

                if(isBorderVertex)
                {
                    // border indices are negative values and are decremented as we iterate
                    vertexIndicesMap[x, y] = borderVertexIndex;
                    borderVertexIndex--;
                } else
                {
                    // mesh indices are positive values and are incremented as we iterate
                    vertexIndicesMap[x, y] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }
        }

        for (int y = 0; y < borderedSize; y+= meshLODIncrement)
        {
            for (int x = 0; x < borderedSize; x+= meshLODIncrement)
            {
                // Create a triangle vertex
                // Based on heightMap, heightMultiplier, heightCurve

                // Subtract by mesh simplicationIncrement to center UVs
                int vertexIndex = vertexIndicesMap[x, y];
                // Center by dividing x - LODIncrement by the mesh size(UV as a percentage of the width)
                Vector2 percentUV = new Vector2((x - meshLODIncrement) / (float)meshSize, (y - meshLODIncrement) / (float)meshSize);
                float height = threadHeightCurve.Evaluate(heightMap[x, y]) * heightMultiplier;
                Vector3 vertexPosition = new Vector3(topLeftX + percentUV.x * meshSizeUnSimplified, height, topLeftZ - percentUV.y * meshSizeUnSimplified);

                meshData.AddVertex(vertexPosition, percentUV, vertexIndex);

                // Skip edges
                if(x < borderedSize - 1 && y < borderedSize - 1)
                {
                    // Create triangle per square a,b,c,d
                    int a = vertexIndicesMap[x, y];
                    int b = vertexIndicesMap[x + meshLODIncrement, y];
                    int c = vertexIndicesMap[x, y + meshLODIncrement];
                    int d = vertexIndicesMap[x + meshLODIncrement, y + meshLODIncrement];

                    // Create triangle adc, dab
                    meshData.AddTriangle(a,d,c);
                    meshData.AddTriangle(d,a,b);
                }

                vertexIndex++;
            }
        }

        // For threading in unity, return meshdata in the thread and return all of it outside of it
        return meshData;
    }
}

// Class to create mesh, triangles, uvs, etc
public class MeshData {
    Vector3[] vertices;
    int[] triangles;
    Vector2[] uvs;

    Vector3[] borderVertices;
    int[] borderTriangles;

    int triangleIndex;
    int borderTriangleIndex;

    public MeshData(int verticesPerLine)
    {
        vertices = new Vector3[verticesPerLine * verticesPerLine];
        uvs = new Vector2[verticesPerLine * verticesPerLine];
        triangles = new int[(verticesPerLine - 1) * (verticesPerLine - 1) * 6];

        // vertices on the border
        borderVertices = new Vector3[verticesPerLine * 4 + 4];
        // no. of triangles on the border
        borderTriangles = new int[24 * verticesPerLine];
    }

    public void AddVertex(Vector3 vertexPosition, Vector2 uv, int vertexIndex)
    {
        if(vertexIndex < 0)
        {
            // Negative Vertex -> Border Vertex
            borderVertices[-vertexIndex - 1] = vertexPosition;
        } else
        {
            // Positive Index -> Mesh Vertex
            vertices[vertexIndex] = vertexPosition;
            uvs[vertexIndex] = uv;
        }
    }

    public void AddTriangle(int a, int b, int c)
    {
        if(a < 0 || b < 0 || c < 0)
        {
            // Negative index -> Border Triangle
            borderTriangles[borderTriangleIndex] = a;
            borderTriangles[borderTriangleIndex + 1] = b;
            borderTriangles[borderTriangleIndex + 2] = c;
            borderTriangleIndex += 3;
        } else
        {
            // Positive index -> Mesh Triangle
            triangles[triangleIndex] = a;
            triangles[triangleIndex + 1] = b;
            triangles[triangleIndex + 2] = c;
            triangleIndex += 3;
        }
    }

    Vector3[] CalculateNormals()
    {
        Vector3[] vertexNormals = new Vector3[vertices.Length];

        // Normals for mesh triangles
        for (int i = 0; i < triangles.Length / 3; i++)
        {
            int normalTriangleIndex = i * 3;
            int vertexIndexA = triangles[normalTriangleIndex];
            int vertexIndexB = triangles[normalTriangleIndex + 1];
            int vertexIndexC = triangles[normalTriangleIndex + 2];

            Vector3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
            vertexNormals[vertexIndexA] += triangleNormal;
            vertexNormals[vertexIndexB] += triangleNormal;
            vertexNormals[vertexIndexC] += triangleNormal;
        }
        // Normals for border triangles
        for (int i = 0; i < borderTriangles.Length / 3; i++)
        {
            int normalTriangleIndex = i * 3;
            int vertexIndexA = borderTriangles[normalTriangleIndex];
            int vertexIndexB = borderTriangles[normalTriangleIndex + 1];
            int vertexIndexC = borderTriangles[normalTriangleIndex + 2];

            Vector3 triangleNormal = SurfaceNormalFromIndices(vertexIndexA, vertexIndexB, vertexIndexC);
            if(vertexIndexA >= 0)
            {
                vertexNormals[vertexIndexA] += triangleNormal;
            }
            if (vertexIndexB >= 0)
            {
                vertexNormals[vertexIndexB] += triangleNormal;
            }
            if (vertexIndexC >= 0)
            {
                vertexNormals[vertexIndexC] += triangleNormal;
            }
        }

        foreach (Vector3 vertexNormal in vertexNormals) {
            vertexNormal.Normalize();
        }

        return vertexNormals;
    }

    Vector3 SurfaceNormalFromIndices(int indexA, int indexB, int indexC)
    {
        // Take from borderVertices if negative index or from regular meshVertices
        Vector3 pointA = indexA < 0 ? borderVertices[-indexA - 1] : vertices[indexA];
        Vector3 pointB = indexB < 0 ? borderVertices[-indexB - 1] : vertices[indexB];
        Vector3 pointC = indexC < 0 ? borderVertices[-indexC - 1] : vertices[indexC];

        // Unity calculates normals per triangle vertex, cross product of the 2 sides to the triangle
        return Vector3.Cross(pointB - pointA, pointC - pointA).normalized;
    }

    public Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.normals = CalculateNormals();
        return mesh;
    }
}
