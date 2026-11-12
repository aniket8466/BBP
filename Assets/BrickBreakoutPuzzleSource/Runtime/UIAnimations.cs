
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

public class UIAnimations : MonoBehaviour
{
    public static UIAnimations Instance { get; private set; }

    private readonly Dictionary<RectTransform, Vector3> originalScales = new Dictionary<RectTransform, Vector3>();
    private Coroutine currentRoutine;

    [System.Serializable]
    public class UIAnimationSection
    {
        [Tooltip("Name used to show, hide, or toggle this section.")]
        public string sectionName;

        [Tooltip("Elements that appear when this section is played.")]
        public List<RectTransform> appearUIElements = new List<RectTransform>();

        [Tooltip("Elements that disappear when this section is played.")]
        public List<RectTransform> disappearUIElements = new List<RectTransform>();

        [Tooltip("Invoked after all appear elements finish animating.")]
        public UnityEvent onAppearCompleted = new UnityEvent();
    }

    [Header("Named Sections")]
    [SerializeField] private List<UIAnimationSection> sections = new List<UIAnimationSection>();
    [SerializeField] private float appearStartScale = 0f;
    [SerializeField] private float popAnimationDuration = 0.5f;
    [SerializeField] private float disappearAnimationDuration = 0.5f;

    private void Awake() // Initialize instance
    {
        Instance = this;
    }

    public void ShowSection(string sectionName) // Show section with transition
    {
        PlayAnimation(sectionName);
    }

    public void HideSection(string sectionName) // Hide section
    {
        HideSection(sectionName, null);
    }

    public void HideSection(string sectionName, UnityAction onComplete) // Hide section with callback
    {
        UIAnimationSection section = FindSection(sectionName);
        if (section == null) return;

        StartAnimation(HideElementsSequentially(
            section.appearUIElements != null && section.appearUIElements.Count > 0
                ? section.appearUIElements
                : section.disappearUIElements, onComplete));
    }

    public void ToggleSection(string sectionName) // Toggle section with transition
    {
        PlayAnimation(sectionName);
    }

    public void PlayAnimation(string sectionName, UnityAction onComplete = null) // Play section transition
    {
        UIAnimationSection section = FindSection(sectionName);
        if (section == null) return;

        StartAnimation(PlaySectionTransition(section, onComplete));
    }

    private void StartAnimation(IEnumerator routine) // Start animation
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(routine);
    }

    private IEnumerator PlaySectionTransition(UIAnimationSection section, UnityAction onComplete = null) // Play transition
    {
        yield return HideElementsSequentially(section.disappearUIElements);
        yield return ShowElementsSequentially(section.appearUIElements);

        section.onAppearCompleted?.Invoke();
        onComplete?.Invoke();

        currentRoutine = null;
    }

    private IEnumerator HideElementsSequentially(List<RectTransform> elements, UnityAction onComplete = null) // Hide elements
    {
        if (elements != null)
        {
            foreach (RectTransform element in elements)
            {
                if (element == null || !element.gameObject.activeSelf) continue;

                element.DOKill();

                if (disappearAnimationDuration <= 0f)
                {
                    element.localScale = Vector3.zero;
                    element.gameObject.SetActive(false);
                    continue;
                }

                bool completed = false;

                element.DOScale(Vector3.zero, disappearAnimationDuration)
                    .SetEase(Ease.InBack, 0.75f)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        if (element != null)
                            element.gameObject.SetActive(false);

                        completed = true;
                    });

                yield return new WaitUntil(() => completed);
            }
        }

        onComplete?.Invoke();
    }

    private IEnumerator ShowElementsSequentially(List<RectTransform> elements) // Show elements
    {
        if (elements == null || elements.Count == 0) yield break;

        foreach (RectTransform element in elements)
        {
            if (element == null) continue;

            if (!originalScales.ContainsKey(element))
                originalScales.Add(element, element.localScale);

            element.DOKill();
            element.gameObject.SetActive(true);
            element.localScale = originalScales[element] * appearStartScale;
        }

        foreach (RectTransform element in elements)
        {
            if (element == null) continue;

            if (popAnimationDuration <= 0f)
            {
                element.localScale = originalScales[element];
                continue;
            }

            bool completed = false;

            element.DOScale(originalScales[element], popAnimationDuration)
                .SetEase(Ease.OutBack, 0.75f)
                .SetUpdate(true)
                .OnComplete(() => completed = true);

            yield return new WaitUntil(() => completed);
        }
    }

    private UIAnimationSection FindSection(string sectionName) // Find section
    {
        if (sections == null || string.IsNullOrEmpty(sectionName)) return null;

        return sections.Find(section => section != null && section.sectionName == sectionName);
    }

    private void OnDestroy() // Cleanup
    {
        if (Instance == this)
            Instance = null;

        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        foreach (RectTransform element in originalScales.Keys)
        {
            if (element != null)
                element.DOKill();
        }
    }
}
