using System.Collections.Generic;
using UnityEngine;

public class CreatureManager : MonoBehaviour
{
    // Singleton minta a könnyû hozzáférés érdekében
    public static CreatureManager Instance { get; private set; }

    // Lista a CreatureBehavior példányok tárolására
    private List<CreatureBehavior> creatures = new List<CreatureBehavior>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void OnEnable()
    {
        CreatureBehavior.OnCreatureSpawned += RegisterCreature;
        CreatureBehavior.OnCreatureDestroyed += UnregisterCreature;
    }

    private void OnDisable()
    {
        CreatureBehavior.OnCreatureSpawned -= RegisterCreature;
        CreatureBehavior.OnCreatureDestroyed -= UnregisterCreature;
    }

    // Metódus egy creature hozzáadásához
    public void RegisterCreature(CreatureBehavior creature)
    {
        if (!creatures.Contains(creature))
        {
            creatures.Add(creature);
        }
    }

    // Metódus egy creature eltávolításához
    public void UnregisterCreature(CreatureBehavior creature)
    {
        if (creatures.Contains(creature))
        {
            creatures.Remove(creature);
        }
    }

    // Metódus egy creature lekérdezéséhez index alapján
    public CreatureBehavior GetCreatureByIndex(int index)
    {
        if (index >= 0 && index < creatures.Count)
        {
            return creatures[index];
        }
        else
        {
            Debug.LogError("Index out of range");
            return null;
        }
    }

    // Metódus az összes creature számának lekérdezésére
    public int GetCreaturesCount()
    {
        return creatures.Count;
    }
}
