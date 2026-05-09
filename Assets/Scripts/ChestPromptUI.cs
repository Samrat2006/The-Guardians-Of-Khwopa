using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple Yes/No prompt used for chest interaction.
/// Built at runtime if no prefab is provided.
/// </summary>
public class ChestPromptUI : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI promptLabel;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private TextMeshProUGUI yesLabel;
    [SerializeField] private TextMeshProUGUI noLabel;
    [SerializeField] private TextMeshProUGUI toastLabel;

    private Action onYes;
    private Action onNo;
    private bool built;

    public static ChestPromptUI GetOrCreate()
    {
        ChestPromptUI existing = FindFirstObjectByType<ChestPromptUI>();
        if (existing != null) return existing;

        GameObject host = new GameObject("ChestPromptUI");
        ChestPromptUI ui = host.AddComponent<ChestPromptUI>();
        ui.BuildRuntimeUI();
        return ui;
    }

    private void Awake()
    {
        if (canvas != null && group != null && panel != null && promptLabel != null && yesButton != null && noButton != null)
            built = true;
    }

    private void BuildRuntimeUI()
    {
        if (built) return;

        canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(transform, false);
        panel = panelGo.AddComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.25f);
        panel.anchorMax = new Vector2(0.5f, 0.25f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(720, 220);
        panel.anchoredPosition = Vector2.zero;

        Image bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.78f);

        Outline outline = panelGo.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.12f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject promptGo = new GameObject("Prompt");
        promptGo.transform.SetParent(panel, false);
        RectTransform promptRt = promptGo.AddComponent<RectTransform>();
        promptRt.anchorMin = new Vector2(0.07f, 0.52f);
        promptRt.anchorMax = new Vector2(0.93f, 0.95f);
        promptRt.offsetMin = Vector2.zero;
        promptRt.offsetMax = Vector2.zero;

        promptLabel = promptGo.AddComponent<TextMeshProUGUI>();
        promptLabel.text = "Would you like to open the chest?";
        promptLabel.fontSize = 40;
        promptLabel.alignment = TextAlignmentOptions.Center;
        promptLabel.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            promptLabel.font = TMP_Settings.defaultFontAsset;

        yesButton = BuildButton(panel, "YesButton", new Vector2(0.18f, 0.10f), new Vector2(0.47f, 0.42f), out yesLabel);
        noButton = BuildButton(panel, "NoButton", new Vector2(0.53f, 0.10f), new Vector2(0.82f, 0.42f), out noLabel);
        yesLabel.text = "Yes";
        noLabel.text = "No";

        GameObject toastGo = new GameObject("Toast");
        toastGo.transform.SetParent(panel, false);
        RectTransform toastRt = toastGo.AddComponent<RectTransform>();
        toastRt.anchorMin = new Vector2(0.05f, 0.00f);
        toastRt.anchorMax = new Vector2(0.95f, 0.20f);
        toastRt.offsetMin = Vector2.zero;
        toastRt.offsetMax = Vector2.zero;
        toastLabel = toastGo.AddComponent<TextMeshProUGUI>();
        toastLabel.text = "";
        toastLabel.fontSize = 28;
        toastLabel.alignment = TextAlignmentOptions.Center;
        toastLabel.color = new Color(1f, 0.92f, 0.3f, 1f);
        if (TMP_Settings.defaultFontAsset != null)
            toastLabel.font = TMP_Settings.defaultFontAsset;

        yesButton.onClick.AddListener(HandleYes);
        noButton.onClick.AddListener(HandleNo);

        built = true;
        gameObject.SetActive(false);
    }

    private static Button BuildButton(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, out TextMeshProUGUI label)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.12f);

        Button btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.12f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.20f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.28f);
        colors.selectedColor = colors.highlightedColor;
        btn.colors = colors;

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        RectTransform tr = textGo.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;

        label = textGo.AddComponent<TextMeshProUGUI>();
        label.fontSize = 34;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        return btn;
    }

    public void Show(string prompt, Action yes, Action no)
    {
        if (!built) BuildRuntimeUI();

        onYes = yes;
        onNo = no;
        promptLabel.text = prompt;
        toastLabel.text = "";

        gameObject.SetActive(true);
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
    }

    public void Hide()
    {
        onYes = null;
        onNo = null;
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    public void SetToast(string message)
    {
        if (!built) BuildRuntimeUI();
        toastLabel.text = message;
    }

    private void HandleYes()
    {
        Action cb = onYes;
        cb?.Invoke();
    }

    private void HandleNo()
    {
        Action cb = onNo;
        cb?.Invoke();
    }
}

