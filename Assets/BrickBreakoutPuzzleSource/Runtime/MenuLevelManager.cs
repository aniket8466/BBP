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

    private void Start()
    {
        if (levelButtonsRoot == null || levelScrollRect == null || levelButtonPrefab == null)
        {
            Debug.LogError("MenuLevelManager needs its Scroll View, Content transform, and LevelBtn prefab assigned.", this);
            enabled = false;
            return;
        }

        CreateLevelButtons();
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
        StartCoroutine(ScrollToCurrentLevelAfterLayout());
    }

    private void CreateLevelButtons()
    {
        for (int i = levelButtonsRoot.childCount - 1; i >= 0; i--)
            Destroy(levelButtonsRoot.GetChild(i).gameObject);

        int highestUnlocked = GameManager.HighestUnlockedLevel;
        for (int level = 1; level <= Mathf.Clamp(levelCount, 1, GameManager.MaximumLevel); level++)
        {
            bool unlocked = level <= highestUnlocked;
            GameObject buttonObject = Instantiate(levelButtonPrefab, levelButtonsRoot, false);
            buttonObject.name = $"Level {level:0000}";
            Button button = buttonObject.GetComponent<Button>();
            ButtonClickSfx.Attach(button);
            button.interactable = unlocked;
            int selectedLevel = level;
            if (unlocked) button.onClick.AddListener(() => ChooseLevel(selectedLevel));
            TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = $"LEVEL {level:0000}";
            UpdateButtonStars(buttonObject.transform, GameManager.GetLevelStars(level));
        }

        Canvas.ForceUpdateCanvases();
        RectTransform contentRect = levelButtonsRoot as RectTransform;
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
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
        GameManager.SelectLevel(level);
        if (Application.CanStreamedLevelBeLoaded(LevelSceneName))
            SceneManager.LoadScene(LevelSceneName);
        else
            Debug.LogWarning($"Level scene '{LevelSceneName}' is not available in the build settings.", this);
    }

    public void OpenLevelSelection()
    {
        if (levelScrollRect == null) return;
        Transform panel = levelScrollRect.transform.parent;
        if (panel != null) panel.gameObject.SetActive(true);
        StartCoroutine(ScrollToCurrentLevelAfterLayout());
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
        int totalRows = Mathf.CeilToInt((float)levelCount / columns);
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
