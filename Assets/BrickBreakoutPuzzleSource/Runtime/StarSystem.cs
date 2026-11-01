using System.Collections;
using DG.Tweening;
using UnityEngine;

public sealed class StarSystem : MonoBehaviour
{
    [SerializeField] private GameObject[] stars;
    private Coroutine presentation;

    public void SetStars(Transform starsRoot)
    {
        if ((stars == null || stars.Length == 0) && starsRoot != null)
        {
            stars = new GameObject[starsRoot.childCount];
            for (int i = 0; i < stars.Length; i++)
            {
                Transform starHolder = starsRoot.GetChild(i);
                stars[i] = starHolder.childCount > 0
                    ? starHolder.GetChild(0).gameObject
                    : starHolder.gameObject;
            }
        }
    }

    public void PresentStars(int starCount)
    {
        if (stars == null || stars.Length == 0) return;
        if (presentation != null) StopCoroutine(presentation);
        presentation = StartCoroutine(PresentStarsRoutine(Mathf.Clamp(starCount, 0, 3)));
    }

    private IEnumerator PresentStarsRoutine(int starCount)
    {
        int count = Mathf.Min(3, stars.Length);
        for (int i = 0; i < count; i++)
        {
            if (stars[i] == null) continue;
            Transform star = stars[i].transform;
            star.DOKill();
            star.gameObject.SetActive(false);
            star.localScale = Vector3.one;
        }

        yield return new WaitForSecondsRealtime(0.25f);
        for (int i = 0; i < Mathf.Min(starCount, count); i++)
        {
            if (stars[i] == null) continue;
            Transform star = stars[i].transform;
            star.gameObject.SetActive(true);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("LevelStar_" + (i + 1));
            star.localScale = Vector3.zero;
            star.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
            yield return new WaitForSecondsRealtime(0.2f);
        }
        presentation = null;
    }
}
