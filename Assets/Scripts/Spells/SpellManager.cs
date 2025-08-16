using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellManager : MonoBehaviour
{
    public static SpellManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
   
    



    public void ResolveSpell(CardData data, object context = null)
    {
        switch (data.spellType)
        {
            case SpellType.Teleport:
                if (data.spellUI != null)
                {
                    Instantiate(data.spellUI, GameObject.Find("Canvas").transform);
                }
                else
                {
                    Debug.LogWarning("Teleport spell has no UI assigned.");
                }
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

