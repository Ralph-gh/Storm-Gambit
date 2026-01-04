using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "StormGambit/Mages/Mage Database")]
public class MageDatabase : ScriptableObject
{
    public List<MageData> mages = new();

    public List<MageData> GetAll() => mages;

    public MageData GetById(MageId id)
    {
        return mages.Find(m => m && m.mageId == id);
    }
}
