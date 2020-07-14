using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class MeshGenerator
{
    // Getting called from a seperate thread
    public static MeshData GenerateTerrainMesh(float[,] heightMap, int levelOfDetail, MeshSettings meshSettings)
    {
        // mesh LOD increment to ignore vertices
        int meshSkipIncrement = (levelOfDetail == 0) ? 1 : levelOfDetail * 2;

        int numVertsPerLine = meshSettings.numVertsPerLine;

        // For calculating vertex normals on the outer side of the mesh
        //int borderedSize = heightMap.GetLength(0);
        //// meshSize based on LOD Increment (for UVs, etc) with LOD
        //int meshSize = borderedSize - 2 * meshLODIncrement;
        //// meshSize without LOD Increment (for positions, etc)
        //int meshSizeUnSimplified = borderedSize - 2;

        int[,] vertexIndicesMap = new int[numVertsPerLine, numVertsPerLine];
        int meshVertexIndex = 0;
        int outOfMeshVertexIndex = -1;

        Vector2 topLeft = new Vector2(-1, 1) * meshSettings.meshWorldSize / 2f;

        MeshData meshData = new MeshData(numVertsPerLine, meshSkipIncrement, meshSettings.useFlatShading);

        // Border Vertex calculation
        for (int y = 0; y < numVertsPerLine; y++)
        {
            for (int x = 0; x < numVertsPerLine; x++)
            {
                // If its out of the rendered mesh
                bool isOutOfMesVertex = y == 0 || y == numVertsPerLine - 1 || x == 0 || x == numVertsPerLine - 1;

                // Only vertices on the 2 puter most edges in this group and they are not mainVertices(Hence check if the vertex is divisible by the meshSkipIncrement)
                bool isSkippedVertex = x > 2 && x < numVertsPerLine - 3 && y > 2 && y < numVertsPerLine - 3 && ((x - 2) % meshSkipIncrement != 0 || (y - 2) % meshSkipIncrement != 0);

                if(isOutOfMesVertex)
                {
                    // border indices are negative values and are decremented as we iterate
                    vertexIndicesMap[x, y] = outOfMeshVertexIndex;
                    outOfMeshVertexIndex--;
                } else if(!isSkippedVertex)
                {
                    // mesh indices are positive values and are incremented as we iterate
                    vertexIndicesMap[x, y] = meshVertexIndex;
                    meshVertexIndex++;
                }
            }
        }

        for (int y = 0; y < numVertsPerLine; y++)
        {
            for (int x = 0; x < numVertsPerLine; x++)
            {
                // Only vertices on the 2 puter most edges in this group and they are not mainVertices(Hence check if the vertex is divisible by the meshSkipIncrement)
                bool isSkippedVertex = x > 2 && x < numVertsPerLine - 3 && y > 2 && y < numVertsPerLine - 3 && ((x - 2) % meshSkipIncrement != 0 || (y - 2) % meshSkipIncrement != 0);

                if (!isSkippedVertex)
                {
                    // Create a triangle vertex
                    // Based on heightMap, heightMultiplier, heightCurve

                    // If its out of the rendered mesh
                    bool isOutOfMeshVertex = y == 0 || y == numVertsPerLine - 1 || x == 0 || x == numVertsPerLine - 1;
                    bool isMeshEdgeVertex = (y == 1 || y == numVertsPerLine - 2 || x == 1 || x == numVertsPerLine - 2) && !isOutOfMeshVertex;
                    bool isMainVertex = (x - 2) % meshSkipIncrement == 0 && (y - 2) % meshSkipIncrement == 0 && !isOutOfMeshVertex && !isMeshEdgeVertex;
                    bool isEdgeConnectionVertex = !isOutOfMeshVertex && !isMeshEdgeVertex && !isMainVertex && (y == 2 || y == numVertsPerLine - 3 || x == 2 || x == numVertsPerLine - 3);

                    float height = heightMap[x, y];

                    // Subtract by mesh simplicationIncrement to center UVs
                    int vertexIndex = vertexIndicesMap[x, y];
                    // Percent of uv taking into account the skipped Vertices
                    Vector2 percentUV = new Vector2(x - 1, y - 1)/(numVertsPerLine - 3);

                    Vector2 vertexPosition2D = topLeft + new Vector2(percentUV.x, - percentUV.y) * meshSettings.meshWorldSize;

                    // Smoothen edge between LOD and regular edge mesh
                    if(isEdgeConnectionVertex)
                    {
                        // Is outer vertical edge
                        bool isVertical = x == 2 || x == numVertsPerLine - 3;
                        // Choose neigbouring vertex
                        int dstToMainVertexA = ((isVertical) ? y - 2 : x - 2) % meshSkipIncrement;
                        // Other side is meshIncrement - A
                        int dstToMainVertexB = meshSkipIncrement - dstToMainVertexA;

                        float dstPercentFromAtoB = dstToMainVertexA / (float)meshSkipIncrement;

                        float heightMainVertexA = heightMap[(isVertical ? x : x - dstToMainVertexA), (isVertical ? y - dstToMainVertexA : y)];
                        float heightMainVertexB = heightMap[(isVertical ? x : x + dstToMainVertexB), (isVertical ? y + dstToMainVertexB : y)];

                        height = heightMainVertexA * (1 - dstPercentFromAtoB) + heightMainVertexB * dstPercentFromAtoB;
                    }

                    meshData.AddVertex(new Vector3(vertexPosition2D.x, height, vertexPosition2D.y), percentUV, vertexIndex);

                    bool createTriangle = x < numVertsPerLine - 1 && y < numVertsPerLine - 1 && (!isEdgeConnectionVertex || (x != 2 && y != 2));

                    // Skip edges
                    if (createTriangle)
                    {
                        //Calculate current Increment based on using High Detail edges or using LODIncrement
                        int currentIncrement = (isMainVertex && x != numVertsPerLine - 3 && y != numVertsPerLine - 3) ? meshSkipIncrement : 1;

                        // Create triangle per square a,b,c,d
                        int a = vertexIndicesMap[x, y];
                        int b = vertexIndicesMap[x + currentIncrement, y];
                        int c = vertexIndicesMap[x, y + currentIncrement];
                        int d = vertexIndicesMap[x + currentIncrement, y + currentIncrement];

                        // Create triangle adc, dab
                        meshData.AddTriangle(a, d, c);
                        meshData.AddTriangle(d, a, b);
                    }
                }
            }
        }

        meshData.ProcessMeshNormals();

        // For threading in unity, return meshdata in the thread and return all of it outside of it
        return meshData;
    }
}

