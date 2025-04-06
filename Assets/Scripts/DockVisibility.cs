using UnityEngine;
using System.Runtime.InteropServices;
using System.Text;

public class DockVisibility : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    public bool forceShowInEditor = false;

    [DllImport("user32.dll")]
    static extern System.IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern int GetWindowText(System.IntPtr hWnd, StringBuilder text, int count);

    void Start()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    void Update()
    {
#if UNITY_EDITOR
        if (forceShowInEditor)
        {
            canvasGroup.alpha = 1f;
            return;
        }
#endif

        string activeWindow = GetActiveWindowTitle();
        bool isDesktop = string.IsNullOrEmpty(activeWindow) || activeWindow == "Program Manager";

        canvasGroup.alpha = isDesktop ? 1f : 0f;
    }

    string GetActiveWindowTitle()
    {
        const int nChars = 256;
        StringBuilder Buff = new StringBuilder(nChars);
        System.IntPtr handle = GetForegroundWindow();

        if (GetWindowText(handle, Buff, nChars) > 0)
        {
            return Buff.ToString();
        }
        return null;
    }
}
