using System.Collections;
using UnityEngine;

internal class PredationState : CreatureStateBase
{
    private static readonly WaitForFixedUpdate WaitForFixedUpdateCached = new WaitForFixedUpdate();

    private Coroutine predationCoroutine;
    private BaseCreatureBehaviour preyTarget;
    private bool isPredating;

    public PredationState(BaseCreatureBehaviour creatureBehaviour) : base(creatureBehaviour, CreatureStateType.Predation) { }

    public override void ExitState()
    {
        isPredating = false;

        if (predationCoroutine != null)
        {
            creature.coroutineRunner.StopCoroutine(predationCoroutine);
        }

        ReleasePrey();
        predationCoroutine = null;
        preyTarget = null;

        base.ExitState();
    }

    public override void UpdateState()
    {
        if (!isPredating)
        {
            creature.stateMachine.TransitionToWandering();
        }
    }

    public void StartPredationCoroutine(BaseCreatureBehaviour prey)
    {
        predationCoroutine = creature.coroutineRunner.StartCoroutine(PredationRoutine(prey));
    }

    private IEnumerator PredationRoutine(BaseCreatureBehaviour prey)
    {
        if (!IsValidPrey(prey))
        {
            isPredating = false;
            yield break;
        }

        preyTarget = prey;

        if (preyTarget is HerbivoreBehaviour herbivore && !herbivore.TryStartCapture(creature))
        {
            preyTarget = null;
            isPredating = false;
            yield break;
        }

        isPredating = true;
        Statistics.Instance?.RecordPredationAttempt();

        var config = GameConfig.Instance;
        float eatingDuration = config != null ? Mathf.Max(0f, config.predatorEatingDuration) : 0f;
        float elapsed = 0f;

        while (isPredating && IsValidPrey(preyTarget) && elapsed < eatingDuration)
        {
            elapsed += Time.fixedDeltaTime;
            yield return WaitForFixedUpdateCached;
        }

        if (!isPredating)
            yield break;

        if (IsValidPrey(preyTarget))
        {
            ResolvePredation(preyTarget);
        }
        else
        {
            ReleasePrey();
        }

        preyTarget = null;
        isPredating = false;
        predationCoroutine = null;

        creature.stateMachine.TransitionToWandering();
    }

    private void ResolvePredation(BaseCreatureBehaviour prey)
    {
        bool predatorSucceeded = UnityEngine.Random.value <= CalculatePredationSuccessChance(prey);

        if (predatorSucceeded)
        {
            Statistics.Instance?.RecordPredationResolved(true);
            float gainedEnergy = CalculateEnergyGainFromPrey(prey);
            ReleasePrey();
            creature.EnergyManager.GainEnergy(gainedEnergy);
            prey.Despawn(CreatureDeathReason.Predation);
            return;
        }

        Statistics.Instance?.RecordPredationResolved(false);
        ReleasePrey();

        if (prey is HerbivoreBehaviour herbivore)
        {
            herbivore.TriggerEscapeFrom(creature);
        }

        if (creature is PredatorBehaviour predator)
        {
            predator.BlacklistPrey(prey);
        }

        var config = GameConfig.Instance;
        if (config != null)
        {
            creature.MovementManager.ApplyTemporarySpeedMultiplier(
                config.predatorFailedHuntSpeedMultiplier,
                config.predatorFailedHuntDebuffDuration);
        }
    }
    private float CalculateEnergyGainFromPrey(BaseCreatureBehaviour prey)
    {
        var config = GameConfig.Instance;
        float preyCurrentEnergy = prey.EnergyManager.EnergyLevel;
        float preyStoredEnergy = prey.maxEnergy * 0.75f;
        float biomassEnergy = prey.Weight * config.predatorEnergyGainPerPreyWeight;
        float rawEnergyGain = Mathf.Max(preyCurrentEnergy, preyStoredEnergy) + biomassEnergy;

        return rawEnergyGain;
    }
    private float CalculatePredationSuccessChance(BaseCreatureBehaviour prey)
    {
        var config = GameConfig.Instance;
        if (config == null)
            return 0.75f;

        float predatorStrength = creature is PredatorBehaviour predator ? predator.Strength : config.predatorStrengthMax;
        float herbivoreAgility = prey is HerbivoreBehaviour herbivore ? herbivore.Agility : config.herbivoreAgilityMin;

        float predatorScore =
            config.predatorStrengthScoreWeight * Normalize(predatorStrength, config.predatorStrengthMin, config.predatorStrengthMax) +
            config.predatorWeightScoreWeight * Normalize(creature.Weight, config.predatorWeightMin, config.predatorWeightMax);

        float herbivoreScore =
            config.herbivoreAgilityScoreWeight * Normalize(herbivoreAgility, config.herbivoreAgilityMin, config.herbivoreAgilityMax) +
            config.herbivoreWeightScoreWeight * Normalize(prey.Weight, config.herbivoreWeightMin, config.herbivoreWeightMax);

        float advantage = predatorScore - herbivoreScore;
        float chance = 1f / (1f + Mathf.Exp(-(config.predationBias + advantage * config.predationSharpness)));

        float minChance = Mathf.Min(config.minPredationSuccessChance, config.maxPredationSuccessChance);
        float maxChance = Mathf.Max(config.minPredationSuccessChance, config.maxPredationSuccessChance);
        return Mathf.Clamp(chance, minChance, maxChance);
    }

    private float Normalize(float value, float min, float max)
    {
        float normalizedMin = Mathf.Min(min, max);
        float normalizedMax = Mathf.Max(min, max);

        if (Mathf.Approximately(normalizedMin, normalizedMax))
            return 0.5f;

        return Mathf.InverseLerp(normalizedMin, normalizedMax, value);
    }

    private bool IsValidPrey(BaseCreatureBehaviour prey)
    {
        return prey != null &&
               prey != creature &&
               prey.gameObject.activeInHierarchy;
    }

    private void ReleasePrey()
    {
        if (preyTarget is HerbivoreBehaviour herbivore)
        {
            herbivore.ReleaseCapture(creature);
        }
    }
}
