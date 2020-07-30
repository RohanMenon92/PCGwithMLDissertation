using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEngine;

public class GeneratorAgent : Agent
{
    [Tooltip("Minimum number of colliders that should be created to collect data")]
    public int minimumChunkColliders = 9;
    public float minXNormal = 1.5f;
    public float maxXNormal = 2.5f;
    public float minYNormal = 4.5f;
    public float maxYNormal = 5.75f;
    public float minZNormal = 1.5f;
    public float maxZNormal = 2.5f;
    public float minValidSlopePercent = 0.85f;
    public float maxValidSlopePercent = 0.95f;
    public float minWaterAmount = 0.05f;
    public float maxWaterAmount = 0.1f;

    public bool isTraining = true;
    public bool hasComputedReward = false;

    TerrainGenerator terrainGen;

    float minHeightMultiplier = 30f;
    float maxHeightMultiplier = 300f;
    float minNoiseEstimator = 1f;
    float maxNoiseEstimator = 2.5f;
    float minNoiseScale = 20f;
    float maxNoiseScale = 200f;
    int minNoiseOctaves = 4;
    int maxNoiseOctaves = 10;
    float minPersistence = 0.2f;
    float maxPersistence = 0.9f;
    float minLacunarity = 1f;
    float maxLacunarity = 5f;
    int minSeed = -500;
    int maxSeed = 500;
    float minNoiseOffset = 0f;
    float maxNoiseOffset = 50f;
    float minWaterLevel = 1f;
    float maxWaterLevel = 10f;

    public override void Initialize()
    {
        terrainGen = FindObjectOfType<TerrainGenerator>();

        HeightMapSettings newHeightSettings = (HeightMapSettings)ScriptableObject.CreateInstance(typeof(HeightMapSettings));
        newHeightSettings.falloffCurve = terrainGen.heightMapSettings.falloffCurve;
        newHeightSettings.heightCurve = terrainGen.heightMapSettings.heightCurve;
        newHeightSettings.heightMultiplier = terrainGen.heightMapSettings.heightMultiplier;
        newHeightSettings.noiseSettings = terrainGen.heightMapSettings.noiseSettings;
        MeshSettings newMeshSettings = (MeshSettings)ScriptableObject.CreateInstance(typeof(MeshSettings));
        newMeshSettings.chunkSizeIndex = terrainGen.meshSettings.chunkSizeIndex;
        newMeshSettings.flatShadedChunkSizeIndex = terrainGen.meshSettings.flatShadedChunkSizeIndex;
        newMeshSettings.terrainScale = terrainGen.meshSettings.terrainScale;
        newMeshSettings.useFlatShading = terrainGen.meshSettings.useFlatShading;
        newMeshSettings.waterLevel = terrainGen.meshSettings.waterLevel;

        AssetDatabase.DeleteAsset("Assets/Scripts/Generator/Training/HeightMap.asset");
        AssetDatabase.DeleteAsset("Assets/Scripts/Generator/Training/MeshSettings.asset");

        AssetDatabase.CreateAsset(newHeightSettings, "Assets/Scripts/Generator/Training/HeightMap.asset");
        AssetDatabase.CreateAsset(newMeshSettings, "Assets/Scripts/Generator/Training/MeshSettings.asset");

        terrainGen.meshSettings = newMeshSettings;
        terrainGen.heightMapSettings = newHeightSettings;

        // If the agent is not training, Max Step is 0
        if (!isTraining)
        {
            MaxStep = 0;
        }
    }

    public void UpdateRandomizedTerrainData()
    {
        // Set random height map settings
        // These are also valid values for minimum and maximum  values for these variables
        terrainGen.heightMapSettings.heightMultiplier = Random.Range(minHeightMultiplier, maxHeightMultiplier);
        terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable = Random.Range(minNoiseEstimator, maxNoiseEstimator);

        terrainGen.heightMapSettings.noiseSettings.scale = Random.Range(minNoiseScale, maxNoiseScale);
        terrainGen.heightMapSettings.noiseSettings.octaves = Random.Range(minNoiseOctaves, maxNoiseOctaves);
        terrainGen.heightMapSettings.noiseSettings.persistence = Random.Range(minPersistence, maxPersistence);
        terrainGen.heightMapSettings.noiseSettings.lacunarity = Random.Range(minLacunarity, maxLacunarity);
        terrainGen.heightMapSettings.noiseSettings.seed = Random.Range(minSeed, maxSeed);
        terrainGen.heightMapSettings.noiseSettings.offset = new Vector2(Random.Range(minNoiseOffset, maxNoiseOffset), Random.Range(minNoiseOffset, maxNoiseOffset));

        terrainGen.meshSettings.waterLevel = Random.Range(minWaterLevel, terrainGen.heightMapSettings.maxHeight/2);
    }

