using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space booster / super meter bar (bottom center; default stack: booster below health). Subscribes to <see cref="PlayerSuperMeter"/>.
/// You do <b>not</b> need to assign <see cref="superMeter"/> if this component sits on the same <see cref="GameObject"/> as <see cref="PlayerSuperMeter"/> (or its child).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class PlayerSuperMeterUI : MonoBehaviour
{
    [Tooltip("Optional. Leave empty to auto-find on this object, parent, or in the scene.")]
    [SerializeField] private PlayerSuperMeter superMeter;

    [Header("Booster bar — scale & position (1920×1080 reference)")]
    [Tooltip("Scales panel width/height, bar thickness, label font, and fill padding together.")]
    [SerializeField] [Range(0.35f, 2.5f)] private float barScale = 1f;

    [SerializeField] private bool boosterBelowHealthBar = true;
    [Tooltip("When auto is on: nudges the booster bar up/down (pixels).")]
    [SerializeField] private float extraBottomOffset = 0f;
    [Tooltip("Default panel width before bar scale.")]
    [SerializeField] private float basePanelWidth = 480f;
    [Tooltip("Default panel height before bar scale.")]
    [SerializeField] private float basePanelHeight = 78f;
    [Tooltip("Inner bar strip height before bar scale.")]
    [SerializeField] private float baseBarThickness = 33f;
    [SerializeField] private float baseLabelFontSize = 27f;
    [SerializeField] private float baseFillInset = 5f;
    [Tooltip("Space between label area and bar inside panel.")]
    [SerializeField] private float baseBarPaddingFromPanelBottom = 6f;

    private Image fillImage;
    private Text labelText;
    private bool hudBuilt;
    private bool subscribed;

    private void Awake()
    {
        GameplayHudCanvas.BoosterBelowHealthBar = boosterBelowHealthBar;
        ResolveSuperMeter();
        BuildUI();
    }

    private void Start()
    {
        GameplayHudCanvas.BoosterBelowHealthBar = boosterBelowHealthBar;
        ResolveSuperMeter();
        TrySubscribe();
        RefreshAll();
    }

    private void OnEnable()
    {
        ResolveSuperMeter();
        TrySubscribe();
        RefreshAll();
    }

    private void OnDisable()
    {
        TryUnsubscribe();
    }

    private void ResolveSuperMeter()
    {
        if (superMeter != null) return;

        superMeter = GetComponent<PlayerSuperMeter>();
        if (superMeter == null)
            superMeter = GetComponentInParent<PlayerSuperMeter>();
        if (superMeter == null)
            superMeter = FindObjectOfType<PlayerSuperMeter>();
    }

    private void TrySubscribe()
    {
        if (superMeter == null) return;
        if (subscribed) return;
        superMeter.OnMeterChanged += OnMeterChanged;
        subscribed = true;
    }

    private void TryUnsubscribe()
    {
        if (!subscribed || superMeter == null) return;
        superMeter.OnMeterChanged -= OnMeterChanged;
        subscribed = false;
    }

    private void OnMeterChanged(float current, float max) => Refresh(current, max);

    private void RefreshAll()
    {
        ResolveSuperMeter();
        float cur = superMeter != null ? superMeter.Current : 0f;
        float max = superMeter != null ? superMeter.Max : 100f;
        Refresh(cur, max);
    }

    private void Refresh(float current, float max)
    {
        if (fillImage != null && max > 0.01f)
            fillImage.fillAmount = Mathf.Clamp01(current / max);

        if (labelText != null)
            labelText.text = $"Boost {Mathf.RoundToInt(Mathf.Max(0f, current))} / {Mathf.RoundToInt(max)}";
    }

    private void BuildUI()
    {
        if (hudBuilt) return;
        hudBuilt = true;

        Canvas canvas = GameplayHudCanvas.GetOrCreate();

        float s = Mathf.Max(0.01f, barScale);
        float w = basePanelWidth * s;
        float h = basePanelHeight * s;
        float barH = baseBarThickness * s;
        int fontPx = Mathf.Max(8, Mathf.RoundToInt(baseLabelFontSize * s));
        float inset = baseFillInset * s;
        float barPad = baseBarPaddingFromPanelBottom * s;

        float boosterBottom = GameplayHudCanvas.BoosterPanelBottomFromScreen(extraBottomOffset);

        GameObject panel = new GameObject("PlayerBoosterPanel", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, boosterBottom);
        panelRt.sizeDelta = new Vector2(w, h);

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(panel.transform, false);
        labelText = labelGo.AddComponent<Text>();
        labelText.font = ResolveUiFont();
        labelText.fontSize = fontPx;
        labelText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        labelText.alignment = TextAnchor.LowerCenter;
        RectTransform labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0.5f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = new Vector2(0f, 0f);
        labelRt.offsetMax = new Vector2(0f, -4f);

        GameObject bgGo = new GameObject("BarBackground", typeof(RectTransform));
        bgGo.transform.SetParent(panel.transform, false);
        Image bg = bgGo.AddComponent<Image>();
        bg.sprite = UiSpriteUtility.WhiteSprite();
        bg.color = new Color(0.1f, 0.1f, 0.16f, 0.94f);
        RectTransform bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0f);
        bgRt.anchorMax = new Vector2(1f, 0f);
        bgRt.pivot = new Vector2(0.5f, 0f);
        bgRt.anchoredPosition = new Vector2(0f, barPad);
        bgRt.sizeDelta = new Vector2(0f, barH);

        GameObject fillGo = new GameObject("BarFill", typeof(RectTransform));
        fillGo.transform.SetParent(bgGo.transform, false);
        fillImage = fillGo.AddComponent<Image>();
        fillImage.sprite = UiSpriteUtility.WhiteSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.color = new Color(0.35f, 0.72f, 1f, 1f);
        fillImage.fillAmount = 0f;
        RectTransform fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(inset, inset);
        fillRt.offsetMax = new Vector2(-inset, -inset);

        if (superMeter == null)
            Debug.LogWarning(
                "PlayerSuperMeterUI: No PlayerSuperMeter found yet — booster bar is shown at 0%. Add PlayerSuperMeter to the player (same object or parent).",
                this);
    }

    private static Font ResolveUiFont()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;
        f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f != null) return f;

        try
        {
            return Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "Liberation Sans" }, 16);
        }
        catch
        {
            return null;
        }
    }
}
