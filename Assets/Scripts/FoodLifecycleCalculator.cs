using Unity.Mathematics;

public static class FoodLifecycleCalculator
{
    public const float StartScale = 0.1f;

    public static FoodMirrorData Step(
        FoodMirrorData data,
        float deltaTime,
        float foodMaturityAge,
        float foodMaxAge)
    {
        data.age += Max(0f, deltaTime);
        data.despawnRequested = false;

        float maxNutrition = Max(0f, data.maxNutritionValue);
        data.growthFraction = CalculateGrowthFraction(data.age, foodMaturityAge);

        if (!data.isBeingEaten)
        {
            data.nutritionValue = CalculateNutritionValue(maxNutrition, data.growthFraction);
            float scale = CalculateScale(data.growthFraction);
            data.scale = new float3(scale, scale, scale);
            data.despawnRequested = data.age > foodMaxAge;
        }
        else
        {
            data.nutritionValue = Max(0f, data.nutritionValue);
        }

        data.nutritionPercent = maxNutrition > 0f
            ? Clamp01(data.nutritionValue / maxNutrition)
            : 0f;

        return data;
    }

    public static float CalculateGrowthFraction(float age, float foodMaturityAge)
    {
        if (foodMaturityAge <= 0f)
            return 1f;

        return SmoothStep(Clamp01(age / foodMaturityAge));
    }

    public static float CalculateNutritionValue(float maxNutritionValue, float growthFraction)
    {
        float maxNutrition = Max(0f, maxNutritionValue);
        return Lerp(maxNutrition * StartScale, maxNutrition, growthFraction);
    }

    public static float CalculateScale(float growthFraction)
    {
        return Lerp(StartScale, 1f, growthFraction);
    }

    public static float Clamp01(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;

        if (value <= 0f)
            return 0f;

        return value >= 1f ? 1f : value;
    }

    private static float SmoothStep(float t)
    {
        t = Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float from, float to, float t)
    {
        return from + (to - from) * Clamp01(t);
    }

    private static float Max(float a, float b)
    {
        return a >= b ? a : b;
    }
}