    public override void OnEpisodeBegin()
    {
        if(!isTraining)
        {
            return;
        }

        // Update new/randomized noise data in mesh settings and height map settings
        //UpdateRandomizedTerrainData();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(terrainGen.trainerChunksGenerated);

        sensor.AddObservation(terrainGen.heightMapSettings.heightMultiplier);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.scale);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.octaves);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.persistence);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.lacunarity);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.seed);
        sensor.AddObservation(terrainGen.meshSettings.waterLevel);

        if(terrainGen.chunkCollidersMade > 0)
        {
            sensor.AddObservation(terrainGen.totNormalX / terrainGen.chunkCollidersMade);
            sensor.AddObservation(terrainGen.totNormalY / terrainGen.chunkCollidersMade);
            sensor.AddObservation(terrainGen.totNormalZ / terrainGen.chunkCollidersMade);
            sensor.AddObservation(terrainGen.totValidSlope / terrainGen.chunkCollidersMade);
            sensor.AddObservation(terrainGen.totWaterAmount / terrainGen.chunkCollidersMade);
        } else
        {
            sensor.AddObservation(0);
            sensor.AddObservation(0);
            sensor.AddObservation(0);
            sensor.AddObservation(0);
            sensor.AddObservation(0);
        }
    }

    public void ComputeRewards()
    {
        //Debug.Log("ComputeRewards");
        float avgXNormal = (terrainGen.totNormalX / terrainGen.chunkCollidersMade);
        float avgYNormal = (terrainGen.totNormalY / terrainGen.chunkCollidersMade);
        float avgZNormal = (terrainGen.totNormalZ / terrainGen.chunkCollidersMade);
        float avgValidSlope = (terrainGen.totValidSlope / terrainGen.chunkCollidersMade);
        float avgWaterAmount = (terrainGen.totWaterAmount / terrainGen.chunkCollidersMade);


        bool hasGotViableNormals = avgXNormal > minXNormal && avgXNormal < maxXNormal && avgYNormal > minYNormal && avgYNormal < maxYNormal && avgZNormal > minZNormal && avgZNormal < maxZNormal;

        if (avgYNormal < minYNormal || avgYNormal > maxYNormal)
        {
            // Negative reward multiplied by difference amount missed
            AddReward(-2f * Mathf.Abs(avgYNormal - (minYNormal + maxYNormal)/2));
            if (avgYNormal < minXNormal || avgYNormal > maxXNormal || avgYNormal < minZNormal || avgYNormal > maxZNormal)
            {
                AddReward(-1f * Mathf.Abs(avgXNormal - (minXNormal + maxXNormal) / 2) * Mathf.Abs(avgZNormal - (minZNormal + maxZNormal) / 2));
            }
        } else if (hasGotViableNormals)
        {
            //Debug.Log("Viable Normals");
            AddReward(0.25f);

            bool hasGoodWater = avgWaterAmount > minWaterAmount && avgWaterAmount < maxWaterAmount;
            if (hasGoodWater)
            {
                //Debug.Log("Viable Water");
                AddReward(2f);
                bool hasGotGoodSlope = avgValidSlope > minValidSlopePercent && avgValidSlope < maxValidSlopePercent;
                if (hasGotGoodSlope)
                {
                    //Debug.Log("Viable Slopes");
                    AddReward(5f);
                    EndEpisode();
                }
                else
                {
                    AddReward(-0.1f);
                    //EndEpisode();
                }
            }
            else
            {
                AddReward(-0.2f);
                //EndEpisode();
            }
        }
        else
        {
            AddReward(-1f);
        }

        Debug.Log("Completed Episodes:" + this.CompletedEpisodes + " StepCount:" + this.StepCount + " Cumalative Reward:" + this.GetCumulativeReward());
        hasComputedReward = true;
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        //Debug.Log("IN ACTION Taking Action");
        if (!terrainGen.trainerChunksGenerated || !hasComputedReward)
        {
            return;
        }

        hasComputedReward = false;

        // Normalize vectorAction from (-1,1) to (0,1)
        for (int i = 0; i < vectorAction.Length; i++)
        {
            vectorAction[i] = (vectorAction[i] + 1) / 2f;
        }

        // 9 vector Actions to control Noise Generation parameters
        terrainGen.heightMapSettings.heightMultiplier = minHeightMultiplier + (vectorAction[0] * (maxHeightMultiplier - minHeightMultiplier));
        terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable = minNoiseEstimator + (vectorAction[1] * (maxNoiseEstimator - minNoiseEstimator));
        terrainGen.heightMapSettings.noiseSettings.scale = minNoiseScale + (vectorAction[2] * (maxNoiseScale - minNoiseScale));
        terrainGen.heightMapSettings.noiseSettings.octaves = minNoiseOctaves + Mathf.CeilToInt(vectorAction[3] * (maxNoiseOctaves - minNoiseOctaves));
        terrainGen.heightMapSettings.noiseSettings.persistence = minPersistence + (vectorAction[4] * (maxPersistence - minPersistence));
        terrainGen.heightMapSettings.noiseSettings.lacunarity = minLacunarity + (vectorAction[5] * (maxLacunarity - minLacunarity));
        terrainGen.heightMapSettings.noiseSettings.seed = minSeed + Mathf.FloorToInt(vectorAction[6] * (maxSeed - minSeed));
        //terrainGen.heightMapSettings.noiseSettings.offset = new Vector2(minNoiseOffset + (vectorAction[7] * (maxNoiseOffset - minNoiseOffset)), minNoiseOffset + (vectorAction[8] * (maxNoiseOffset - minNoiseOffset)));
        terrainGen.meshSettings.waterLevel = minWaterLevel + (vectorAction[7] * (maxWaterLevel - minWaterLevel));

        //Debug.Log("IN ACTION Action Reset Generator");
        terrainGen.ResetGenerator();
    }
}
