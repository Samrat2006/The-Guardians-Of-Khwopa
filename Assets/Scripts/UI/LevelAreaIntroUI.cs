using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Per-zone styling for <see cref="LevelAreaIntroUI"/>. Set on each <see cref="LevelAreaZone"/> in the Inspector.
/// </summary>
[System.Serializable]
public class LevelAreaIntroPresentation
{
    [Header("Title")]
    public Color titleColor = Color.white;
    public Vector2 titleAnchoredPosition = new Vector2(0f, 40f);
    public Vector2 titleSizeDelta = new Vector2(1400f, 120f);
    public float titleFontSize = 72f;
    public FontStyles titleFontStyle = FontStyles.Bold;
    public TextAlignmentOptions titleAlignment = TextAlignmentOptions.Center;

    [Header("Body")]
    public Color bodyColor = new Color(1f, 1f, 1f, 0.9f);
    public Vector2 bodyAnchoredPosition = new Vector2(0f, -48f);
    public Vector2 bodySizeDelta = new Vector2(1200f, 200f);
    public float bodyFontSize = 32f;
    public FontStyles bodyFontStyle = FontStyles.Normal;
    public TextAlignmentOptions bodyAlignment = TextAlignmentOptions.Center;

    [Header("Fonts (optional)")]
    [Tooltip("Leave empty to use TMP default font.")]
    public TMP_FontAsset titleFont;
    [Tooltip("Leave empty to use TMP default font.")]
    public TMP_FontAsset bodyFont;
}

/// <summary>
/// Fullscreen or centered banner for level/area titles. Built at runtime if needed.
/// </summary>
public class LevelAreaIntroUI : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private TextMeshProUGUI titleTmp;
    [SerializeField] private TextMeshProUGUI bodyTmp;

    private bool built;
    private Coroutine running;
    private static LevelAreaIntroUI s_instance;

    private static readonly LevelAreaIntroPresentation s_defaultPresentation = new LevelAreaIntroPresentation();

    public static LevelAreaIntroUI Instance
    {
        get
        {
            if (s_instance != null) return s_instance;
            LevelAreaIntroUI found = FindFirstObjectByType<LevelAreaIntroUI>();
            if (found != null)
            {
                s_instance = found;
                return s_instance;
            }

            GameObject host = new GameObject("LevelAreaIntroUI");
            LevelAreaIntroUI ui = host.AddComponent<LevelAreaIntroUI>();
            ui.BuildRuntimeUI();
            s_instance = ui;
            return s_instance;
        }
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        if (canvas != null && rootGroup != null && titleTmp != null)
            built = true;
    }

    private void OnDestroy()
    {
        if (s_instance == this)
            s_instance = null;
    }

    /// <summary>Show title + optional body, then fade out. Uses unscaled time (works during pause-style freezes).</summary>
    public void Show(string title, string body, float holdSeconds, float fadeInDuration, float fadeOutDuration)
    {
        Show(title, body, holdSeconds, fadeInDuration, fadeOutDuration, null);
    }

    /// <summary>Same as <see cref="Show(string,string,float,float,float)"/> but applies layout/colors/fonts from <paramref name="presentation"/>.</summary>
    public void Show(string title, string body, float holdSeconds, float fadeInDuration, float fadeOutDuration, LevelAreaIntroPresentation presentation)
    {
        EnsureBuilt();

        // Unity can't start coroutines on inactive GameObjects.
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        if (running != null)
            StopCoroutine(running);

        running = StartCoroutine(RunShow(title, body, holdSeconds, fadeInDuration, fadeOutDuration, presentation));
    }

    private IEnumerator RunShow(string title, string body, float holdSeconds, float fadeInDuration, float fadeOutDuration, LevelAreaIntroPresentation presentation)
    {
        EnsureBuilt();
        ApplyPresentation(presentation != null ? presentation : s_defaultPresentation);
        titleTmp.text = title ?? string.Empty;
        bool hasBody = !string.IsNullOrWhiteSpace(body);
        if (bodyTmp != null)
        {
            bodyTmp.gameObject.SetActive(hasBody);
            bodyTmp.text = hasBody ? body : string.Empty;
        }

        gameObject.SetActive(true);
        rootGroup.alpha = 0f;
        yield return FadeGroup(rootGroup, 0f, 1f, fadeInDuration);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, holdSeconds));
        yield return FadeGroup(rootGroup, 1f, 0f, fadeOutDuration);
        rootGroup.alpha = 0f;
        gameObject.SetActive(false);
        running = null;
    }

    private void EnsureBuilt()
    {
        if (!built || canvas == null || rootGroup == null || titleTmp == null)
            BuildRuntimeUI();
    }

    private void BuildRuntimeUI()
    {
        canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(transform, false);
        RectTransform prt = panel.GetComponent<RectTransform>();
        StretchFull(prt);

        rootGroup = panel.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        GameObject titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(panel.transform, false);
        RectTransform trt = titleGo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 0.5f);
        trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = new Vector2(0f, 40f);
        trt.sizeDelta = new Vector2(1400f, 120f);

        titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            titleTmp.font = TMP_Settings.defaultFontAsset;

        GameObject bodyGo = new GameObject("Body", typeof(RectTransform));
        bodyGo.transform.SetParent(panel.transform, false);
        RectTransform brt = bodyGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.5f);
        brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);

        bodyTmp = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyTmp.enableWordWrapping = true;
        if (TMP_Settings.defaultFontAsset != null)
            bodyTmp.font = TMP_Settings.defaultFontAsset;

        ApplyPresentation(s_defaultPresentation);

        built = true;
        gameObject.SetActive(false);
    }

    private void ApplyPresentation(LevelAreaIntroPresentation p)
    {
        if (p == null || titleTmp == null)
            return;

        RectTransform trt = titleTmp.rectTransform;
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = p.titleAnchoredPosition;
        trt.sizeDelta = p.titleSizeDelta;
        titleTmp.color = p.titleColor;
        titleTmp.fontSize = p.titleFontSize;
        titleTmp.fontStyle = p.titleFontStyle;
        titleTmp.alignment = p.titleAlignment;
        if (p.titleFont != null)
            titleTmp.font = p.titleFont;

        if (bodyTmp == null)
            return;

        RectTransform brt = bodyTmp.rectTransform;
        brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = p.bodyAnchoredPosition;
        brt.sizeDelta = p.bodySizeDelta;
        bodyTmp.color = p.bodyColor;
        bodyTmp.fontSize = p.bodyFontSize;
        bodyTmp.fontStyle = p.bodyFontStyle;
        bodyTmp.alignment = p.bodyAlignment;
        if (p.bodyFont != null)
            bodyTmp.font = p.bodyFont;
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }

        group.alpha = to;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
