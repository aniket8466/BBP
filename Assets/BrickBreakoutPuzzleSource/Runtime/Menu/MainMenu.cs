using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using FuncrateStudio;


public class MainMenu : MonoBehaviour
{
    [SerializeField] private MenuLevelManager levelMenu;
    [SerializeField] private GameObject shopPanel, coinsList, settingsPanel;
    [SerializeField] private Button shopButton, coinButton, settingsButton, shopClose, settingsClose;
    [SerializeField] private Button coinPack10Button, coinPack100Button, coinPack200Button;
    [SerializeField] private Button hintPack1Button, hintPack5Button, hintPack10Button;
    [SerializeField] private Toggle coinsToggle, bgmToggle, sfxToggle;
    [SerializeField] private Slider volumeSlider, sfxVolumeSlider;
    [SerializeField] private TMP_Text coinBalanceText;
    [SerializeField] private AudioManager audioManager;

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
        Button[] sceneButtons = FindObjectsOfType<Button>(true);
        foreach (Button button in sceneButtons) ButtonClickSfx.Attach(button);
        AddListener(shopButton, OpenShop); AddListener(coinButton, OpenCoins); AddListener(settingsButton, OpenSettings);
        AddListener(shopClose, CloseShop); AddListener(settingsClose, CloseSettings);
        AddListener(coinPack10Button, BuyCoinPack10); AddListener(coinPack100Button, BuyCoinPack100); AddListener(coinPack200Button, BuyCoinPack200);
        AddListener(hintPack1Button, BuyHintPack1); AddListener(hintPack5Button, BuyHintPack5); AddListener(hintPack10Button, BuyHintPack10);
        RefreshCoinBalance();
        if (audioManager == null) return;
        if (bgmToggle != null) { bgmToggle.SetIsOnWithoutNotify(audioManager.IsBGMEnabled()); bgmToggle.onValueChanged.AddListener(audioManager.SetBGMEnabled); }
        if (sfxToggle != null) { sfxToggle.SetIsOnWithoutNotify(audioManager.IsSFXEnabled()); sfxToggle.onValueChanged.AddListener(audioManager.SetSFXEnabled); }
        if (volumeSlider != null) { volumeSlider.SetValueWithoutNotify(audioManager.GetVolume() * 100f); volumeSlider.onValueChanged.AddListener(SetVolumePercent); }
        if (sfxVolumeSlider != null) { sfxVolumeSlider.SetValueWithoutNotify(audioManager.GetSFXVolume() * 100f); sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolumePercent); }
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action) { if (button != null) button.onClick.AddListener(action); }
    private void OpenShop() { if (shopPanel != null) shopPanel.SetActive(true); }
    private void OpenCoins() { OpenShop(); if (coinsToggle != null) coinsToggle.isOn = true; else if (coinsList != null) coinsList.SetActive(true); }
    private void OpenSettings() { if (settingsPanel != null) settingsPanel.SetActive(true); }
    private void CloseShop() { if (shopPanel != null) shopPanel.SetActive(false); }
    private void CloseSettings() { if (settingsPanel != null) settingsPanel.SetActive(false); }
    private void SetVolumePercent(float value) { if (audioManager != null) audioManager.SetVolume(value * 0.01f); }
    private void SetSFXVolumePercent(float value) { if (audioManager != null) audioManager.SetSFXVolume(value * 0.01f); }
    private void RefreshCoinBalance() { if (coinBalanceText != null) coinBalanceText.text = GameManager.Coins.ToString(); }
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
        if (coinPack10Button != null) coinPack10Button.interactable = interactable;
        if (coinPack100Button != null) coinPack100Button.interactable = interactable;
        if (coinPack200Button != null) coinPack200Button.interactable = interactable;
    }

    public void LoadLevel()
    {
        if (levelMenu != null) levelMenu.OpenLevelSelection();
        else SceneManager.LoadScene("Level");
    }

    private void OnDestroy()
    {
        RemoveListener(shopButton, OpenShop); RemoveListener(coinButton, OpenCoins); RemoveListener(settingsButton, OpenSettings);
        RemoveListener(shopClose, CloseShop); RemoveListener(settingsClose, CloseSettings);
        RemoveListener(coinPack10Button, BuyCoinPack10); RemoveListener(coinPack100Button, BuyCoinPack100); RemoveListener(coinPack200Button, BuyCoinPack200);
        RemoveListener(hintPack1Button, BuyHintPack1); RemoveListener(hintPack5Button, BuyHintPack5); RemoveListener(hintPack10Button, BuyHintPack10);
        if (audioManager == null) return;
        if (bgmToggle != null) bgmToggle.onValueChanged.RemoveListener(audioManager.SetBGMEnabled);
        if (sfxToggle != null) sfxToggle.onValueChanged.RemoveListener(audioManager.SetSFXEnabled);
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(SetVolumePercent);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveListener(SetSFXVolumePercent);
    }

    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action) { if (button != null) button.onClick.RemoveListener(action); }
}




