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

    TerrainGenerator terrainGen;

    float minHeightMultiplier = 30f;
    float maxHeightMultiplier = 200f;
    float minNoiseEstimator = 1f;
    float maxNoiseEstimator = 2.5f;
    float minNoiseScale = 20f;
    float maxNoiseScale = 200f;
    int minNoiseOctaves = 4;
    int maxNoiseOctaves = 12;
    float minPersistence = 0.4f;
    float maxPersistence = 0.9f;
    float minLacunarity = 2f;
    float maxLacunarity = 5f;
    int minSeed = 0;
    int maxSeed = 100;
    float minNoiseOffset = 0f;
    float maxNoiseOffset = 50f;
    float minWaterLevel = 1f;
    float maxWaterLevel = 5f;

    EnvironmentParameters m_ResetParams;

    public override void Initialize()
    {
        terrainGen = FindObjectOfType<TerrainGenerator>();

        HeightMapSettings newHeightSettings = (HeightMapSettings)ScriptableObject.CreateInstance(typeof(HeightMapSettings));
        newHeightSettings.falloffCurve = terrainGen.heightMapSettings.falloffCurve;
        newHeightSettings.heightCurve = terrainGen.heightMapSettings.heightCurve;
        newHeightSettings.heightMultiplier = terrainGen.heightMapSettings.heightMultiplier;
        newHeightSettings.noiseSettings = new NoiseSettings();
        newHeightSettings.noiseSettings.lacunarity = terrainGen.heightMapSettings.noiseSettings.lacunarity;
        newHeightSettings.noiseSettings.persistence = terrainGen.heightMapSettings.noiseSettings.persistence;
        newHeightSettings.noiseSettings.normalizeMode = terrainGen.heightMapSettings.noiseSettings.normalizeMode;
        newHeightSettings.noiseSettings.noiseEstimatorVariable = terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable;
        newHeightSettings.noiseSettings.octaves = terrainGen.heightMapSettings.noiseSettings.octaves;
        newHeightSettings.noiseSettings.offset = terrainGen.heightMapSettings.noiseSettings.offset;
        newHeightSettings.noiseSettings.scale = terrainGen.heightMapSettings.noiseSettings.scale;
        newHeightSettings.noiseSettings.seed = terrainGen.heightMapSettings.noiseSettings.seed;
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

        m_ResetParams = Academy.Instance.EnvironmentParameters;

        // If the agent is not training, Max Step is 0
        if (!isTraining)
        {
            MaxStep = 0;
        }

        Academy.Instance.AutomaticSteppingEnabled = false;
        UpdateRandomizedTerrainData();
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
        terrainGen.meshSettings.waterLevel = Random.Range(minWaterLevel, terrainGen.heightMapSettings.maxHeight / 2);

        terrainGen.heightMapSettings.noiseSettings.offset = new Vector2(Random.Range(minNoiseOffset, maxNoiseOffset), Random.Range(minNoiseOffset, maxNoiseOffset));
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
        // 10 normalized value observations
        sensor.AddObservation((terrainGen.heightMapSettings.heightMultiplier - minHeightMultiplier) / (maxHeightMultiplier - minHeightMultiplier));
        sensor.AddObservation((terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable - minNoiseEstimator) / (maxNoiseEstimator - minNoiseEstimator));
        sensor.AddObservation((terrainGen.heightMapSettings.noiseSettings.scale - minNoiseScale) / (maxNoiseScale - minNoiseScale));
        sensor.AddObservation((terrainGen.heightMapSettings.noiseSettings.octaves - minNoiseOctaves) / (maxNoiseOctaves - minNoiseOctaves));
        sensor.AddObservation((terrainGen.heightMapSettings.noiseSettings.persistence - minPersistence) / (maxPersistence - minPersistence));
        sensor.AddObservation((terrainGen.heightMapSettings.noiseSettings.lacunarity - minLacunarity) / (maxLacunarity - minLacunarity));
        sensor.AddObservation((terrainGen.heightMapSettings.noiseSettings.seed - minSeed) / (maxSeed - minSeed));
        sensor.AddObservation((terrainGen.heightMapSettings.noiseSettings.offset.x - minNoiseOffset) / (maxNoiseOffset - minNoiseOffset));
        sensor.AddObservation((terrainGen.heightMapSettings.noiseSettings.offset.y - minNoiseOffset) / (maxNoiseOffset - minNoiseOffset));
        sensor.AddObservation((terrainGen.meshSettings.waterLevel - minWaterLevel) / (maxWaterLevel - minWaterLevel));

        // 10 settings observations
        sensor.AddObservation(terrainGen.trainerChunksGenerated);
        sensor.AddObservation(terrainGen.heightMapSettings.heightMultiplier);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.scale);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.octaves);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.persistence);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.lacunarity);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.seed);
        sensor.AddObservation(terrainGen.heightMapSettings.noiseSettings.offset);
        sensor.AddObservation(terrainGen.meshSettings.waterLevel);

        // 5 output observations
        if (terrainGen.chunkCollidersMade > 0)
        {   
            sensor.AddObservation(((terrainGen.totNormalX / terrainGen.chunkCollidersMade) - minXNormal) / (maxXNormal - minXNormal));
            sensor.AddObservation(((terrainGen.totNormalY / terrainGen.chunkCollidersMade) - minYNormal) / (maxYNormal - minYNormal));
            sensor.AddObservation(((terrainGen.totNormalZ / terrainGen.chunkCollidersMade) - minZNormal) / (maxZNormal - minZNormal));
            sensor.AddObservation(((terrainGen.totValidSlope / terrainGen.chunkCollidersMade) - minValidSlopePercent) / (maxValidSlopePercent - minValidSlopePercent));
            sensor.AddObservation(((terrainGen.totWaterAmount / terrainGen.chunkCollidersMade) - minWaterAmount) / (maxWaterAmount - minWaterAmount));
        } else
        {
            sensor.AddObservation(0.0f);
            sensor.AddObservation(0.0f);
            sensor.AddObservation(0.0f);
            sensor.AddObservation(0.0f);
            sensor.AddObservation(0.0f);
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

        Debug.Log("Lesson:" + m_ResetParams.GetWithDefault("lessonID", 0f) + " Completed Episodes:" + this.CompletedEpisodes + " StepCount:" + this.StepCount + " Cumalative Reward:" + this.GetCumulativeReward());

        // Do not calculate rewards here unless valid values have been set
        switch (m_ResetParams.GetWithDefault("lessonID", -1f))
        {
            case -1:
                // Add a slight amount of reward for focusing on ideal values
                {
                if(hasGotViableNormals && hasGoodWater && hasGotGoodSlope)
                    AddReward(10f);
                }
                break;
            case 1:
                if (hasGoodWater)
                {
                    AddReward(0.1f);
                }

                // Focus on only normals and water                
                if (hasGotViableNormals)
                {
                    AddReward(50f);
                    //EndEpisode();
                } else
                {
                    // Negative reward multiplied by difference amount missed
                    // Staggered adding of reward because learning environment defaults to flatenned terrain
                    if (avgYNormal < minYNormal || avgYNormal > maxYNormal)
                    {
                        AddReward(-2f * Mathf.Abs(avgYNormal - (minYNormal + maxYNormal) / 2));
                    }
                    else
                    {
                        AddReward(1f);
                    }
                    if (avgXNormal < minXNormal || avgXNormal > maxXNormal)
                    {
                        AddReward(-1f * Mathf.Abs(avgXNormal - (minXNormal + maxXNormal) / 2));
                    }
                    else
                    {
                        AddReward(1f);
                    }
                    if (avgZNormal < minZNormal || avgYNormal > maxZNormal)
                    {
                        AddReward(-1f * Mathf.Abs(avgZNormal - (minZNormal + maxZNormal) / 2));
                    }
                    else
                    {
                        AddReward(1f);
                    }
                }
                break;
            case 2:
                // Focus on only water
                // Focusing on first making water good so that normals can be calculated better
                // Adds way too much noise to learning and prevents normals calculation
                // water should be focused on after
                if (hasGotViableNormals)
                {
                    AddReward(0.1f);
                }

                if (hasGotViableNormals && hasGoodWater)
                {
                    AddReward(50f);
                    //EndEpisode();
                }
                else
                {
                    AddReward(-1f * Mathf.Abs(avgWaterAmount - (minWaterAmount + maxWaterAmount) / 2));
                    //EndEpisode();
                }
                break;
            case 3:
                // Focus mainly on slope
                if (hasGotViableNormals)
                {
                    AddReward(0.15f);
                }

                if (hasGoodWater)
                {
                    AddReward(0.1f);
                }

                if (hasGotViableNormals && hasGoodWater && hasGotGoodSlope)
                {
                    AddReward(50f);
                    //EndEpisode();
                }
                else
                {
                    AddReward(-5f * Mathf.Abs(avgValidSlope - (minValidSlopePercent + maxValidSlopePercent) / 2));
                    //EndEpisode();
                }
                break;
            case 4:
                // Focus on all criteria being met and in the soonest time possible
                if (hasGoodWater && hasGotGoodSlope && hasGotViableNormals)
                {
                    // Add bonus reward based on persistence, lacunarity, divided by scale (Soother terrain will get less reward)
                    // Prevents generation of extremely smooth terrain, adds variablility to reward signal
                    float rewardMultiplier = (terrainGen.heightMapSettings.noiseSettings.persistence * terrainGen.heightMapSettings.noiseSettings.lacunarity) / (maxNoiseScale / terrainGen.heightMapSettings.noiseSettings.scale);
                    // Add Success reward (3000f)
                    SetReward(500f * rewardMultiplier);
                    EndEpisode();
                } else
                {
                    AddReward(-5f);
                }
                break;
        }
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        //Debug.Log("IN ACTION Taking Action");
        if (!terrainGen.trainerChunksGenerated)
        {
            return;
        }
        //Debug.Log("No early return");

        bool isValidValuesLesson = m_ResetParams.GetWithDefault("lessonID", -1f) == -1;

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

        // 8 vector actions, increment relatively, upddate div value to make sure limits are proper
        float divValue = (maxHeightMultiplier - minHeightMultiplier) / trainValueDiv;
        if (vectorAction[0] == 0 || (
            (terrainGen.heightMapSettings.heightMultiplier - (Mathf.Abs(vectorAction[0]) * divValue) > minHeightMultiplier || vectorAction[0] > 0) && 
            (terrainGen.heightMapSettings.heightMultiplier + (Mathf.Abs(vectorAction[0]) * divValue) < maxHeightMultiplier || vectorAction[0] < 0)))
        {
            terrainGen.heightMapSettings.heightMultiplier += vectorAction[0] * divValue;
            // If first lesson, addReward for valid value
            AddReward(isValidValuesLesson ? 2.5f : 0f);
        } else
        {
            // If first lesson, addReward for invalid value
            AddReward(isValidValuesLesson ? -10 : -1);
        }

        divValue = (maxNoiseEstimator - minNoiseEstimator) / trainValueDiv;
        if (vectorAction[1] == 0 || (
            (terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable - (Mathf.Abs(vectorAction[1]) * divValue) > minNoiseEstimator || vectorAction[1] > 0) && 
            (terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable + (Mathf.Abs(vectorAction[1]) * divValue) < maxNoiseEstimator || vectorAction[1] < 0)))
        {
            terrainGen.heightMapSettings.noiseSettings.noiseEstimatorVariable += vectorAction[1] * divValue;
            // If first lesson, addReward for valid value
            AddReward(isValidValuesLesson ? 2.5f : 0f);
        }
        else
        {
            // If first lesson, addReward for invalid value
            AddReward(isValidValuesLesson ? -10 : -1);
        }

        divValue = (maxNoiseScale - minNoiseScale) / trainValueDiv;
        if (vectorAction[2] == 0 || (
            (terrainGen.heightMapSettings.noiseSettings.scale - (Mathf.Abs(vectorAction[2]) * divValue) > minNoiseScale || vectorAction[2] > 0) && 
            (terrainGen.heightMapSettings.noiseSettings.scale + (Mathf.Abs(vectorAction[2]) * divValue) < maxNoiseScale || vectorAction[2] < 0)))
        {
            terrainGen.heightMapSettings.noiseSettings.scale += vectorAction[2] * divValue;
            // If first lesson, addReward for valid value
            AddReward(isValidValuesLesson ? 2.5f : 0f);
        }
        else
        {
            // If first lesson, addReward for invalid value
            AddReward(isValidValuesLesson ? -10 : -1);
        }

        divValue = (maxNoiseOctaves - minNoiseOctaves) / trainValueDiv;
        if (vectorAction[3] == 0 || (
            (terrainGen.heightMapSettings.noiseSettings.octaves - (Mathf.Abs(vectorAction[3]) * divValue) > minNoiseOctaves || vectorAction[3] > 0) && 
            (terrainGen.heightMapSettings.noiseSettings.octaves + (Mathf.Abs(vectorAction[3]) * divValue) < maxNoiseOctaves || vectorAction[3] < 0)))
        {
            terrainGen.heightMapSettings.noiseSettings.octaves += Mathf.CeilToInt(vectorAction[3] * divValue);
            // If first lesson, addReward for valid value
            AddReward(isValidValuesLesson ? 2.5f : 0f);
        }
        else
        {
            // If first lesson, addReward for invalid value
            AddReward(isValidValuesLesson ? -10 : -1);
        }

        divValue = (maxPersistence - minPersistence) / trainValueDiv;
        if (vectorAction[4] == 0 || (
            (terrainGen.heightMapSettings.noiseSettings.persistence - (Mathf.Abs(vectorAction[4]) * divValue) > minPersistence || vectorAction[4] > 0) && 
            (terrainGen.heightMapSettings.noiseSettings.persistence + (Mathf.Abs(vectorAction[4]) * divValue) < maxPersistence || vectorAction[4] < 0)))
        {
            terrainGen.heightMapSettings.noiseSettings.persistence += vectorAction[4] * divValue;
            // If first lesson, addReward for valid value
            AddReward(isValidValuesLesson ? 2.5f : 0f);
        }
        else
        {
            // If first lesson, addReward for invalid value
            AddReward(isValidValuesLesson ? -10 : -1);
        }

        divValue = (maxLacunarity - minLacunarity) / trainValueDiv;
        if (vectorAction[5] == 0 || (
            (terrainGen.heightMapSettings.noiseSettings.lacunarity - (Mathf.Abs(vectorAction[5]) * divValue) > minLacunarity || vectorAction[5] > 0) && 
            (terrainGen.heightMapSettings.noiseSettings.lacunarity + (Mathf.Abs(vectorAction[5]) * divValue) < maxLacunarity || vectorAction[5] < 0)))
        {
            terrainGen.heightMapSettings.noiseSettings.lacunarity += vectorAction[5] * divValue;
            // If first lesson, addReward for valid value
            AddReward(isValidValuesLesson ? 2.5f : 0f);
        }
        else
        {
            // If first lesson, addReward for invalid value
            AddReward(isValidValuesLesson ? -10 : -1);
        }

        divValue = (maxSeed - minSeed) / trainValueDiv;
        if (vectorAction[6] == 0 || (
            (terrainGen.heightMapSettings.noiseSettings.seed - (Mathf.Abs(vectorAction[6]) * divValue) > minSeed || vectorAction[6] > 0) && 
            (terrainGen.heightMapSettings.noiseSettings.seed + (Mathf.Abs(vectorAction[6]) * divValue) < maxSeed || vectorAction[6] < 0)))
        {
            terrainGen.heightMapSettings.noiseSettings.seed += Mathf.CeilToInt(vectorAction[6] * divValue);
            // If first lesson, addReward for valid value
            AddReward(isValidValuesLesson ? 2.5f : 0f);
        }
        else
        {
            // If first lesson, addReward for invalid value
            AddReward(isValidValuesLesson ? -10 : -1);
        }

        divValue = (maxWaterLevel - minWaterLevel) / trainValueDiv;
        if (vectorAction[7] == 0 || (
            (terrainGen.meshSettings.waterLevel - (Mathf.Abs(vectorAction[7]) * divValue) > minWaterLevel || vectorAction[7] > 0) && 
            (terrainGen.meshSettings.waterLevel + (Mathf.Abs(vectorAction[7]) * divValue) < maxWaterLevel || vectorAction[7] < 0)))
        {
            terrainGen.meshSettings.waterLevel += vectorAction[7] * divValue;
            // If first lesson, addReward for valid value
            AddReward(isValidValuesLesson ? 2.5f : 0f);
        }
        else
        {
            // If first lesson, addReward for invalid value
            AddReward(isValidValuesLesson ? -10 : -1);
        }

        Vector2 offsetVector = terrainGen.heightMapSettings.noiseSettings.offset;
        divValue = (maxNoiseOffset - minNoiseOffset) / trainValueDiv;
        if (vectorAction[8] == 0 || (
            (terrainGen.heightMapSettings.noiseSettings.offset.x - (Mathf.Abs(vectorAction[8]) * divValue) > minNoiseOffset || vectorAction[8] > 0) && 
            (terrainGen.heightMapSettings.noiseSettings.offset.x + (Mathf.Abs(vectorAction[8]) * divValue) < maxNoiseOffset || vectorAction[8] < 0)))
        {
            offsetVector.x += vectorAction[8] * divValue;
            // If first lesson, addReward for valid value
            AddReward(isValidValuesLesson ? 2.5f : 0f);
        }
        else
        {
            // If first lesson, addReward for invalid value
            AddReward(isValidValuesLesson ? -10 : -1);
        }
        divValue =(maxNoiseOffset - minNoiseOffset) / trainValueDiv;
        if (vectorAction[9] == 0 || (
            (terrainGen.heightMapSettings.noiseSettings.offset.y - (Mathf.Abs(vectorAction[9]) * divValue) > minNoiseOffset || vectorAction[9] > 0) && 
            (terrainGen.heightMapSettings.noiseSettings.offset.y + (Mathf.Abs(vectorAction[9]) * divValue) < maxNoiseOffset || vectorAction[9] < 0)))
        {
            offsetVector.y += vectorAction[9] * (maxNoiseOffset - minNoiseOffset) / trainValueDiv;
            // If first lesson, addReward for valid value
            AddReward(isValidValuesLesson ? 2.5f : 0f);
        }
        else
        {
            // If first lesson, addReward for invalid value
            AddReward(isValidValuesLesson ? -10 : -1);
        }
        terrainGen.heightMapSettings.noiseSettings.offset = offsetVector;

        terrainGen.ResetGenerator();
    }

    public int GetMinimumChunks()
    {
        // return 1 for valid inputs and minimumchunkcolliders for the rest of the lessons
        return m_ResetParams.GetWithDefault("lessonID", -1f) == -1 ? 3 : minimumChunkColliders;
    }

    public void OnGenerationComplete()
    {
        this.ComputeRewards();
        this.RequestDecision();
        Academy.Instance.EnvironmentStep();
    }
}
