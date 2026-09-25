using UnityEngine;

public class SpellCastLockToken : MonoBehaviour
{
    private bool acquired;

    private void OnEnable()
    {
        if (acquired)
            return;

        acquired = true;
        SpellCastState.BeginCast();
    }

    private void OnDisable()
    {
        Release();
    }

    private void OnDestroy()
    {
        Release();
    }

    private void Release()
    {
        if (!acquired)
            return;

        acquired = false;
        SpellCastState.EndCast();
    }
}