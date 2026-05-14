using System.Text;
using UnityEngine;

public class SelectedCreatureStatsUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private bool showPanel = true;
    [SerializeField] private Vector2 panelOffset = new Vector2(16f, 16f);
    [SerializeField] private float panelWidth = 360f;
    [SerializeField] private float panelHeight = 380f;
    [SerializeField] private int headerFontSize = 16;
    [SerializeField] private int bodyFontSize = 13;

    private readonly StringBuilder builder = new StringBuilder(768);

    private CreatureSelectionManager selectionManager;
    private BaseCreatureBehaviour selectedCreature;
    private bool isSubscribed;

    private GUIStyle headerStyle;
    private GUIStyle bodyStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (FindObjectOfType<SelectedCreatureStatsUI>() != null)
            return;

        GameObject panelObject = new GameObject("SelectedCreatureStatsUI");
        panelObject.AddComponent<SelectedCreatureStatsUI>();
    }

    private void OnEnable()
    {
        TryAttachSelectionManager();
    }

    private void OnDisable()
    {
        DetachSelectionManager();
    }

    private void Update()
    {
        if (!isSubscribed || selectionManager == null)
        {
            TryAttachSelectionManager();
        }

        if (selectedCreature != null && !IsSelectable(selectedCreature))
        {
            selectedCreature = null;
        }
    }

    private void TryAttachSelectionManager()
    {
        if (isSubscribed)
            return;

        if (selectionManager == null)
        {
            selectionManager = CreatureSelectionManager.Instance ?? FindObjectOfType<CreatureSelectionManager>();
        }

        if (selectionManager == null)
        {
            GameObject selectionManagerObject = new GameObject("CreatureSelectionManager");
            selectionManager = selectionManagerObject.AddComponent<CreatureSelectionManager>();
        }

        if (selectionManager == null)
            return;

        selectionManager.SelectionChanged += HandleSelectionChanged;
        isSubscribed = true;
        HandleSelectionChanged(selectionManager.SelectedCreature);
    }

    private void DetachSelectionManager()
    {
        if (!isSubscribed || selectionManager == null)
            return;

        selectionManager.SelectionChanged -= HandleSelectionChanged;
        isSubscribed = false;
    }

    private void HandleSelectionChanged(BaseCreatureBehaviour creature)
    {
        selectedCreature = IsSelectable(creature) ? creature : null;
    }

    private void OnGUI()
    {
        if (!showPanel)
            return;

        EnsureStyles();

        Rect panelRect = new Rect(panelOffset.x, panelOffset.y, panelWidth, panelHeight);
        GUI.Box(panelRect, GUIContent.none);

        Rect contentRect = new Rect(panelRect.x + 10f, panelRect.y + 8f, panelRect.width - 20f, panelRect.height - 16f);
        Rect headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 24f);
        Rect bodyRect = new Rect(contentRect.x, contentRect.y + 26f, contentRect.width, contentRect.height - 26f);

        GUI.Label(headerRect, "Selected creature", headerStyle);

        if (selectedCreature == null)
        {
            GUI.Label(bodyRect, "No creature selected.\nLeft click on a creature to select it.", bodyStyle);
            return;
        }

        BuildCreatureStatsText(selectedCreature, builder);
        GUI.Label(bodyRect, builder.ToString(), bodyStyle);
    }

    private void BuildCreatureStatsText(BaseCreatureBehaviour creature, StringBuilder output)
    {
        output.Clear();

        Vector3 position = creature.transform.position;
        AgeManager age = creature.AgeManager;
        EnergyManager energy = creature.EnergyManager;
        MovementManager movement = creature.MovementManager;
        ObservationManager observation = creature.ObservationManager;
        ReproductionManager reproduction = creature.ReproductionManager;

        output.Append("Name: ").Append(creature.name).Append('\n');
        output.Append("Id: ").Append(creature.GetInstanceID()).Append('\n');
        output.Append("Species: ").Append(GetSpeciesName(creature)).Append('\n');
        output.Append("Sex: ").Append(creature.Sex).Append('\n');
        output.Append("State: ").Append(creature.CurrentStateType).Append('\n');
        output.Append("Pos: ")
            .Append(position.x.ToString("0.0")).Append(", ")
            .Append(position.y.ToString("0.0")).Append(", ")
            .Append(position.z.ToString("0.0")).Append('\n');

        output.Append("Age: ").Append(FormatFloat(age != null ? age.Age : -1f)).Append('\n');
        output.Append("Maturity: ").Append(FormatPercent(age != null ? age.MaturityFraction : -1f)).Append('\n');

        float energyValue = energy != null ? energy.EnergyLevel : -1f;
        float maxEnergy = energy != null ? energy.CurrentMaxEnergy : -1f;
        output.Append("Energy: ")
            .Append(FormatFloat(energyValue))
            .Append(" / ")
            .Append(FormatFloat(maxEnergy))
            .Append('\n');

        output.Append("Weight: ").Append(FormatFloat(creature.Weight)).Append('\n');
        output.Append("Speed: ").Append(FormatFloat(movement != null ? movement.MoveSpeed : -1f)).Append('\n');
        output.Append("Sense: ").Append(FormatFloat(observation != null ? observation.SenseRadius : -1f)).Append('\n');
        output.Append("Desirability: ").Append(FormatFloat(reproduction != null ? reproduction.Desirability : -1f)).Append('\n');
        output.Append("Last AI action: ").Append(creature.LastUtilityAISelectedAction).Append('\n');

        CreatureUtilityBehaviorData utilityProfile = UtilityBehaviorScoring.Sanitize(creature.UtilityBehaviorProfile);
        output.Append("Utility keep/food/mate/wander: ")
            .Append(FormatFloat(utilityProfile.keepCurrentStateWeight)).Append(" / ")
            .Append(FormatFloat(utilityProfile.foodActionWeight)).Append(" / ")
            .Append(FormatFloat(utilityProfile.searchMateWeight)).Append(" / ")
            .Append(FormatFloat(utilityProfile.wanderWeight)).Append('\n');

        if (creature is HerbivoreBehaviour herbivore)
        {
            output.Append("Social: ").Append(herbivore.SocialStrategy).Append('\n');
            output.Append("Agility: ").Append(FormatFloat(herbivore.Agility)).Append('\n');
            output.Append("Threatened: ").Append(herbivore.IsThreatened ? "Yes" : "No").Append('\n');
        }
        else if (creature is PredatorBehaviour predator)
        {
            output.Append("Strength: ").Append(FormatFloat(predator.Strength)).Append('\n');
        }
    }

    private static string GetSpeciesName(BaseCreatureBehaviour creature)
    {
        if (creature is HerbivoreBehaviour)
            return "Herbivore";

        if (creature is PredatorBehaviour)
            return "Predator";

        return creature.GetType().Name;
    }

    private static string FormatFloat(float value)
    {
        return value >= 0f ? value.ToString("0.00") : "-";
    }

    private static string FormatPercent(float value)
    {
        return value >= 0f ? (value * 100f).ToString("0.0") + "%" : "-";
    }

    private void EnsureStyles()
    {
        if (headerStyle != null && bodyStyle != null)
            return;

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(10, headerFontSize),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            wordWrap = false
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(9, bodyFontSize),
            alignment = TextAnchor.UpperLeft,
            wordWrap = true
        };
    }

    private static bool IsSelectable(BaseCreatureBehaviour creature)
    {
        return creature != null &&
               creature.gameObject.activeInHierarchy &&
               !creature.IsDespawnQueued;
    }
}