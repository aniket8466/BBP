
using UnityEngine;
using UnityEngine.UI;

public class HelpAndSupport : MonoBehaviour
{
    [SerializeField] private Button supportEmailButton;

    void Awake()
    {
        supportEmailButton.onClick.AddListener(OpenSupportEmail);
    }
    public void OpenSupportEmail() // Opens the support email app
    {
        Application.OpenURL("mailto:funcratestudiohelp@gmail.com?subject=Breakout%20Puzzle%20Support");
    }
}
