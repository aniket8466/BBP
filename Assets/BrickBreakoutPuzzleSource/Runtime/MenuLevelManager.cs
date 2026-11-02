using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MenuLevelManager : MonoBehaviour
{
    [SerializeField] private Transform levelButtonsRoot;
    [SerializeField] private ScrollRect levelScrollRect;
    [SerializeField] private Button closeButton;
    [SerializeField] private GameObject levelButtonPrefab;
    [SerializeField] private GridLayoutGroup levelGrid;
    [SerializeField, Min(1)] private int levelCount = GameManager.MaximumLevel;

    private const string LevelSceneName = "Level";
    private const int LevelsPerBatch = 100;
    // Keep each frame's UI-instantiation work small so opening the level picker
    // does not stall the Play button or the first visible frame of the panel.
    private const int ButtonBatchSize = 20;
    private bool buttonsCreated;
    private bool isCreatingButtons;
    private int createdLevelCount;

    private void Start()
    {
        closeButton.onClick.AddListener(CloseLevelSelection);
        ButtonClickSfx.Attach(closeButton);
    }

    private IEnumerator CreateLevelButtonsOverFrames()
    {
        isCreatingButtons = true;

        // Let Unity render the opened panel before creating the level list.
        yield return null;

        for (int i = levelButtonsRoot.childCount - 1; i >= 0; i--)
            Destroy(levelButtonsRoot.GetChild(i).gameObject);

        int highestUnlocked = GameManager.HighestUnlockedLevel;
        int maxLevelCount = Mathf.Clamp(levelCount, 1, GameManager.MaximumLevel);
        int batchLimit = Mathf.CeilToInt(highestUnlocked / (float)LevelsPerBatch) * LevelsPerBatch;
        int totalLevels = Mathf.Min(maxLevelCount, batchLimit);
        for (int level = 1; level <= totalLevels; level++)
        {
            bool unlocked = level <= highestUnlocked;
            GameObject buttonObject = Instantiate(levelButtonPrefab, levelButtonsRoot, false);
            buttonObject.name = $"LEVEL {level:00}";
            Button button = buttonObject.GetComponent<Button>();
            ButtonClickSfx.Attach(button);
            button.interactable = unlocked;
            int selectedLevel = level;
            if (unlocked) button.onClick.AddListener(() => ChooseLevel(selectedLevel));
            TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);
            label.text = $"LEVEL {level:00}";
            UpdateButtonStars(buttonObject.transform, GameManager.GetLevelStars(level));

            if (level % ButtonBatchSize == 0)
                yield return null;
        }

        Canvas.ForceUpdateCanvases();
        RectTransform contentRect = (RectTransform)levelButtonsRoot;
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        buttonsCreated = true;
        createdLevelCount = totalLevels;
        isCreatingButtons = false;
        StartCoroutine(ScrollToCurrentLevelAfterLayout());
    }

    private static void UpdateButtonStars(Transform buttonRoot, int earnedStars)
    {
        Transform starHolder = FindChild(buttonRoot, "StarHolder");

        for (int i = 0; i < starHolder.childCount; i++)
        {
            Transform starSlot = starHolder.GetChild(i);
            if (starSlot.childCount == 0) continue;

            starSlot.gameObject.SetActive(true);
            starSlot.GetChild(0).gameObject.SetActive(i < earnedStars);
        }
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root.name == childName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }

    private void ChooseLevel(int level)
    {
        if (!GameManager.IsLevelUnlocked(level)) return;
        if (!Application.CanStreamedLevelBeLoaded(LevelSceneName))
        {
            Debug.LogWarning($"Level scene '{LevelSceneName}' is not available in the build settings.", this);
            return;
        }

        GameManager.SelectLevel(level);
        LevelGenerator.SetLevelToLoad(level);
        SceneManager.LoadScene(LevelSceneName);
    }

    public void OpenLevelSelection()
    {
        Transform panel = levelScrollRect.transform.parent;
        panel.gameObject.SetActive(true);

        if (buttonsCreated)
            StartCoroutine(ScrollToCurrentLevelAfterLayout());
        else if (!isCreatingButtons)
            StartCoroutine(CreateLevelButtonsOverFrames());
    }

    private void CloseLevelSelection()
    {
        Transform panel = levelScrollRect.transform.parent;
        panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        closeButton.onClick.RemoveListener(CloseLevelSelection);
    }

    private IEnumerator ScrollToCurrentLevelAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        RectTransform contentRect = (RectTransform)levelButtonsRoot;
        RectTransform viewportRect = levelScrollRect.viewport;
        int columns = levelGrid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
            ? Mathf.Max(1, levelGrid.constraintCount)
            : Mathf.Max(1, Mathf.FloorToInt((contentRect.rect.width + levelGrid.spacing.x) /
                (levelGrid.cellSize.x + levelGrid.spacing.x)));
        int currentRow = (GameManager.SelectedLevel - 1) / columns;
        int totalLevels = createdLevelCount;
        float contentHeight = contentRect.rect.height;
        float maxScroll = Mathf.Max(0f, contentHeight - viewportRect.rect.height);
        float rowHeight = levelGrid.cellSize.y + levelGrid.spacing.y;
        float targetOffset = currentRow * rowHeight - (viewportRect.rect.height - rowHeight) * 0.5f;
        levelScrollRect.verticalNormalizedPosition = maxScroll <= 0f
            ? 1f
            : 1f - Mathf.Clamp(targetOffset, 0f, maxScroll) / maxScroll;
    }
}
