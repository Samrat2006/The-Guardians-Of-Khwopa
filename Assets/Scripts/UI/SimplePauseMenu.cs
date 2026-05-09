using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Minimal, reliable pause toggler:
/// - ESC toggles a pause menu GameObject
/// - sets Time.timeScale (freeze/unfreeze)
/// - unlocks cursor while paused (optional)
/// </summary>
public class SimplePauseMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pauseMenuRoot;
    [SerializeField] private bool autoFixPauseMenuLayout = true;
    [SerializeField] private int ensureCanvasSortingOrder = 3000;
    [SerializeField] private bool autoCreatePauseMenuIfMissing = true;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("Cursor")]
    [SerializeField] private bool unlockCursorWhilePaused = true;

    [Header("Emergency overlay (debug)")]
    [Tooltip("Draws a simple PAUSED overlay even if UI is broken. If you don't see this, the script is not running.")]
    [SerializeField] private bool emergencyOnGuiOverlay = true;

    [Header("Keyboard menu navigation (optional)")]
    [Tooltip("Assign the 3 TMP texts in order: Resume, Restart, Main Menu.")]
    [SerializeField] private TextMeshProUGUI[] menuOptions;
    [SerializeField] private Color optionColor = Color.black;
    [SerializeField] private Color selectedColor = new Color(1f, 0.92f, 0.3f, 1f);
    [SerializeField] private float selectedScale = 1.15f;

    private bool _paused;
    private float _prevTimeScale = 1f;
    private int _selectedIndex;
    private int _ignoreInputUntilFrame;

    private void Awake()
    {
        if (pauseMenuRoot == null && autoCreatePauseMenuIfMissing)
            pauseMenuRoot = CreateRuntimePauseMenu();

        // Start unpaused.
        SetPaused(false);
    }

    private void Update()
    {
        if (WasTogglePressed())
        {
            SetPaused(!_paused);
            _ignoreInputUntilFrame = Time.frameCount + 1;
            return;
        }

        if (!_paused) return;
        if (Time.frameCount <= _ignoreInputUntilFrame) return;

        HandleMenuNavigation();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;

        if (pauseMenuRoot != null)
        {
            if (paused && autoFixPauseMenuLayout)
                EnsureMenuIsVisible();
            pauseMenuRoot.SetActive(paused);
        }

        if (paused)
        {
            _prevTimeScale = Time.timeScale <= 0f ? 1f : Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = _prevTimeScale <= 0f ? 1f : _prevTimeScale;
        }

        if (unlockCursorWhilePaused)
        {
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }

        if (paused)
        {
            _selectedIndex = 0;
            UpdateMenuSelectionVisuals();
        }
    }

    // ---- UI Button hooks (wire these in the Inspector) ----
    public void Resume() => SetPaused(false);

    public void RestartScene()
    {
        Time.timeScale = 1f;
        _paused = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMainMenu(string mainMenuSceneName = "MainMenu")
    {
        // Optional confirm via DialogueManager if present.
        DialogueManager dm = FindFirstObjectByType<DialogueManager>();
        if (dm != null)
        {
            dm.ShowYesNoChoice("Would you really like to quit to Main Menu?", yes =>
            {
                if (!yes) return;
                Time.timeScale = 1f;
                _paused = false;
                SceneManager.LoadScene(mainMenuSceneName);
            });
            return;
        }

        Time.timeScale = 1f;
        _paused = false;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void HandleMenuNavigation()
    {
        if (menuOptions == null || menuOptions.Length == 0) return;

        if (WasUpPressed())
        {
            _selectedIndex--;
            if (_selectedIndex < 0) _selectedIndex = menuOptions.Length - 1;
            UpdateMenuSelectionVisuals();
        }
        else if (WasDownPressed())
        {
            _selectedIndex++;
            if (_selectedIndex >= menuOptions.Length) _selectedIndex = 0;
            UpdateMenuSelectionVisuals();
        }
        else if (WasSubmitPressed())
        {
            ExecuteSelectedOption();
        }
    }

    private void ExecuteSelectedOption()
    {
        // Convention: 0=Resume, 1=Restart, 2=Main Menu
        if (_selectedIndex == 0) Resume();
        else if (_selectedIndex == 1) RestartScene();
        else if (_selectedIndex == 2) QuitToMainMenu();
        else Resume();
    }

    private void UpdateMenuSelectionVisuals()
    {
        if (menuOptions == null) return;
        for (int i = 0; i < menuOptions.Length; i++)
        {
            TextMeshProUGUI t = menuOptions[i];
            if (t == null) continue;
            bool sel = i == _selectedIndex;
            t.color = sel ? selectedColor : optionColor;
            t.transform.localScale = sel ? Vector3.one * selectedScale : Vector3.one;
        }
    }

    private static bool WasUpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        return Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
#endif
    }

    private static bool WasDownPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        return Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
#endif
    }

    private static bool WasSubmitPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        return Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    private bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.escapeKey.wasPressedThisFrame;
        return false;
