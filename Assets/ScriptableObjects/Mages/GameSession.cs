using UnityEngine;

public static class GameSession
{
    public static MageData SelectedMage { get; private set; }

    public static void SetSelectedMage(MageData mage)
    {
        SelectedMage = mage;
        Debug.Log("[GameSession] Selected Mage: " + (mage ? mage.displayName : "NULL"));
    }
}
