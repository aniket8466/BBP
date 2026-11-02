using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System;
using FuncrateStudio;


public class LevelUIManager : MonoBehaviour
{
    [Header("Level UI")]
    [SerializeField] private GameObject panelBG;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text hintCountText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Transform lifeIconsRoot;
    [SerializeField] private Button pauseBtn;
    [SerializeField] private Button hintBtn;

    [Header("Pause UI")]
    [SerializeField] private GameObject panelPause;
    [SerializeField] private Button resumeBtn;
    [SerializeField] private Button restartBtn;
    [SerializeField] private Button homeBtn;

    [Header("Level Complete UI")]
    [SerializeField] private GameObject panelLevelComplete;
    [SerializeField] private Button nextLevelBtn;
    [SerializeField] private Button homeLevelCompleteBtn;
    [SerializeField] private TMP_Text levelCompleteCountText;
    [SerializeField] private TMP_Text levelCompleteHintCountText;
    [SerializeField] private TMP_Text levelCompleteTimeText;

    [Header("Level Failed UI")]
    [SerializeField] private GameObject panelLevelFailed;
    [SerializeField] private Button restartLevelBtn;
    [SerializeField] private Button adBtn;
    [SerializeField] private Button homeLevelFailedBtn;
    [SerializeField] private TMP_Text levelFailedCountText;
    [SerializeField] private TMP_Text levelFailedHintCountText;
    [SerializeField] private TMP_Text levelFailedTimeText;

    [Header("Navigation")]
    [SerializeField] private string homeSceneName = "Home";

    private LevelGenerator levelGenerator;
    private int displayedLives = -1;
    private int displayedHints = -1;
    private GameObject confirmationPanel;
    private bool navigationPending;
    private float timeScaleBeforeConfirmation = 1f;
    private StarSystem starSystem;
    private Transform completionStarsRoot;
    private GameObject gameplayUIRoot;

    private void Start()
    {
        levelGenerator = FindObjectOfType<LevelGenerator>();
        Button[] sceneButtons = FindObjectsOfType<Button>(true);
        foreach (Button button in sceneButtons) ButtonClickSfx.Attach(button);
        AddListeners();
        levelGenerator.LevelLoaded += HandleLevelLoaded;
        levelGenerator.LevelCompleted += HandleLevelCompleted;
        levelGenerator.LevelFailed += HandleLevelFailed;
        panelBG.SetActive(false);
        gameplayUIRoot = FindChild(panelBG.transform.parent, "Game")?.gameObject;
        UpdateGameplayUIVisibility();
        starSystem = GetComponent<StarSystem>();
        if (starSystem == null) starSystem = gameObject.AddComponent<StarSystem>();
        completionStarsRoot = FindChild(panelLevelComplete.transform, "StarHolder");
        starSystem.SetStars(completionStarsRoot);
        UpdateLevelUI();
        UpdateLifeCountUI();
        UpdateHintCountUI();
    }

    private void Update()
    {
        UpdateTimerUI();
        UpdateLifeCountUI();
        UpdateHintCountUI();
    }

    private void AddListeners()
    {
        pauseBtn.onClick.AddListener(PauseGame);
        hintBtn.onClick.AddListener(UseHint);
        resumeBtn.onClick.AddListener(ResumeGame);
        restartBtn.onClick.AddListener(RestartLevel);
        restartLevelBtn.onClick.AddListener(RestartLevel);
        homeBtn.onClick.AddListener(LoadHome);
        homeLevelCompleteBtn.onClick.AddListener(LoadHome);
        homeLevelFailedBtn.onClick.AddListener(LoadHome);
        nextLevelBtn.onClick.AddListener(LoadNextLevel);
        adBtn.onClick.AddListener(ResumeWithCoinsOrAd);
    }

    private void RemoveListeners()
    {
        pauseBtn.onClick.RemoveListener(PauseGame);
        hintBtn.onClick.RemoveListener(UseHint);
        resumeBtn.onClick.RemoveListener(ResumeGame);
        restartBtn.onClick.RemoveListener(RestartLevel);
        restartLevelBtn.onClick.RemoveListener(RestartLevel);
        homeBtn.onClick.RemoveListener(LoadHome);
        homeLevelCompleteBtn.onClick.RemoveListener(LoadHome);
        homeLevelFailedBtn.onClick.RemoveListener(LoadHome);
        nextLevelBtn.onClick.RemoveListener(LoadNextLevel);
        adBtn.onClick.RemoveListener(ResumeWithCoinsOrAd);
    }

    private void LoadLevelData()
    {
        UpdateLevelUI();
        UpdateTimerUI();
        UpdateHintCountUI();
        UpdateLifeCountUI();
    }

    private void HandleLevelLoaded(int levelNumber)
    {
        UpdateLevelUI();
    }

