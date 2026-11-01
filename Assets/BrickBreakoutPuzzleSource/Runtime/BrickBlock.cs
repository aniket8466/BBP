using System;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BrickBlock : MonoBehaviour
{
    public Vector2Int Cell { get; private set; }
    public Vector2Int Direction { get; private set; }
    private Vector3 homePosition;
    [SerializeField, Min(0.1f)] private float slideOutDuration = 1.15f;
    [SerializeField] private TrailRenderer exitTrail;
    private bool movingOut;
    private bool blockedFeedbackPlaying;
    internal bool IsMovingOut => movingOut;
    internal void Initialize(Vector2Int cell, Vector2Int direction, Vector3 position, Action<BrickBlock> onTap)
    {
        Cell = cell;
        Direction = direction;
        homePosition = position;
    }

    internal void PlaySpawnAnimation(float delay)
    {
        Vector3 targetScale = transform.localScale;
        transform.localScale = Vector3.zero;
        transform.DOScale(targetScale, 0.32f)
            .SetDelay(delay)
            .SetEase(Ease.OutBack);
    }

    internal void FlyOut(Vector3 direction, float distance, Action onComplete = null)
    {
        if (movingOut) return;
        movingOut = true;
        transform.DOKill();

        Collider ownCollider = GetComponent<Collider>();
        if (ownCollider != null) ownCollider.enabled = false;

        Vector3 originalScale = transform.localScale;
        const float pressDuration = 0.08f;
        const float releaseDuration = 0.10f;
        float slideDuration = Mathf.Max(0.1f, slideOutDuration);
        TrailRenderer trail = exitTrail != null ? exitTrail : GetComponentInChildren<TrailRenderer>(true);
        if (trail != null)
        {
            trail.Clear();
            trail.emitting = false;
            Renderer blockRenderer = GetComponent<Renderer>();
            if (blockRenderer != null && blockRenderer.sharedMaterial != null)
            {
                Color color = blockRenderer.sharedMaterial.color;
                trail.startColor = new Color(color.r, color.g, color.b, 0.8f);
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }

        Sequence flyTween = DOTween.Sequence();
        flyTween.Append(transform.DOScale(originalScale * 0.88f, pressDuration).SetEase(Ease.OutQuad));
        flyTween.Append(transform.DOScale(originalScale, releaseDuration).SetEase(Ease.OutBack));
        flyTween.AppendCallback(() =>
        {
            if (trail != null) trail.emitting = true;
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX("Slide");
        });
        flyTween.Append(transform.DOMove(transform.position + direction.normalized * distance, slideDuration).SetEase(Ease.OutCubic));
        flyTween.Join(transform.DOScale(Vector3.zero, slideDuration).SetEase(Ease.InBack));
        flyTween.OnComplete(() =>
        {
            if (trail != null)
            {
                trail.emitting = false;
                trail.transform.SetParent(null, true);
                Destroy(trail.gameObject, trail.time);
            }
            gameObject.SetActive(false);
            Destroy(gameObject);
            onComplete?.Invoke();
        });
    }

    internal void PlayBlockedFeedback()
    {
        if (movingOut || blockedFeedbackPlaying) return;
        blockedFeedbackPlaying = true;
        Vector3 shakeDirection = new Vector3(Direction.y, -Direction.x, 0f) * 0.045f;
        transform.DOShakePosition(0.2f, shakeDirection, 18, 0f, false, true)
            .OnComplete(() =>
            {
                transform.position = homePosition;
                blockedFeedbackPlaying = false;
            });
    }

}
