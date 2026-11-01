using UnityEngine;
using UnityEngine.UI;

/// <summary>Plays the standard click sound once when a UI button is pressed.</summary>
public sealed class ButtonClickSfx : MonoBehaviour
{
    private Button button;

    public static void Attach(Button target)
    {
        if (target != null && target.GetComponent<ButtonClickSfx>() == null)
            target.gameObject.AddComponent<ButtonClickSfx>();
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(PlayClick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(PlayClick);
    }

    private static void PlayClick()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("click");
    }
}
