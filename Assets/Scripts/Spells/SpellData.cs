using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Spell/SpellData")]
public class SpellData : ScriptableObject
{
    public Sprite icon;
    public string spellName;
    

    public GameObject spellUI; // reference to UI prefab for interaction
    public SpellType type; // Teleportation, Resurrection, etc.

    public void Activate()
    {
        Debug.Log($"Activating spell: {spellName}");
        // Instantiate spellUI or resolve directly
    }
}
