using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

// Plays pop and disappear animations for UI elements grouped by a name.
public class UIAnimations : MonoBehaviour
{
    public static UIAnimations Instance { get; private set; }
    private readonly Dictionary<RectTransform, Vector3> originalScales = new Dictionary<RectTransform, Vector3>();
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


    void Awake()
    {

        Instance = this;
    }

    /// <summary>Show all elements in the section with a pop animation.</summary>
    public void ShowSection(string sectionName)
    {
        UIAnimationSection section = FindSection(sectionName);
        if (section == null || section.appearUIElements == null || section.appearUIElements.Count == 0) return;

        StartCoroutine(ShowSectionRoutine(section));
    }

    /// <summary>Hide all elements in the section with a disappear animation.</summary>
    public void HideSection(string sectionName)
    {
        HideSection(sectionName, null);
    }

    public void HideSection(string sectionName, UnityAction onComplete)
    {
        UIAnimationSection section = FindSection(sectionName);
        if (section == null) return;
        List<RectTransform> elements = section.appearUIElements;
        if (elements == null || elements.Count == 0) elements = section.disappearUIElements;
        StartCoroutine(HideElementsSequentially(elements, onComplete));
    }

    /// <summary>Play the section transition: disappear its outgoing elements, then show its incoming elements.</summary>
    public void ToggleSection(string sectionName)
    {
        UIAnimationSection section = FindSection(sectionName);
        if (section == null) return;
        StartCoroutine(PlaySectionTransition(section));
    }

    /// <summary>Toggle a named section, animating its elements in or out.</summary>
    public void PlayAnimation(string sectionName, UnityAction onComplete = null)
    {
        UIAnimationSection section = FindSection(sectionName);
        if (section == null) return;
        StartCoroutine(PlaySectionTransition(section, onComplete));
    }

    private IEnumerator PlaySectionTransition(UIAnimationSection section, UnityAction onComplete = null)
    {
        yield return HideElementsSequentially(section.disappearUIElements);
        yield return ShowElementsSequentially(section.appearUIElements);
        section.onAppearCompleted?.Invoke();
        onComplete?.Invoke();
    }

    private IEnumerator ShowSectionRoutine(UIAnimationSection section)
    {
        yield return ShowElementsSequentially(section.appearUIElements);
        section.onAppearCompleted?.Invoke();
    }

    private IEnumerator HideElementsSequentially(List<RectTransform> elements, UnityAction onComplete = null)
    {
        if (elements != null)
        {
            foreach (RectTransform element in elements)
            {
                if (element == null || !element.gameObject.activeSelf) continue;
                element.DOKill();
                element.DOScale(Vector3.zero, disappearAnimationDuration).SetEase(Ease.InBack, 0.75f).SetUpdate(true).OnComplete(() => element.gameObject.SetActive(false));
                if (disappearAnimationDuration > 0f) yield return new WaitForSecondsRealtime(disappearAnimationDuration);
                else element.gameObject.SetActive(false);
            }
        }
        onComplete?.Invoke();
    }

    private IEnumerator ShowElementsSequentially(List<RectTransform> elements)
    {
        if (elements == null || elements.Count == 0) yield break;

        // Prepare the whole group first so every element starts from the same scale.
        foreach (RectTransform element in elements)
        {
            if (element == null) continue;
            if (!originalScales.ContainsKey(element))
                originalScales.Add(element, element.localScale);
            element.DOKill();
            element.gameObject.SetActive(true);
            element.localScale = originalScales[element] * appearStartScale;
        }

        // Then animate each element in order to create the wave.
        foreach (RectTransform element in elements)
        {
            if (element == null) continue;
            element.DOScale(originalScales[element], popAnimationDuration).SetEase(Ease.OutBack, 0.75f).SetUpdate(true);
            if (popAnimationDuration > 0f) yield return new WaitForSecondsRealtime(popAnimationDuration);
        }
    }

    private UIAnimationSection FindSection(string sectionName)
    {
        if (sections == null || sections.Count == 0 || string.IsNullOrEmpty(sectionName)) return null;
        return sections.Find(section => section != null && section.sectionName == sectionName);
    }
}
