using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellManager : MonoBehaviour
{
    public static SpellManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public void ResolveSpell(CardData data, object context = null)
    {
        switch (data.spellType)
        {
            case SpellType.Teleport:
                Debug.Log("Teleport spell resolving.");
                // Add teleport logic
                break;

            case SpellType.Resurrect:
                Debug.Log("Resurrect spell resolving.");
                // Add resurrection logic
                break;

            default:
                Debug.Log("No spell effect assigned.");
                break;
        }
    }
}