    private void UpdateLevelUI()
    {
        levelText.text = $"LEVEL {levelGenerator.CurrentLevel:00}";
    }

    private void PauseGame()
    {
        if (levelGenerator.IsLevelComplete || levelGenerator.IsTimerExpired || levelGenerator.IsOutOfLives) return;
        Time.timeScale = 0f;
        ShowPanel(panelPause);
    }

    private void ResumeGame()
    {
        HidePanel(panelPause);
        panelBG.SetActive(false);
        UpdateGameplayUIVisibility();
        Time.timeScale = 1f;
    }

    private void RestartLevel()
    {
        Time.timeScale = 1f;
        HideAllPanels();
        levelGenerator.RestartLevel();
        LoadLevelData();
    }

    private void LoadNextLevel()
    {
        Time.timeScale = 1f;
        HideAllPanels();
        levelGenerator.LoadNextLevel();
        LoadLevelData();
    }

    private void LoadHome()
    {
        if (navigationPending) return;
        navigationPending = true;
        FSAdsManager ads = FSAdsManager.Instance;
        if (ads != null) { ads.ShowInterstitialAd(LoadHomeScene); return; }
        LoadHomeScene();
    }

    private void LoadHomeScene()
    {
        Time.timeScale = 1f;
        if (!Application.CanStreamedLevelBeLoaded(homeSceneName))
        {
            Debug.LogWarning($"Home scene '{homeSceneName}' is not available in the build settings.", this);
            return;
        }
        SceneManager.LoadScene(homeSceneName);
    }

    private void UpdateTimerUI()
    {
        timerText.text = levelGenerator.TimerEnabled ? FormatTime(levelGenerator.TimeRemainingSeconds) : "--:--";
    }

    private void UpdateHintCountUI()
    {
        int hintsAvailable = GameManager.Hints;
        if (displayedHints == hintsAvailable) return;
        displayedHints = hintsAvailable;
        string label = hintsAvailable.ToString();
        hintCountText.text = label;
    }

    private void UpdateLifeCountUI()
    {
        int lives = levelGenerator.LivesRemaining;
        if (displayedLives == lives) return;
        displayedLives = lives;
        int iconCount = lifeIconsRoot.childCount;
        for (int i = 0; i < iconCount; i++)
        {
            Transform icon = lifeIconsRoot.GetChild(i);
            bool shouldShow = i < lives;
            icon.DOKill();
            if (shouldShow)
            {
                icon.gameObject.SetActive(true);
                icon.localScale = Vector3.one * 0.7f;
                icon.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
            }
            else if (icon.gameObject.activeSelf)
            {
                icon.DOScale(Vector3.zero, 0.18f).SetUpdate(true).OnComplete(() => icon.gameObject.SetActive(false));
            }
        }
    }

    private void UseHint()
    {
        if (GameManager.Hints > 0) { levelGenerator.ShowHint(); return; }
        ShowConfirmation("Watch an ad to get 1 hint?", () => ShowRewardedAd(() => GameManager.AddHints(1)));
    }

    private void ResumeWithCoinsOrAd()
    {
        if (!levelGenerator.IsOutOfLives) return;
        if (GameManager.Coins >= 50)
        {
            GameManager.TrySpendCoins(50);
            GrantResumeLife();
            return;
        }

        ShowRewardedAd(GrantResumeLife);
    }

    private void GrantResumeLife()
    {
        if (!levelGenerator.IsOutOfLives) return;
        levelGenerator.GrantResumeLife();
        HidePanel(panelLevelFailed);
        panelBG.SetActive(false);
        UpdateGameplayUIVisibility();
        Time.timeScale = 1f;
        displayedLives = -1;
        UpdateLifeCountUI();
    }

    private void ShowRewardedAd(Action reward)
    {
        FSAdsManager ads = FSAdsManager.Instance;
        if (ads == null || !ads.IsRewardedAdReady)
        {
            ShowConfirmation("Ad is not ready. Please try again.", null);
            return;
        }
        HideConfirmation();
        ads.ShowRewardedAd(() => reward?.Invoke());
    }

    private void ShowConfirmation(string message, Action onConfirm)
    {
        if (confirmationPanel == null) confirmationPanel = CreateConfirmationPanel();
        timeScaleBeforeConfirmation = Time.timeScale;
        Time.timeScale = 0f;
        panelBG.SetActive(true);
        pendingConfirm = onConfirm;
        confirmationPanel.SetActive(true);
        UpdateGameplayUIVisibility();
        confirmationPanel.transform.Find("Message").GetComponent<TMP_Text>().text = message;
        Button yes = confirmationPanel.transform.Find("Confirm").GetComponent<Button>();
        yes.onClick.RemoveAllListeners();
        if (onConfirm == null) yes.gameObject.SetActive(false);
        else { yes.gameObject.SetActive(true); yes.onClick.AddListener(ShowConfirmationReward); }
        confirmationPanel.transform.Find("Cancel").GetComponent<Button>().onClick.RemoveAllListeners();
        confirmationPanel.transform.Find("Cancel").GetComponent<Button>().onClick.AddListener(HideConfirmation);
    }

