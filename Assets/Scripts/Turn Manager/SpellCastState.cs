using System;

public static class SpellCastState
{
    public static bool IsCasting { get; private set; }

    public static event Action<bool> CastingChanged;

    private static int activeLocks = 0;

    public static void BeginCast()
    {
        activeLocks++;

        if (!IsCasting)
        {
            IsCasting = true;
            CastingChanged?.Invoke(true);
        }
    }

    public static void EndCast()
    {
        activeLocks = Math.Max(0, activeLocks - 1);

        if (activeLocks == 0 && IsCasting)
        {
            IsCasting = false;
            CastingChanged?.Invoke(false);
        }
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        activeLocks = 0;
        IsCasting = false;
        CastingChanged = null;
    }
}