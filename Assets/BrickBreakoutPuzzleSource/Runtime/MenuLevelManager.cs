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
        if (levelButtonsRoot == null || levelScrollRect == null || levelButtonPrefab == null)
        {
            Debug.LogError("MenuLevelManager needs its Scroll View, Content transform, and LevelBtn prefab assigned.", this);
            enabled = false;
            return;
        }

        if (closeButton == null && levelScrollRect.transform.parent != null)
        {
            Button[] panelButtons = levelScrollRect.transform.parent.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < panelButtons.Length; i++)
            {
                if (panelButtons[i].name != "Close") continue;
                closeButton = panelButtons[i];
                break;
            }
        }
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseLevelSelection);
            ButtonClickSfx.Attach(closeButton);
        }
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
            if (label != null) label.text = $"LEVEL {level:00}";
            UpdateButtonStars(buttonObject.transform, GameManager.GetLevelStars(level));

            if (level % ButtonBatchSize == 0)
                yield return null;
        }

        Canvas.ForceUpdateCanvases();
        RectTransform contentRect = levelButtonsRoot as RectTransform;
        if (contentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        buttonsCreated = true;
        createdLevelCount = totalLevels;
        isCreatingButtons = false;
        StartCoroutine(ScrollToCurrentLevelAfterLayout());
    }

    private static void UpdateButtonStars(Transform buttonRoot, int earnedStars)
    {
        Transform starHolder = FindChild(buttonRoot, "StarHolder");
        if (starHolder == null) return;

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
        if (root == null) return null;
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
        if (levelScrollRect == null) return;
        Transform panel = levelScrollRect.transform.parent;
        if (panel != null) panel.gameObject.SetActive(true);

        if (buttonsCreated)
            StartCoroutine(ScrollToCurrentLevelAfterLayout());
        else if (!isCreatingButtons)
            StartCoroutine(CreateLevelButtonsOverFrames());
    }

    private void CloseLevelSelection()
    {
        Transform panel = levelScrollRect != null ? levelScrollRect.transform.parent : null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(CloseLevelSelection);
    }

    private IEnumerator ScrollToCurrentLevelAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        GridLayoutGroup grid = levelButtonsRoot.GetComponent<GridLayoutGroup>();
        RectTransform contentRect = levelButtonsRoot as RectTransform;
        RectTransform viewportRect = levelScrollRect.viewport != null
            ? levelScrollRect.viewport
            : levelScrollRect.GetComponent<RectTransform>();
        if (contentRect == null || viewportRect == null) yield break;

        int columns = grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount
            ? Mathf.Max(1, grid.constraintCount)
            : Mathf.Max(1, Mathf.FloorToInt((contentRect.rect.width + (grid != null ? grid.spacing.x : 0f)) /
                ((grid != null ? grid.cellSize.x + grid.spacing.x : contentRect.rect.width))));
        int currentRow = (GameManager.SelectedLevel - 1) / columns;
        int totalLevels = createdLevelCount;
        int totalRows = Mathf.CeilToInt((float)totalLevels / columns);
        float contentHeight = contentRect.rect.height;
        float maxScroll = Mathf.Max(0f, contentHeight - viewportRect.rect.height);
        if (grid != null)
        {
            float rowHeight = grid.cellSize.y + grid.spacing.y;
            float targetOffset = currentRow * rowHeight - (viewportRect.rect.height - rowHeight) * 0.5f;
            levelScrollRect.verticalNormalizedPosition = maxScroll <= 0f
                ? 1f
                : 1f - Mathf.Clamp(targetOffset, 0f, maxScroll) / maxScroll;
        }
        else if (totalRows > 1)
        {
            levelScrollRect.verticalNormalizedPosition = 1f - (float)currentRow / (totalRows - 1);
        }
    }
}