    private GameObject CreateConfirmationPanel()
    {
        GameObject root = new GameObject("AdConfirmation", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(panelBG.transform, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(560, 280);
        root.GetComponent<Image>().color = new Color(.12f, .14f, .22f, .98f);
        CreateLabel(root.transform, "Message", "", new Vector2(0, 55), new Vector2(500, 100), 30);
        CreateActionButton(root.transform, "Confirm", "Watch Ad", new Vector2(-125, -85), ShowConfirmationReward);
        CreateActionButton(root.transform, "Cancel", "Cancel", new Vector2(125, -85), HideConfirmation);
        root.SetActive(false);
        return root;
    }

    private Action pendingConfirm;
    private void ShowConfirmationReward() { Action action = pendingConfirm; HideConfirmation(); action?.Invoke(); }

    private Button CreateActionButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(220, 72); rect.anchoredPosition = position;
        buttonObject.GetComponent<Image>().color = new Color(.18f, .55f, .85f);
        Button button = buttonObject.GetComponent<Button>(); if (action != null) button.onClick.AddListener(action);
        ButtonClickSfx.Attach(button);
        CreateLabel(buttonObject.transform, "Label", label, Vector2.zero, new Vector2(210, 65), 24);
        return button;
    }

    private static void CreateLabel(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = size; rect.anchoredPosition = position;
        TMP_Text text = labelObject.GetComponent<TMP_Text>(); text.text = value; text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.color = Color.white;
    }

    private void HideConfirmation()
    {
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        Time.timeScale = timeScaleBeforeConfirmation;
        if (!panelPause.activeSelf && !panelLevelComplete.activeSelf && !panelLevelFailed.activeSelf)
            panelBG.SetActive(false);
        UpdateGameplayUIVisibility();
    }

    private void HandleLevelCompleted(int levelNumber)
    {
        int starsEarned = Mathf.Clamp(levelGenerator.LivesRemaining, 0, 3);
        GameManager.AwardCoinsForLevelStars(levelNumber, starsEarned);
        starSystem.PresentStars(starsEarned);
        UpdateResultsText(levelCompleteCountText, levelCompleteHintCountText, levelCompleteTimeText);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("StarBlast");
        ShowPanel(panelLevelComplete);
    }

    private static Transform FindChild(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            if (children[i].name == childName) return children[i];
        return null;
    }

    private void HandleLevelFailed(int levelNumber)
    {
        UpdateResultsText(levelFailedCountText, levelFailedHintCountText, levelFailedTimeText);
        adBtn.interactable = levelGenerator.IsOutOfLives;
        ShowPanel(panelLevelFailed);
    }

    private void UpdateResultsText(TMP_Text countText, TMP_Text hintsText, TMP_Text timeText)
    {
        countText.text = $"LEVEL {levelGenerator.CurrentLevel:00}";
        hintsText.text = $"Hints: {GameManager.Hints}";
        timeText.text = $"Time: {FormatTime(levelGenerator.TimeRemainingSeconds)}";
    }

    private void ShowPanel(GameObject panel)
    {
        panelBG.SetActive(true);
        panel.SetActive(true);
        UpdateGameplayUIVisibility();
    }

    private void HidePanel(GameObject panel)
    {
        if (panel != null) panel.SetActive(false);
    }

    private void UpdateGameplayUIVisibility()
    {
        if (gameplayUIRoot == null) return;
        bool modalOpen = (panelPause != null && panelPause.activeSelf)
            || (panelLevelComplete != null && panelLevelComplete.activeSelf)
            || (panelLevelFailed != null && panelLevelFailed.activeSelf)
            || (confirmationPanel != null && confirmationPanel.activeSelf);
        gameplayUIRoot.SetActive(!modalOpen);
    }

    private void HideAllPanels()
    {
        panelPause.SetActive(false);
        panelLevelComplete.SetActive(false);
        panelLevelFailed.SetActive(false);
        panelBG.SetActive(false);
        UpdateGameplayUIVisibility();
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private void OnDestroy()
    {
        RemoveListeners();
        levelGenerator.LevelLoaded -= HandleLevelLoaded;
        levelGenerator.LevelCompleted -= HandleLevelCompleted;
        levelGenerator.LevelFailed -= HandleLevelFailed;
        Time.timeScale = 1f;
    }
}