#else
        return Input.GetKeyDown(toggleKey);
#endif
    }

    private void EnsureMenuIsVisible()
    {
        if (pauseMenuRoot == null)
        {
            if (autoCreatePauseMenuIfMissing)
                pauseMenuRoot = CreateRuntimePauseMenu();
            if (pauseMenuRoot == null) return;
        }

        // Ensure we are under an enabled overlay Canvas and that it renders on top.
        Canvas canvas = pauseMenuRoot.GetComponentInParent<Canvas>(true);
        if (canvas == null)
        {
            // If it's not under a Canvas, put it under a new overlay canvas so it can render.
            Canvas wrapper = CreateOverlayCanvas("RuntimePauseCanvas", ensureCanvasSortingOrder);
            pauseMenuRoot.transform.SetParent(wrapper.transform, false);
            canvas = wrapper;
        }

        if (canvas != null)
        {
            canvas.enabled = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (canvas.sortingOrder < ensureCanvasSortingOrder)
                canvas.sortingOrder = ensureCanvasSortingOrder;
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        // Force panel to fullscreen stretch (fixes Top/Right offsets pushing it offscreen).
        RectTransform rt = pauseMenuRoot.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition3D = Vector3.zero;
            rt.localScale = Vector3.one;
        }

        // Ensure there's a visible background even if the panel image is missing/transparent.
        Image img = pauseMenuRoot.GetComponent<Image>();
        if (img == null)
            img = pauseMenuRoot.AddComponent<Image>();
        if (img.sprite == null)
            img.sprite = UiSpriteUtility.WhiteSprite();
        if (img.color.a < 0.05f)
            img.color = new Color(0f, 0f, 0f, 0.6f);

        // If there's a CanvasGroup forcing invisibility, override it.
        CanvasGroup cg = pauseMenuRoot.GetComponent<CanvasGroup>();
        if (cg != null && cg.alpha <= 0.001f)
            cg.alpha = 1f;
    }

    private static Canvas CreateOverlayCanvas(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private GameObject CreateRuntimePauseMenu()
    {
        Canvas canvas = CreateOverlayCanvas("RuntimePauseCanvas", ensureCanvasSortingOrder);

        GameObject panel = new GameObject("PauseMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = panel.GetComponent<Image>();
        img.sprite = UiSpriteUtility.WhiteSprite();
        img.color = new Color(0f, 0f, 0f, 0.6f);

        // Minimal label so you can SEE the menu even if other UI is broken.
        GameObject labelGo = new GameObject("PausedLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(panel.transform, false);
        Text t = labelGo.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.fontSize = 48;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = "PAUSED\n\nPress ESC to resume";
        RectTransform lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.5f, 0.5f);
        lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(900f, 300f);

        panel.SetActive(false);
        return panel;
    }

    private void OnGUI()
    {
        if (!emergencyOnGuiOverlay) return;
        if (!_paused) return;

        const int w = 420;
        const int h = 120;
        int x = (Screen.width - w) / 2;
        int y = (Screen.height - h) / 2;
        GUI.Box(new Rect(x, y, w, h), "PAUSED\nPress ESC to resume");
    }
}

