using System;
using UnityEngine;

public sealed class LifeManager : MonoBehaviour
{
    public event Action<int> LivesChanged;
    public event Action OutOfLives;

    public int StartingLives { get; private set; }
    public int LivesRemaining { get; private set; }
    public bool IsOutOfLives => LivesRemaining <= 0;

    public void ResetLives(int startingLives)
    {
        StartingLives = Mathf.Max(1, startingLives);
        LivesRemaining = StartingLives;
        LivesChanged?.Invoke(LivesRemaining);
    }

    public bool LoseLife()
    {
        if (IsOutOfLives) return false;
        LivesRemaining--;
        LivesChanged?.Invoke(LivesRemaining);
        if (IsOutOfLives) OutOfLives?.Invoke();
        return true;
    }

    public void AddLife()
    {
        LivesRemaining++;
        LivesChanged?.Invoke(LivesRemaining);
    }
}
