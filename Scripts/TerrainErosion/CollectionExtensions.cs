using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CollectionExtensions
{
    public static T GetRandom<T>(this IEnumerable<T> list)
    {
        return list.ElementAt(Random.Range(0, list.Count() - 1));
    }

    public static float Max(this float[,] values)
    {
        int length = values.GetLength(0);
        int width = values.GetLength(1);
        float maximum = float.MinValue;

        for (int y = 0; y < width; y++)
            for (int x = 0; x < length; x++)
            {
                if (values[x, y] > maximum)
                    maximum = values[x, y];
            }

        return maximum;
    }

    public static float Min(this float[,] values)
    {
        int length = values.GetLength(0);
        int width = values.GetLength(1);
        float minimum = float.MaxValue;

        for (int y = 0; y < width; y++)
            for (int x = 0; x < length; x++)
            {
                if (values[x, y] < minimum)
                    minimum = values[x, y];
            }

        return minimum;
    }
}

public static class VectorExtensions
{
    public static Vector3 WithY(this Vector3 vector, float value)
    {
        vector.y = value;
        return vector;
    }
}