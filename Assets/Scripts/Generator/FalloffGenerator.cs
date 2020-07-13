using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class FalloffGenerator
{
    public static float[,] GenerateFallOfMap(int size, AnimationCurve falloffCurve)
    {
        // fallOffCurve will break on multiThreading, fix with creating a new curve instance
        AnimationCurve falloffCurveInstance = new AnimationCurve(falloffCurve.keys);

        float[,] map = new float[size, size];
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                float x = i / (float)size * 2 - 1;
                float y = j / (float)size * 2 - 1;

                float value = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                map[i, j] = falloffCurveInstance.Evaluate(value);
            }
        }
        return map;
    }
}
