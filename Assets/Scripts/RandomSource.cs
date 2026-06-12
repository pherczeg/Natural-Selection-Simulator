/// <summary>
/// RNG-injection seam for pure calculators that need randomness. Calculator methods take
/// <c>ref TRandom rng</c> with <c>where TRandom : struct, IRandomSource</c> so a single
/// implementation serves the legacy mono path (<see cref="UnityRandomSource"/>), Burst ECS
/// jobs and deterministic tests (<see cref="MathematicsRandomSource"/>) without boxing.
/// </summary>
public interface IRandomSource
{
    /// <summary>Random value in roughly [0, 1]; mirrors UnityEngine.Random.value semantics.</summary>
    float NextFloat();

    /// <summary>Random value in [min, max]; mirrors UnityEngine.Random.Range semantics.</summary>
    float NextFloat(float min, float max);
}

/// <summary>
/// Delegates to the global UnityEngine.Random stream so legacy mono call sites keep their
/// historical draw order byte-identical. Main-thread only — NOT Burst-compatible.
/// </summary>
public struct UnityRandomSource : IRandomSource
{
    public float NextFloat()
    {
        return UnityEngine.Random.value;
    }

    public float NextFloat(float min, float max)
    {
        return UnityEngine.Random.Range(min, max);
    }
}

/// <summary>
/// Wraps Unity.Mathematics.Random for Burst jobs and seeded deterministic tests.
/// Mutable struct — always pass by ref so the sequence advances for the caller.
/// Note: NextFloat() is [0, 1) while UnityEngine.Random.value is [0, 1]; the open upper
/// bound is acceptable for all current consumers.
/// </summary>
public struct MathematicsRandomSource : IRandomSource
{
    public Unity.Mathematics.Random random;

    public MathematicsRandomSource(uint seed)
    {
        random = new Unity.Mathematics.Random(seed);
    }

    public MathematicsRandomSource(Unity.Mathematics.Random random)
    {
        this.random = random;
    }

    public float NextFloat()
    {
        return random.NextFloat();
    }

    public float NextFloat(float min, float max)
    {
        return random.NextFloat(min, max);
    }
}
