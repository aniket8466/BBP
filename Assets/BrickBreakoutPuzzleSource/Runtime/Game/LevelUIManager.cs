
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System;
using System.Collections;
using FuncrateStudio;

public class LevelUIManager : MonoBehaviour
{
    [Header("Level UI")]
    [SerializeField] private GameObject panelBG;
    [SerializeField] private GameObject gameplayUIRoot;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text hintCountText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Transform lifeIconsRoot;
    [SerializeField] private Transform lifeLostEffectsRoot;
    [SerializeField] private Button pauseBtn;
    [SerializeField] private Button hintBtn;
    [SerializeField] private GameObject hintHolder;

    [Header("Pause UI")]
    [SerializeField] private GameObject panelPause;
    [SerializeField] private Button resumeBtn;
    [SerializeField] private Button restartBtn;
    [SerializeField] private Button homeBtn;

    [Header("Level Complete UI")]
    [SerializeField] private GameObject panelLevelComplete;
    [SerializeField] private Transform completionStarsRoot;
    [SerializeField, Min(0f)] private float levelCompletePanelDelay = 1.5f;
    [SerializeField, Min(0f)] private float retryPanelDelay = 0.5f;
    [SerializeField] private Button nextLevelBtn;
    [SerializeField] private Button homeLevelCompleteBtn;
    [SerializeField] private TMP_Text levelCompleteCountText;
    [SerializeField] private TMP_Text levelCoinCountText;
    [SerializeField] private TMP_Text levelCompleteTimeText;

    [Header("Level Failed UI")]
    [SerializeField] private GameObject panelLevelFailed;
    [SerializeField] private Button restartLevelBtn;
    [SerializeField] private Button adBtn;
    [SerializeField] private Button homeLevelFailedBtn;
    [SerializeField] private TMP_Text levelFailedCountText;
    [SerializeField] private TMP_Text levelFailedHintCountText;
    [SerializeField] private TMP_Text levelFailedTimeText;

    [Header("Add Hint")]
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private Button confirmationYesBtn, confirmationNoBtn;

    [Header("Navigation")]
    [SerializeField] private string homeSceneName = "Home";

    [SerializeField] private LevelGenerator levelGenerator;

    private int displayedLives = -1;
    private int displayedHints = -1;
    private bool navigationPending;
    private bool hasLostFocus;

    private float timeScaleBeforeConfirmation = 1f;

    private StarSystem starSystem;
    private UIAnimations uiAnimations;

    private int completionStarsEarned;
    private int lastCountdownSecond = -1;
    private int lastTimerFormattedSecond = -1;

    private Vector3 timerTextInitialScale;
    private string lastTimerDisplayText;

    private Action pendingConfirm;

    private void Start()
    {
        ButtonClickSfx.Attach(pauseBtn);
        ButtonClickSfx.Attach(hintBtn);
        ButtonClickSfx.Attach(resumeBtn);
        ButtonClickSfx.Attach(restartBtn);
        ButtonClickSfx.Attach(homeBtn);
        ButtonClickSfx.Attach(restartLevelBtn);
        ButtonClickSfx.Attach(adBtn);
        ButtonClickSfx.Attach(homeLevelCompleteBtn);
        ButtonClickSfx.Attach(nextLevelBtn);
        ButtonClickSfx.Attach(homeLevelFailedBtn);

        AddListeners();

        levelGenerator.LevelLoaded += HandleLevelLoaded;
        levelGenerator.LevelCompleted += HandleLevelCompleted;
        levelGenerator.LevelFailed += HandleLevelFailed;

        panelBG.SetActive(false);

        UpdateGameplayUIVisibility();

        starSystem = GetComponent<StarSystem>();
        if (starSystem == null)
            starSystem = gameObject.AddComponent<StarSystem>();

        starSystem.SetStars(completionStarsRoot);

        uiAnimations = UIAnimations.Instance;

        timerTextInitialScale = timerText.transform.localScale;

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

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            hasLostFocus = true;
            return;
        }