// Class to create mesh, triangles, uvs, etc
public class MeshData {
    Vector3[] vertices;
    int[] triangles;
    Vector2[] uvs;

    Vector3[] bakedNormals;
    Vector3[] outOfMeshVertices;
    int[] outOfMeshTriangles;

    int triangleIndex;
    int outOfMeshTriangleIndex;

    bool useFlatShading;

    public MeshData(int numVertsPerLine, int skipIncrement, bool useFlatShading)
    {
        this.useFlatShading = useFlatShading;

        int numMeshEdgeVertices = (numVertsPerLine - 2) * 4 - 4;
        int numConnectionVertices = (skipIncrement - 1) * (numVertsPerLine - 5) / skipIncrement * 4;

        // Find out main vertices per line
        int numMainVertsPerLine = (numVertsPerLine - 5) / skipIncrement + 1;
        int numMainVertices = numMainVertsPerLine * numMainVertsPerLine;

        vertices = new Vector3[numMeshEdgeVertices + numConnectionVertices + numMainVertices];
        uvs = new Vector2[vertices.Length];

        // Number of triangles is ((numVertsPerLine - 3) * 4 - 4) * 2;
        int numMeshEdgeTriangles = 8 * (numVertsPerLine - 4);
        int numMainTriangles = (numMainVertsPerLine - 1) * (numMainVertsPerLine - 1) * 2;
        triangles = new int[(numMeshEdgeTriangles + numMainTriangles) * 3];

        // vertices on the border
        outOfMeshVertices = new Vector3[numVertsPerLine * 4 - 4];
        // no. of triangles on the border ((numVertsPerLine - 1) * 4 - 4) * 2 * 3
        outOfMeshTriangles = new int[24 * (numVertsPerLine - 2)];
    }

    public void AddVertex(Vector3 vertexPosition, Vector2 uv, int vertexIndex)
    {
        if(vertexIndex < 0)
        {
            // Negative Vertex -> Border Vertex
            outOfMeshVertices[-vertexIndex - 1] = vertexPosition;
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
            outOfMeshTriangles[outOfMeshTriangleIndex] = a;
            outOfMeshTriangles[outOfMeshTriangleIndex + 1] = b;
            outOfMeshTriangles[outOfMeshTriangleIndex + 2] = c;
            outOfMeshTriangleIndex += 3;
        } else
        {
            // Positive index -> Mesh Triangle
            triangles[triangleIndex] = a;
            triangles[triangleIndex + 1] = b;
            triangles[triangleIndex + 2] = c;
            triangleIndex += 3;
        }
    }

    public void ProcessMeshNormals()
    {
        if(useFlatShading)
        {
            BakeFlatShadedNormals();
        } else
        {
            BakeNormals();
        }
    }

    private void BakeFlatShadedNormals()
    {
        // Create seperate triangled vertices so that the vertices have their own lighting
        Vector3[] flatShadedVertices = new Vector3[triangles.Length];
        Vector2[] flatShadedUvs = new Vector2[triangles.Length];

        // Seperate triangles for every triangle in the mesh
        for(int i = 0; i < triangles.Length; i++)
        {
            flatShadedVertices[i] = vertices[triangles[i]];
            flatShadedUvs[i] = uvs[triangles[i]];
            // Reset triangle
            triangles[i] = i;
        }

        vertices = flatShadedVertices;
        uvs = flatShadedUvs;
    }

    void BakeNormals()
    {
        bakedNormals = CalculateNormals();
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
        for (int i = 0; i < outOfMeshTriangles.Length / 3; i++)
        {
            int normalTriangleIndex = i * 3;
            int vertexIndexA = outOfMeshTriangles[normalTriangleIndex];
            int vertexIndexB = outOfMeshTriangles[normalTriangleIndex + 1];
            int vertexIndexC = outOfMeshTriangles[normalTriangleIndex + 2];

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
        Vector3 pointA = indexA < 0 ? outOfMeshVertices[-indexA - 1] : vertices[indexA];
        Vector3 pointB = indexB < 0 ? outOfMeshVertices[-indexB - 1] : vertices[indexB];
        Vector3 pointC = indexC < 0 ? outOfMeshVertices[-indexC - 1] : vertices[indexC];

        // Unity calculates normals per triangle vertex, cross product of the 2 sides to the triangle
        return Vector3.Cross(pointB - pointA, pointC - pointA).normalized;
    }

    public Mesh CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        if(useFlatShading) {
            //Every triangle is disconnected from another triangle, just recalculate normals
            mesh.RecalculateNormals();
        } else
        {
            // Use normals with lighting from other meshes taken into account
            mesh.normals = bakedNormals;
        }
        return mesh;
    }
}
