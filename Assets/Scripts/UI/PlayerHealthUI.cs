using UnityEngine;
using UnityEngine.UI;

/// <summary>Screen-space player health bar (bottom center). Subscribes to <see cref="PlayerHealth"/>.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-150)]
public class PlayerHealthUI : MonoBehaviour
{
    [SerializeField] PlayerHealth playerHealth;
    Image fillImage;
    Text labelText;

    void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = FindObjectOfType<PlayerHealth>();

        BuildUI();
    }

    void Start()
    {
        RefreshAll();
    }

    void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged += OnHealthChanged;
        RefreshAll();
    }

    void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHealthChanged -= OnHealthChanged;
    }

    void OnHealthChanged(int current, int max) => Refresh(current, max);

    void RefreshAll()
    {
        if (playerHealth == null) return;
        Refresh(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    void Refresh(int current, int max)
    {
        if (fillImage != null && max > 0)
            fillImage.fillAmount = Mathf.Clamp01((float)current / max);

        if (labelText != null)
            labelText.text = $"HP {Mathf.Max(0, current)} / {max}";
    }

    void BuildUI()
    {
        Canvas canvas = GameplayHudCanvas.GetOrCreate();

        GameObject panel = new GameObject("PlayerHealthPanel", typeof(RectTransform));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, GameplayHudCanvas.HealthPanelBottomFromScreen());
        // 50% larger than original 320×52
        panelRt.sizeDelta = new Vector2(480f, 78f);

        GameObject labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(panel.transform, false);
        labelText = labelGo.AddComponent<Text>();
        Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (uiFont != null)
            labelText.font = uiFont;
        labelText.fontSize = 27;
        labelText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        labelText.alignment = TextAnchor.LowerCenter;
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0.5f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.offsetMin = new Vector2(0f, 0f);
        labelRt.offsetMax = new Vector2(0f, -4f);

        GameObject bgGo = new GameObject("BarBackground", typeof(RectTransform));
        bgGo.transform.SetParent(panel.transform, false);
        var bg = bgGo.AddComponent<Image>();
        bg.sprite = UiSpriteUtility.WhiteSprite();
        bg.color = new Color(0.12f, 0.12f, 0.14f, 0.94f);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0f, 0f);
        bgRt.anchorMax = new Vector2(1f, 0f);
        bgRt.pivot = new Vector2(0.5f, 0f);
        bgRt.anchoredPosition = new Vector2(0f, 6f);
        bgRt.sizeDelta = new Vector2(0f, 33f);

        GameObject fillGo = new GameObject("BarFill", typeof(RectTransform));
        fillGo.transform.SetParent(bgGo.transform, false);
        fillImage = fillGo.AddComponent<Image>();
        fillImage.sprite = UiSpriteUtility.WhiteSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.color = new Color(0.25f, 0.78f, 0.38f, 1f);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(5f, 5f);
        fillRt.offsetMax = new Vector2(-5f, -5f);
    }
}