        if (!hasLostFocus)
            return;

        hasLostFocus = false;

        // Don't interrupt existing panels or completed/failed levels.
        if (panelPause.activeSelf ||
            panelLevelComplete.activeSelf ||
            panelLevelFailed.activeSelf ||
            (confirmationPanel != null && confirmationPanel.activeSelf))
            return;

        PauseGame();
    }

    private void AddListeners()
    {
        pauseBtn.onClick.AddListener(PauseGame);
        hintBtn.onClick.AddListener(UseHint);

        hintHolder.SetActive(GameManager.HintButtonEnabled);

        resumeBtn.onClick.AddListener(ResumeGame);
        restartBtn.onClick.AddListener(RestartLevel);
        restartLevelBtn.onClick.AddListener(RestartLevel);

        homeBtn.onClick.AddListener(LoadHome);
        homeLevelCompleteBtn.onClick.AddListener(LoadHome);
        homeLevelFailedBtn.onClick.AddListener(LoadHome);

        nextLevelBtn.onClick.AddListener(LoadNextLevel);
        adBtn.onClick.AddListener(ResumeWithCoinsOrAd);

        confirmationYesBtn.onClick.AddListener(ShowConfirmationReward);
        confirmationNoBtn.onClick.AddListener(HideConfirmation);
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

        confirmationYesBtn.onClick.RemoveListener(ShowConfirmationReward);
        confirmationNoBtn.onClick.RemoveListener(HideConfirmation);
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
        if (levelGenerator.IsLevelComplete ||
            levelGenerator.IsTimerExpired ||
            levelGenerator.IsOutOfLives ||
            panelPause.activeSelf)
            return;

        Time.timeScale = 0f;

        ShowPanel(panelPause, "Pause");
    }

    private void ResumeGame()
    {
        if (uiAnimations != null)
            uiAnimations.HideSection("Pause", FinishResume);
        else
            FinishResume();
    }

    private void FinishResume()
    {
        panelPause.SetActive(false);
        panelBG.SetActive(false);

        UpdateGameplayUIVisibility();

        Time.timeScale = 1f;
    }

    private void RestartLevel()
    {
        string section = panelLevelFailed.activeSelf ? "Retry" : "Pause";

        if (uiAnimations != null)
        {
            uiAnimations.HideSection(section, RestartLevelAfterAnimation);
            return;
        }

        RestartLevelAfterAnimation();
    }

    private void RestartLevelAfterAnimation()
    {
        Time.timeScale = 1f;

        HideAllPanels();

        levelGenerator.RestartLevel();

        LoadLevelData();
    }

    private void LoadNextLevel()
    {
        Time.timeScale = 1f;

        starSystem.HideStars();

        HideAllPanels();

        levelGenerator.LoadNextLevel();

        LoadLevelData();
    }

    private void LoadHome()
    {
        if (navigationPending)
            return;

        navigationPending = true;

        FSAdsManager ads = FSAdsManager.Instance;

        if (ads != null)
        {
            ads.ShowInterstitialAd(LoadHomeScene);
            return;
        }

        LoadHomeScene();
    }

    private void LoadHomeScene()
    {
        Time.timeScale = 1f;

        if (!Application.CanStreamedLevelBeLoaded(homeSceneName))
        {
            Debug.LogWarning(
                $"Home scene '{homeSceneName}' is not available in the build settings.",
                this);

            return;
        }

        SceneManager.LoadScene(homeSceneName);
    }

    private void UpdateTimerUI()
    {
        if (!levelGenerator.TimerEnabled)
        {
            lastTimerFormattedSecond = -1;

            SetTimerDisplayText("--:--");

            if (lastCountdownSecond != -1)
                ResetTimerCountdownPulse();

            return;
        }

        int secondsRemaining = Mathf.CeilToInt(
            levelGenerator.TimeRemainingSeconds);

        if (lastTimerFormattedSecond != secondsRemaining)
        {
            lastTimerFormattedSecond = secondsRemaining;

            SetTimerDisplayText(FormatTime(secondsRemaining));
        }

        if (secondsRemaining > 10)
        {
            if (lastCountdownSecond != -1)
                ResetTimerCountdownPulse();

            return;
        }

        if (secondsRemaining <= 0 ||
            secondsRemaining == lastCountdownSecond)
            return;

        lastCountdownSecond = secondsRemaining;

        Transform timerTransform = timerText.transform;

        timerTransform.DOKill();
        timerTransform.localScale = timerTextInitialScale;

        timerTransform.DOScale(timerTextInitialScale * 1.15f, 0.16f)
            .SetEase(Ease.InOutSine)
            .SetLoops(2, LoopType.Yoyo)
            .SetUpdate(true);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("Count");
    }

    private void ResetTimerCountdownPulse()
    {
        lastCountdownSecond = -1;

        Transform timerTransform = timerText.transform;

        timerTransform.DOKill();
        timerTransform.localScale = timerTextInitialScale;
    }

    private void SetTimerDisplayText(string value)
    {
        if (lastTimerDisplayText == value)
            return;

        lastTimerDisplayText = value;

        timerText.text = value;
    }

    private void UpdateHintCountUI()
    {
        int hintsAvailable = GameManager.Hints;

        if (displayedHints == hintsAvailable)
            return;

        displayedHints = hintsAvailable;

        string label = hintsAvailable.ToString();

        hintCountText.text = label;
    }

    private void UpdateLifeCountUI()
    {
        int lives = levelGenerator.LivesRemaining;

        if (displayedLives == lives)
            return;

        int previousLives = displayedLives;

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

                icon.DOScale(Vector3.one, 0.2f)
                    .SetEase(Ease.OutBack)
                    .SetUpdate(true);
            }
            else if (icon.gameObject.activeSelf)
            {
                if (previousLives >= 0 && lives < previousLives)
                    PlayLifeLostEffect(i);

                icon.DOKill();

                icon.gameObject.SetActive(false);
            }
        }
    }

    private void PlayLifeLostEffect(int heartIndex)
    {
        if (lifeLostEffectsRoot == null ||
            heartIndex < 0 ||
            heartIndex >= lifeLostEffectsRoot.childCount)
            return;

        ParticleSystem[] particleSystems =
            lifeLostEffectsRoot.GetChild(heartIndex)
                .GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            particleSystem.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);

            particleSystem.Play(true);
        }
    }

    private void UseHint()
    {
        if (GameManager.Hints > 0)
        {
            levelGenerator.ShowHint();
            return;
        }

        // Show confirmation to watch ad or spend coins for hint.
        pendingConfirm = ConfirWatchAdToGetHint;

        ShowConfirmation(pendingConfirm);
    }

    private void ResumeWithCoinsOrAd()
    {
        if (levelGenerator.IsTimerExpired)
        {
            if (GameManager.Coins >= 50)
            {
                GameManager.TrySpendCoins(50);

                GrantResumeTime();

                return;
            }

            ShowRewardedAd(GrantResumeTime);

            return;
        }

        if (!levelGenerator.IsOutOfLives)
            return;

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
        if (!levelGenerator.IsOutOfLives)
            return;

        levelGenerator.GrantResumeLife();

        if (uiAnimations != null)
            uiAnimations.HideSection("Retry", FinishResumeAfterLife);
        else
            FinishResumeAfterLife();
    }

    private void GrantResumeTime()
    {
        if (!levelGenerator.IsTimerExpired)
            return;

        levelGenerator.GrantResumeTime(10f);

        if (uiAnimations != null)
            uiAnimations.HideSection("Retry", FinishResumeAfterLife);
        else
            FinishResumeAfterLife();
    }

    private void FinishResumeAfterLife()
    {
        panelLevelFailed.SetActive(false);
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
            ShowConfirmation(null);
            return;
        }

        HideConfirmation();

        ads.ShowRewardedAd(() => reward?.Invoke());
    }

    private void ShowConfirmation(Action onConfirm)
    {
        // Show confirmation panel with Yes/No buttons.
        if (confirmationPanel != null)
        {
            confirmationPanel.SetActive(true);

            timeScaleBeforeConfirmation = Time.timeScale;

            Time.timeScale = 0f;

            pendingConfirm = onConfirm;
        }
    }

    private void ConfirWatchAdToGetHint()
    {
        // Show rewarded ad to get hint.
        ShowRewardedAd(() =>
        {
            GameManager.AddHints(1);
            levelGenerator.ShowHint();
        });
    }

    private void ShowConfirmationReward()
    {
        Action action = pendingConfirm;

        HideConfirmation();

        action?.Invoke();
    }

    private void HideConfirmation()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        Time.timeScale = timeScaleBeforeConfirmation;

        if (!panelPause.activeSelf &&
            !panelLevelComplete.activeSelf &&
            !panelLevelFailed.activeSelf)
        {
            panelBG.SetActive(false);
        }

        UpdateGameplayUIVisibility();
    }

    private void HandleLevelCompleted(int levelNumber)
    {
        completionStarsEarned = Mathf.Clamp(
            levelGenerator.LivesRemaining, 0, 3);

        int coinsBeforeAward = GameManager.Coins;

        GameManager.AwardCoinsForLevelStars(
            levelNumber,
            completionStarsEarned);

        int coinsReceivedInLevel = Mathf.Max(
            0,
            GameManager.Coins - coinsBeforeAward);

        UpdateResultsText(
            levelCompleteCountText,
            levelCoinCountText,
            levelCompleteTimeText);

        levelCoinCountText.text = $"+{coinsReceivedInLevel}";

        StartCoroutine(ShowLevelCompletePanelAfterDelay());
    }

    private IEnumerator ShowLevelCompletePanelAfterDelay()
    {
        yield return new WaitForSecondsRealtime(levelCompletePanelDelay);

        ShowPanel(
            panelLevelComplete,
            "Complete",
            () => starSystem.PresentStars(completionStarsEarned));

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX("StarBlast");
    }

    private void HandleLevelFailed(int levelNumber)
    {
        UpdateResultsText(
            levelFailedCountText,
            levelFailedHintCountText,
            levelFailedTimeText);

        adBtn.interactable =
            levelGenerator.IsOutOfLives ||
            levelGenerator.IsTimerExpired;

        StartCoroutine(ShowRetryPanelAfterDelay());
    }

    private IEnumerator ShowRetryPanelAfterDelay()
    {
        yield return new WaitForSecondsRealtime(retryPanelDelay);

        ShowPanel(panelLevelFailed, "Retry");
    }

    private void UpdateResultsText(
        TMP_Text countText,
        TMP_Text hintsText,
        TMP_Text timeText)
    {
        countText.text = $"LEVEL {levelGenerator.CurrentLevel:00}";

        hintsText.text = $"Hints: {GameManager.Hints}";

        timeText.text =
            $"Time: {FormatTime(levelGenerator.TimeRemainingSeconds)}";
    }

    private void ShowPanel(
        GameObject panel,
        string animationSection = null,
        UnityEngine.Events.UnityAction onAppearCompleted = null)
    {
        panelBG.SetActive(true);

        panel.SetActive(true);

        UpdateGameplayUIVisibility();

        if (!string.IsNullOrEmpty(animationSection) &&
            uiAnimations != null)
        {
            uiAnimations.PlayAnimation(
                animationSection,
                onAppearCompleted);
        }
        else
        {
            onAppearCompleted?.Invoke();
        }
    }

    private void UpdateGameplayUIVisibility()
    {
        bool modalOpen =
            panelPause.activeSelf ||
            panelLevelComplete.activeSelf ||
            panelLevelFailed.activeSelf ||
            (confirmationPanel != null && confirmationPanel.activeSelf);

        gameplayUIRoot.SetActive(!modalOpen);

        levelGenerator.SetPuzzleVisible(!modalOpen);
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
        int totalSeconds = Mathf.Max(
            0,
            Mathf.CeilToInt(seconds));

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
