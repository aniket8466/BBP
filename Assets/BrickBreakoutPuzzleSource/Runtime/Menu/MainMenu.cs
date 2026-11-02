using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using FuncrateStudio;


public class MainMenu : MonoBehaviour
{
    [Header("Level Selection")]
    [SerializeField] private MenuLevelManager levelMenu;

    [Header("Panels")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject coinsList;
    [SerializeField] private GameObject settingsPanel;

    [Header("Menu Buttons")]
    [SerializeField] private Button shopButton;
    [SerializeField] private Button coinButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button shopClose;
    [SerializeField] private Button settingsClose;
    [SerializeField] private Button exitButton;

    [Header("Shop Buttons")]
    [SerializeField] private Button coinPack10Button;
    [SerializeField] private Button coinPack100Button;
    [SerializeField] private Button coinPack200Button;
    [SerializeField] private Button hintPack1Button;
    [SerializeField] private Button hintPack5Button;
    [SerializeField] private Button hintPack10Button;

    [Header("Menu Settings")]
    [SerializeField] private Toggle coinsToggle;
    [SerializeField] private Toggle hintsToggle;

    [Header("Audio Settings")]
    [SerializeField] private Toggle bgmToggle;
    [SerializeField] private Toggle sfxToggle;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private AudioManager audioManager;

    [Header("UI Text")]
    [SerializeField] private TMP_Text coinBalanceText;

    private bool coinPackInProgress;
    private int adsRewarded, adsRequired, pendingCoinReward;
    private bool currentAdRewarded, currentAdClosed;

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Mathf.Min(60, Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value));
    }

    private void Start()
    {
        ButtonClickSfx.Attach(shopButton);
        ButtonClickSfx.Attach(coinButton);
        ButtonClickSfx.Attach(settingsButton);
        ButtonClickSfx.Attach(shopClose);
        ButtonClickSfx.Attach(settingsClose);
        ButtonClickSfx.Attach(exitButton);
        ButtonClickSfx.Attach(coinPack10Button);
        ButtonClickSfx.Attach(coinPack100Button);
        ButtonClickSfx.Attach(coinPack200Button);
        ButtonClickSfx.Attach(hintPack1Button);
        ButtonClickSfx.Attach(hintPack5Button);
        ButtonClickSfx.Attach(hintPack10Button);
        AddListener(shopButton, OpenShop); AddListener(coinButton, OpenCoins); AddListener(settingsButton, OpenSettings);
        AddListener(shopClose, CloseShop); AddListener(settingsClose, CloseSettings);
        AddListener(exitButton, QuitApplication);
        AddListener(coinPack10Button, BuyCoinPack10); AddListener(coinPack100Button, BuyCoinPack100); AddListener(coinPack200Button, BuyCoinPack200);
        AddListener(hintPack1Button, BuyHintPack1); AddListener(hintPack5Button, BuyHintPack5); AddListener(hintPack10Button, BuyHintPack10);
        hintsToggle.SetIsOnWithoutNotify(GameManager.HintButtonEnabled);
        hintsToggle.onValueChanged.AddListener(GameManager.SetHintButtonEnabled);
        hintsToggle.onValueChanged.Invoke(hintsToggle.isOn);
        RefreshCoinBalance();
        bgmToggle.SetIsOnWithoutNotify(audioManager.IsBGMEnabled()); bgmToggle.onValueChanged.AddListener(audioManager.SetBGMEnabled);
        sfxToggle.SetIsOnWithoutNotify(audioManager.IsSFXEnabled()); sfxToggle.onValueChanged.AddListener(audioManager.SetSFXEnabled);
        volumeSlider.SetValueWithoutNotify(audioManager.GetVolume() * 100f); volumeSlider.onValueChanged.AddListener(SetVolumePercent);
        sfxVolumeSlider.SetValueWithoutNotify(audioManager.GetSFXVolume() * 100f); sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolumePercent);
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action) { button.onClick.AddListener(action); }
    private void OpenShop() { shopPanel.SetActive(true); }
    private void OpenCoins() { OpenShop(); coinsToggle.isOn = true; }
    private void OpenSettings() { settingsPanel.SetActive(true); }
    private void CloseShop() { shopPanel.SetActive(false); }
    private void CloseSettings() { settingsPanel.SetActive(false); }
    private static void QuitApplication()
    {
        Application.Quit();
    }
    private void SetVolumePercent(float value) { audioManager.SetVolume(value * 0.01f); }
    private void SetSFXVolumePercent(float value) { audioManager.SetSFXVolume(value * 0.01f); }
    private void RefreshCoinBalance() { coinBalanceText.text = GameManager.Coins.ToString(); }
    private void BuyHints(int amount, int cost) { if (GameManager.TrySpendCoins(cost)) GameManager.AddHints(amount); RefreshCoinBalance(); }
    private void BuyCoinPack10() { BeginCoinPack(1, 10); }
    private void BuyCoinPack100() { BeginCoinPack(2, 100); }
    private void BuyCoinPack200() { BeginCoinPack(3, 200); }
    private void BuyHintPack1() { BuyHints(1, 50); }
    private void BuyHintPack5() { BuyHints(5, 300); }
    private void BuyHintPack10() { BuyHints(10, 500); }

    private void BeginCoinPack(int ads, int reward)
    {
        if (coinPackInProgress || FSAdsManager.Instance == null) return;
        coinPackInProgress = true;
        adsRequired = ads; adsRewarded = 0; pendingCoinReward = reward;
        SetCoinPackButtons(false);
        StartCoroutine(CompleteCoinPack());
    }

    private IEnumerator CompleteCoinPack()
    {
        while (adsRewarded < adsRequired)
        {
            float readyTimeout = 30f;
            FSAdsManager adManager = FSAdsManager.Instance;
            if (adManager == null) break;
            while (!adManager.IsRewardedAdReady && readyTimeout > 0f)
            {
                readyTimeout -= 0.25f;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (!adManager.IsRewardedAdReady) break;

            currentAdRewarded = currentAdClosed = false;
            adManager.ShowRewardedAd(() => currentAdRewarded = true, () => currentAdClosed = true);
            float closeTimeout = 180f;
            while (!currentAdClosed && closeTimeout > 0f)
            {
                closeTimeout -= 0.25f;
                yield return new WaitForSecondsRealtime(0.25f);
            }
            if (!currentAdClosed || !currentAdRewarded) break;
            adsRewarded++;
        }

        if (adsRewarded == adsRequired) GameManager.AddCoins(pendingCoinReward);
        coinPackInProgress = false;
        SetCoinPackButtons(true);
        RefreshCoinBalance();
    }

    private void SetCoinPackButtons(bool interactable)
    {
        coinPack10Button.interactable = interactable;
        coinPack100Button.interactable = interactable;
        coinPack200Button.interactable = interactable;
    }

    public void LoadLevel()
    {
        GameManager.SetHintButtonEnabled(hintsToggle.isOn);

        levelMenu.OpenLevelSelection();
    }

    private void OnDestroy()
    {
        RemoveListener(shopButton, OpenShop); RemoveListener(coinButton, OpenCoins); RemoveListener(settingsButton, OpenSettings);
        RemoveListener(shopClose, CloseShop); RemoveListener(settingsClose, CloseSettings);
        RemoveListener(exitButton, QuitApplication);
        RemoveListener(coinPack10Button, BuyCoinPack10); RemoveListener(coinPack100Button, BuyCoinPack100); RemoveListener(coinPack200Button, BuyCoinPack200);
        RemoveListener(hintPack1Button, BuyHintPack1); RemoveListener(hintPack5Button, BuyHintPack5); RemoveListener(hintPack10Button, BuyHintPack10);
        hintsToggle.onValueChanged.RemoveListener(GameManager.SetHintButtonEnabled);
        bgmToggle.onValueChanged.RemoveListener(audioManager.SetBGMEnabled);
        sfxToggle.onValueChanged.RemoveListener(audioManager.SetSFXEnabled);
        volumeSlider.onValueChanged.RemoveListener(SetVolumePercent);
        sfxVolumeSlider.onValueChanged.RemoveListener(SetSFXVolumePercent);
    }

    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action) { button.onClick.RemoveListener(action); }
}




