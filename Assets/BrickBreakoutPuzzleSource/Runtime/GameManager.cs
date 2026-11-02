using UnityEngine;

/// <summary>Stores lightweight local progression shared by the menu and gameplay scenes.</summary>
public sealed class GameManager : MonoBehaviour
{
    public const int MaximumLevel = 2000;
    private const string HighestUnlockedKey = "ArrowEscape.HighestUnlockedLevel";
    private const string SelectedLevelKey = "ArrowEscape.SelectedLevel";
    private const string CoinsKey = "ArrowEscape.Coins";
    private const string HintsKey = "ArrowEscape.Hints";
    private const string LevelStarsKeyPrefix = "ArrowEscape.LevelStars.";
    private static GameManager instance;

    public static int HighestUnlockedLevel => Mathf.Clamp(PlayerPrefs.GetInt(HighestUnlockedKey, 1), 1, MaximumLevel);
    public static int SelectedLevel => Mathf.Clamp(PlayerPrefs.GetInt(SelectedLevelKey, HighestUnlockedLevel), 1, HighestUnlockedLevel);
    public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);
    public static int Hints => PlayerPrefs.GetInt(HintsKey, 3);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBeforeSceneLoad()
    {
        if (instance != null) return;
        GameObject managerObject = new GameObject(nameof(GameManager));
        instance = managerObject.AddComponent<GameManager>();
        DontDestroyOnLoad(managerObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SaveHighestUnlockedLevel(HighestUnlockedLevel);
        SelectLevel(SelectedLevel);
    }

    public static bool IsLevelUnlocked(int level) => level >= 1 && level <= HighestUnlockedLevel;

    public static void AddCoins(int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt(CoinsKey, Coins + amount);
        PlayerPrefs.Save();
    }

    public static bool TrySpendCoins(int amount)
    {
        if (amount <= 0 || Coins < amount) return false;
        PlayerPrefs.SetInt(CoinsKey, Coins - amount);
        PlayerPrefs.Save();
        return true;
    }

    public static void AddHints(int amount)
    {
        if (amount <= 0) return;
        PlayerPrefs.SetInt(HintsKey, Hints + amount);
        PlayerPrefs.Save();
    }

    public static bool TryUseHint()
    {
        if (Hints <= 0) return false;
        PlayerPrefs.SetInt(HintsKey, Hints - 1);
        PlayerPrefs.Save();
        return true;
    }

    public static int GetLevelStars(int level)
    {
        int safeLevel = Mathf.Clamp(level, 1, MaximumLevel);
        return Mathf.Clamp(PlayerPrefs.GetInt(LevelStarsKeyPrefix + safeLevel, 0), 0, 3);
    }

    public static int AwardCoinsForLevelStars(int level, int starCount)
    {
        int safeLevel = Mathf.Clamp(level, 1, MaximumLevel);
        int newStars = Mathf.Clamp(starCount, 0, 3);
        string key = LevelStarsKeyPrefix + safeLevel;
        int previousStars = Mathf.Clamp(PlayerPrefs.GetInt(key, 0), 0, 3);
        int coinReward = Mathf.Max(0, newStars - previousStars);
        if (coinReward <= 0) return 0;

        PlayerPrefs.SetInt(key, newStars);
        AddCoins(coinReward);
        return coinReward;
    }

    public static void SelectLevel(int level)
    {
        int safeLevel = Mathf.Clamp(level, 1, HighestUnlockedLevel);
        PlayerPrefs.SetInt(SelectedLevelKey, safeLevel);
        PlayerPrefs.Save();
    }

    public static void SaveHighestUnlockedLevel(int level)
    {
        int safeLevel = Mathf.Clamp(level, 1, MaximumLevel);
        int previousHighest = HighestUnlockedLevel;
        int newHighest = Mathf.Max(previousHighest, safeLevel);
        PlayerPrefs.SetInt(HighestUnlockedKey, newHighest);
        if (newHighest > previousHighest) PlayerPrefs.SetInt(SelectedLevelKey, newHighest);
        PlayerPrefs.Save();
    }
}
