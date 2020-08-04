using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEngine;

public class GeneratorAgent : Agent
{
    [Tooltip("Minimum number of colliders that should be created to collect data")]
    public float trainValueDiv = 20;

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
    float maxHeightMultiplier = 200f;
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
    float maxWaterLevel = 5f;

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
        UpdateRandomizedTerrainData();
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
        bool hasGoodWater = avgWaterAmount > minWaterAmount && avgWaterAmount < maxWaterAmount;
        bool hasGotGoodSlope = avgValidSlope > minValidSlopePercent && avgValidSlope < maxValidSlopePercent;

        if (avgYNormal < minYNormal || avgYNormal > maxYNormal)
        {
            // Negative reward multiplied by difference amount missed
            AddReward(-20f * Mathf.Abs(avgYNormal - (minYNormal + maxYNormal) / 2));
            if (avgYNormal < minXNormal || avgYNormal > maxXNormal || avgYNormal < minZNormal || avgYNormal > maxZNormal)
            {
                AddReward(-10f * Mathf.Abs(avgXNormal - (minXNormal + maxXNormal) / 2) * Mathf.Abs(avgZNormal - (minZNormal + maxZNormal) / 2));
                //EndEpisode();
            }
        }
        else if (hasGotViableNormals)
        {
            //Debug.Log("Viable Normals");
            AddReward(150f);
        }
        else
        {
            AddReward(-50f);
            //EndEpisode();
        }

        if (hasGotGoodSlope)
        {
            //Debug.Log("Viable Water");
            AddReward(150f);
        }
        else
        {
            AddReward(-75f);
            //EndEpisode();
        }

        if (hasGoodWater)
        {
            //Debug.Log("Viable Slopes");
            AddReward(50f);
        }
        else
        {
            AddReward(-25f);
            //EndEpisode();
        }

        if(hasGoodWater && hasGotGoodSlope && hasGotViableNormals)
        {
            AddReward(3000f);
            EndEpisode();
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


        // 8 vector Actions to control Noise Generation parameters, set specific values
        // Normalize vectorAction from (-1,1) to (0,1)
        //for (int i = 0; i < vectorAction.Length; i++)
        //{
        //    vectorAction[i] = (vectorAction[i] + 1) / 2f;
        //}
        //terrainGen.heightMapSettings.heightMultiplier = minHeightMultiplier + (vectorAction[0] * (maxHeightMultiplier - minHeightMultiplier));
        //terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable = minNoiseEstimator + (vectorAction[1] * (maxNoiseEstimator - minNoiseEstimator));
        //tb errainGen.heightMapSettings.noiseSettings.scale = minNoiseScale + (vectorAction[2] * (maxNoiseScale - minNoiseScale));
        //terrainGen.heightMapSettings.noiseSettings.octaves = minNoiseOctaves + Mathf.CeilToInt(vectorAction[3] * (maxNoiseOctaves - minNoiseOctaves));
        //terrainGen.heightMapSettings.noiseSettings.persistence = minPersistence + (vectorAction[4] * (maxPersistence - minPersistence));
        //terrainGen.heightMapSettings.noiseSettings.lacunarity = minLacunarity + (vectorAction[5] * (maxLacunarity - minLacunarity));
        //terrainGen.heightMapSettings.noiseSettings.seed = minSeed + Mathf.FloorToInt(vectorAction[6] * (maxSeed - minSeed));
        //terrainGen.meshSettings.waterLevel = minWaterLevel + (vectorAction[7] * (maxWaterLevel - minWaterLevel));

        //terrainGen.heightMapSettings.noiseSettings.offset = new Vector2(minNoiseOffset + (vectorAction[8] * (maxNoiseOffset - minNoiseOffset)), minNoiseOffset + (vectorAction[9] * (maxNoiseOffset - minNoiseOffset)));

        // 8 vector actions, increment relatively
        // (Add negative reward for going above the limit?)
        if ((terrainGen.heightMapSettings.heightMultiplier > minHeightMultiplier || vectorAction[0] > 0) && (terrainGen.heightMapSettings.heightMultiplier < maxHeightMultiplier || vectorAction[0] < 0))
        {
            terrainGen.heightMapSettings.heightMultiplier += vectorAction[0] * (maxHeightMultiplier - minHeightMultiplier) / trainValueDiv;
        } else
        {
            AddReward(-1f);
        }
        if ((terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable > minNoiseEstimator || vectorAction[1] > 0) && (terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable < maxNoiseEstimator || vectorAction[1] < 0))
        {
            terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable += vectorAction[1] * (maxNoiseEstimator - minNoiseEstimator) / trainValueDiv;
        }
        else
        {
            AddReward(-1f);
        }
        if ((terrainGen.heightMapSettings.noiseSettings.scale > minNoiseScale || vectorAction[2] > 0) && (terrainGen.heightMapSettings.noiseSettings.scale < maxNoiseScale || vectorAction[2] < 0))
        {
            terrainGen.heightMapSettings.noiseSettings.scale += vectorAction[2] * (maxNoiseScale - minNoiseScale) / trainValueDiv;
        }
        else
        {
            AddReward(-1f);
        }
        if ((terrainGen.heightMapSettings.noiseSettings.octaves > minNoiseOctaves || vectorAction[3] > 0) && (terrainGen.heightMapSettings.noiseSettings.octaves < maxNoiseOctaves || vectorAction[3] < 0))
        {
            terrainGen.heightMapSettings.noiseSettings.octaves += Mathf.CeilToInt(vectorAction[3] * (maxNoiseOctaves - minNoiseOctaves) / trainValueDiv);
        }
        else
        {
            AddReward(-1f);
        }
        if ((terrainGen.heightMapSettings.noiseSettings.persistence > minPersistence || vectorAction[4] > 0) && (terrainGen.heightMapSettings.noiseSettings.persistence < maxPersistence || vectorAction[4] < 0))
        {
            terrainGen.heightMapSettings.noiseSettings.persistence += vectorAction[4] * (maxPersistence - minPersistence) / trainValueDiv;
        }
        else
        {
            AddReward(-1f);
        }
        if ((terrainGen.heightMapSettings.noiseSettings.lacunarity > minLacunarity || vectorAction[5] > 0) && (terrainGen.heightMapSettings.noiseSettings.lacunarity < maxLacunarity || vectorAction[5] < 0))
        {
            terrainGen.heightMapSettings.noiseSettings.lacunarity += vectorAction[5] * (maxLacunarity - minLacunarity) / trainValueDiv;
        }
        else
        {
            AddReward(-1f);
        }
        if ((terrainGen.heightMapSettings.noiseSettings.seed > minSeed || vectorAction[6] > 0) && (terrainGen.heightMapSettings.noiseSettings.seed < maxSeed || vectorAction[6] < 0))
        {
            terrainGen.heightMapSettings.noiseSettings.seed += Mathf.CeilToInt(vectorAction[6] * (maxSeed - minSeed) / trainValueDiv);
        }
        else
        {
            AddReward(-1f);
        }
        if ((terrainGen.meshSettings.waterLevel > minWaterLevel || vectorAction[7] > 0) && (terrainGen.meshSettings.waterLevel < maxWaterLevel || vectorAction[7] < 0))
        {
            terrainGen.meshSettings.waterLevel += vectorAction[7] * (maxWaterLevel - minWaterLevel) / trainValueDiv;
        }
        else
        {
            AddReward(-1f);
        }

        //Debug.Log("IN ACTION Action Reset Generator");
        terrainGen.ResetGenerator();
    }
}
