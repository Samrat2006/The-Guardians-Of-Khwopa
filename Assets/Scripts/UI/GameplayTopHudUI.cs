using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top HUD with icon "chips" (icon + count + background).
/// Either builds UI at runtime under <c>GameplayHUD</c>, or binds to your own Canvas (manual mode).
/// </summary>
[DisallowMultipleComponent]
public class GameplayTopHudUI : MonoBehaviour
{
    [Header("HUD mode")]
    [Tooltip("Off = do not create UI in code; drag your own TextMeshProUGUI fields below.")]
    [SerializeField] private bool createHudAtRuntime = true;

    [Header("Manual HUD (only when Create Hud At Runtime is off)")]
    [SerializeField] private TextMeshProUGUI manualArrowsCount;
    [SerializeField] private TextMeshProUGUI manualQuestCount;
    [SerializeField] private TextMeshProUGUI manualCoinsCount;

    [Header("Data sources (optional; auto-found if empty)")]
    [SerializeField] private CoinCollector coinCollector;
    [SerializeField] private QuestManager questManager;
    [SerializeField] private PlayerBowShoot bowShoot;
    [SerializeField] private ArrowInventory arrowInventory;

    [Header("Arrows")]
    [Tooltip("Fallback if ArrowInventory isn't found.")]
    [SerializeField] private int arrowsCount = 30;

    [Header("Arrow / Quest HUD icons (runtime build)")]
    [Tooltip("Your PNG (Sprite). Shown inside the circular slot; leave empty to use the drawn placeholder.")]
    [SerializeField] private Sprite arrowHudIcon;
    [SerializeField] private Sprite questHudIcon;

    [Header("Layout — reference corners (quest BL / coin TR / arrow MR)")]
    [SerializeField] private Vector2 screenPad = new Vector2(24f, 24f);
    [Tooltip("Bottom-left quest: lift above bottom edge (leave room for HP bar).")]
    [SerializeField] private float questBottomOffsetY = 108f;

    [Header("Quest — bottom-left rounded card")]
    [SerializeField] private Vector2 questCardSize = new Vector2(152f, 192f);
    [SerializeField] private float questCardIconDiameter = 88f;
    [SerializeField] private float questTitleFontSize = 22f;

    [Header("Arrow — middle-right (big circle + badge)")]
    [SerializeField] private float arrowMainCircleDiameter = 120f;
    [SerializeField] private float arrowBadgeDiameter = 46f;
    [SerializeField] private Color arrowBadgeColor = new Color(0.94f, 0.38f, 0.58f, 1f);
    [SerializeField] private Color arrowBadgeRingColor = new Color(0.72f, 0.22f, 0.42f, 1f);
    [SerializeField] private float arrowBadgeFontSize = 24f;

    [Header("Coin — top-right (big coin + white count box)")]
    [SerializeField] private float coinHeroCircleDiameter = 78f;
    [SerializeField] private Vector2 coinCountBoxSize = new Vector2(118f, 54f);
    [SerializeField] private Color coinCountBoxBg = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color coinCountTextColor = new Color(0.15f, 0.12f, 0.08f, 1f);

    [Header("Circle frame (icons)")]
    [SerializeField] private Color iconCircleFill = new Color(0.18f, 0.2f, 0.24f, 0.92f);
    [SerializeField] private Color iconCircleRing = new Color(0.55f, 0.58f, 0.65f, 0.85f);

    [Header("Style (runtime build only)")]
    [SerializeField] private float uiScale = 1f;
    [SerializeField] private Color chipBg = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color chipBorder = new Color(1f, 1f, 1f, 0.10f);
    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color accentGold = new Color(1f, 0.92f, 0.30f, 0.98f);

    private TextMeshProUGUI arrowsText;
    private TextMeshProUGUI coinsText;

    private GameObject questPanelRoot;
    private TextMeshProUGUI questPanelBodyText;
    private bool questPanelOpen;
    private int ignoreQUntilFrame;

    private Sprite coinIcon;

    [Header("Performance")]
    [Tooltip("How often to refresh TMP text. 0 = every frame (not recommended).")]
    [SerializeField] private float refreshIntervalSeconds = 0.15f;
    private float nextRefreshAt;

    private void Awake()
    {
        if (coinCollector == null) coinCollector = CoinCollector.Instance != null ? CoinCollector.Instance : FindFirstObjectByType<CoinCollector>();
        if (questManager == null) questManager = FindFirstObjectByType<QuestManager>();
        if (bowShoot == null) bowShoot = FindFirstObjectByType<PlayerBowShoot>();
        if (arrowInventory == null) arrowInventory = FindFirstObjectByType<ArrowInventory>();

        if (createHudAtRuntime)
        {
            coinIcon = MakeCoinIconSprite();
            BuildUI();
        }
        else
        {
            arrowsText = manualArrowsCount;
            coinsText = manualCoinsCount;
            Debug.LogWarning(
                "GameplayTopHudUI: Create Hud At Runtime is OFF — hero layout (quest bottom-left / coin top-right / arrow badge) is skipped. Use manual text references or turn it ON.");
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (questPanelOpen)
        {
            questPanelOpen = false;
            DialogueManager.RemoveGlobalGameplayBlock();
        }
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        HandleQuestPanelHotkey();
        // Polling throttled to reduce UI rebuild cost.
        if (refreshIntervalSeconds <= 0f || Time.unscaledTime >= nextRefreshAt)
        {
            nextRefreshAt = Time.unscaledTime + Mathf.Max(0.02f, refreshIntervalSeconds);
            Refresh();
        }
    }

    private void HandleQuestPanelHotkey()
    {
        if (Time.frameCount <= ignoreQUntilFrame)
            return;

        if (!Input.GetKeyDown(KeyCode.Q))
            return;

        if (questPanelOpen)
        {
            SetQuestPanelOpen(false);
            return;
        }

        if (DialogueManager.IsBlockingGameplay)
            return;

        SetQuestPanelOpen(true);
    }

    private void SetQuestPanelOpen(bool open)
    {
        if (questPanelRoot == null)
            return;

        if (open == questPanelOpen)
            return;

        if (open && DialogueManager.IsBlockingGameplay)
            return;

        questPanelOpen = open;
        questPanelRoot.SetActive(open);
        ignoreQUntilFrame = Time.frameCount + 1;

        if (open)
            DialogueManager.AddGlobalGameplayBlock();
        else
            DialogueManager.RemoveGlobalGameplayBlock();

        if (open)
            RefreshQuestPanelBody();
    }

    private void ToggleQuestPanelFromCardClick()
    {
        if (Time.frameCount <= ignoreQUntilFrame)
            return;

        if (questPanelOpen)
            SetQuestPanelOpen(false);
        else if (!DialogueManager.IsBlockingGameplay)
            SetQuestPanelOpen(true);
    }

    private void Refresh()
    {
        if (arrowsText != null)
        {
            int arrows = arrowInventory != null ? arrowInventory.Arrows : arrowsCount;
            arrowsText.text = arrows.ToString();
        }

        if (questPanelOpen && questPanelBodyText != null)
            RefreshQuestPanelBody();

        if (coinsText != null)
        {
            int coins = coinCollector != null ? coinCollector.coins : 0;
            coinsText.text = coins.ToString();
        }
    }

    private void RefreshQuestPanelBody()
    {
        if (questPanelBodyText == null)
            return;
        if (questManager == null)
        {
            questPanelBodyText.text = "<i>No active quest.</i>";
            return;
        }

        questPanelBodyText.text = questManager.BuildQuestSummaryMultiline();
    }

    private void BuildUI()
    {
        Canvas canvas = FindOrCreateHudCanvas();
        ClearHudCanvasChildren(canvas.transform);

        Vector2 pad = screenPad;
        float s = uiScale;

        // Bottom-left: quest card (above HP area)
        float questRowH = questCardSize.y * s + 24f * s;
        RectTransform questRow = CreateRow(
            canvas.transform,
            "BottomLeftQuestRow",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(pad.x, questBottomOffsetY),
            questRowH,
            TextAnchor.LowerLeft);
        CreateQuestBottomLeftCard(questRow);

        // Top-right: coin hero
        float coinRowH = Mathf.Max(coinHeroCircleDiameter, coinCountBoxSize.y) * s + 20f * s;
        RectTransform coinRow = CreateRow(
            canvas.transform,
            "TopRightCoinRow",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-pad.x, -pad.y),
            coinRowH,
            TextAnchor.UpperRight);
        coinsText = CreateCoinHeroTopRight(coinRow);

        // Middle-right: arrow circle + count badge
        float arrowRowH = (arrowMainCircleDiameter + 24f) * s;
        RectTransform arrowRow = CreateRow(
            canvas.transform,
            "MiddleRightArrowRow",
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-pad.x, 0f),
            arrowRowH,
            TextAnchor.MiddleRight);
        arrowsText = CreateArrowCircleBadgeHud(arrowRow);

        CreateQuestJournalPanel(canvas.transform);
    }

    /// <summary>
    /// Removes old runtime HUD so you never stack duplicate bars from previous versions.
    /// </summary>
    private static void ClearHudCanvasChildren(Transform canvasTransform)
    {
        for (int i = canvasTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvasTransform.GetChild(i);
            if (child != null && child.name == "PlayerHealthPanel")
                continue;
            Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Canvas FindOrCreateHudCanvas()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name == "GameplayHUD")
                return c;
        }

        GameObject go = new GameObject("GameplayHUD");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private RectTransform CreateRow(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, float rowHeight, TextAnchor childAlignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(900f, rowHeight);

        HorizontalLayoutGroup h = go.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = childAlignment;
        h.spacing = 12f * uiScale;
        h.childControlWidth = false;
        h.childControlHeight = false;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.padding = new RectOffset(0, 0, 0, 0);

        return rt;
    }

    private void CreateQuestBottomLeftCard(RectTransform row)
    {
        float s = uiScale;
        Vector2 cardSize = questCardSize * s;

        GameObject card = new GameObject("QuestCard", typeof(RectTransform));
        card.transform.SetParent(row, false);
        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.sizeDelta = cardSize;

        int corner = Mathf.RoundToInt(22f * (256f / Mathf.Max(questCardSize.x, 1f)));
        corner = Mathf.Clamp(corner, 14, 48);
        Sprite cardSprite = MakeRoundedRectSlicedSprite(256, 256, corner);

        Image cardBg = card.AddComponent<Image>();
        cardBg.sprite = cardSprite;
        cardBg.type = Image.Type.Sliced;
        cardBg.color = chipBg;

        Outline o = card.AddComponent<Outline>();
        o.effectColor = chipBorder;
        o.effectDistance = new Vector2(1f, -1f);

        Button cardBtn = card.AddComponent<Button>();
        cardBtn.targetGraphic = cardBg;
        cardBtn.transition = Selectable.Transition.None;
        cardBtn.onClick.AddListener(ToggleQuestPanelFromCardClick);

        // Title
        GameObject titleGo = new GameObject("QuestTitle", typeof(RectTransform));
        titleGo.transform.SetParent(card.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -10f * s);
        titleRt.sizeDelta = new Vector2(0f, 36f * s);

        TextMeshProUGUI titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "Quest";
        titleTmp.fontSize = questTitleFontSize * s;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = accentGold;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) titleTmp.font = TMP_Settings.defaultFontAsset;

        float iconD = Mathf.Min(questCardIconDiameter * s, cardSize.y - 56f * s);
        GameObject slot = new GameObject("QuestIcon", typeof(RectTransform));
        slot.transform.SetParent(card.transform, false);
        RectTransform slotRt = slot.GetComponent<RectTransform>();
        slotRt.anchorMin = slotRt.anchorMax = new Vector2(0.5f, 0.5f);
        slotRt.pivot = new Vector2(0.5f, 0.5f);
        slotRt.sizeDelta = new Vector2(iconD, iconD);
        slotRt.anchoredPosition = new Vector2(0f, -14f * s);

        AddCircledIcon(slot, questHudIcon, MakeQuestIconSprite, iconD);
    }

    /// <summary>Left 35% journal; toggled with Q (and quest mini card click).</summary>
    private void CreateQuestJournalPanel(Transform canvasTransform)
    {
        float s = uiScale;

        GameObject root = new GameObject("QuestJournalPanel", typeof(RectTransform));
        root.transform.SetParent(canvasTransform, false);
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0f, 0f);
        rootRt.anchorMax = new Vector2(0.35f, 1f);
        rootRt.pivot = new Vector2(0f, 0.5f);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        questPanelRoot = root;
        root.SetActive(false);

        Image panelBg = root.AddComponent<Image>();
        int corner = 18;
        panelBg.sprite = MakeRoundedRectSlicedSprite(256, 256, corner);
        panelBg.type = Image.Type.Sliced;
        panelBg.color = new Color(0.06f, 0.07f, 0.09f, 0.94f);

        Outline panelOl = root.AddComponent<Outline>();
        panelOl.effectColor = new Color(1f, 1f, 1f, 0.08f);
        panelOl.effectDistance = new Vector2(1f, -1f);

        float pad = 28f * s;

        GameObject titleGo = new GameObject("JournalTitle", typeof(RectTransform));
        titleGo.transform.SetParent(root.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -pad);
        titleRt.sizeDelta = new Vector2(-pad * 2f, 44f * s);

        TextMeshProUGUI titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "Quest log";
        titleTmp.fontSize = 34f * s;
        titleTmp.alignment = TextAlignmentOptions.Left;
        titleTmp.color = accentGold;
        titleTmp.fontStyle = FontStyles.Bold;
        if (TMP_Settings.defaultFontAsset != null) titleTmp.font = TMP_Settings.defaultFontAsset;

        GameObject bodyGo = new GameObject("JournalBody", typeof(RectTransform));
        bodyGo.transform.SetParent(root.transform, false);
        RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 1f);
        bodyRt.offsetMin = new Vector2(pad, pad + 36f * s);
        bodyRt.offsetMax = new Vector2(-pad, -pad - 52f * s);

        questPanelBodyText = bodyGo.AddComponent<TextMeshProUGUI>();
        questPanelBodyText.text = "";
        questPanelBodyText.fontSize = 26f * s;
        questPanelBodyText.alignment = TextAlignmentOptions.TopLeft;
        questPanelBodyText.color = textColor;
        questPanelBodyText.enableWordWrapping = true;
        questPanelBodyText.richText = true;
        if (TMP_Settings.defaultFontAsset != null) questPanelBodyText.font = TMP_Settings.defaultFontAsset;

        GameObject hintGo = new GameObject("JournalHint", typeof(RectTransform));
        hintGo.transform.SetParent(root.transform, false);
        RectTransform hintRt = hintGo.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0f, 0f);
        hintRt.anchorMax = new Vector2(1f, 0f);
        hintRt.pivot = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, pad);
        hintRt.sizeDelta = new Vector2(-pad * 2f, 32f * s);

        TextMeshProUGUI hintTmp = hintGo.AddComponent<TextMeshProUGUI>();
        hintTmp.text = "Press Q to close";
        hintTmp.fontSize = 20f * s;
        hintTmp.alignment = TextAlignmentOptions.Left;
        hintTmp.color = new Color(1f, 1f, 1f, 0.55f);
        hintTmp.fontStyle = FontStyles.Italic;
        if (TMP_Settings.defaultFontAsset != null) hintTmp.font = TMP_Settings.defaultFontAsset;

        root.transform.SetAsLastSibling();
    }

    private TextMeshProUGUI CreateCoinHeroTopRight(RectTransform row)
    {
        float s = uiScale;
        float d = coinHeroCircleDiameter * s;
        Vector2 boxSz = coinCountBoxSize * s;
        float gap = 12f * s;
        float edge = 6f * s;
        float totalW = boxSz.x + gap + d + edge * 2f;
        float totalH = Mathf.Max(d, boxSz.y) + 8f * s;

        GameObject root = new GameObject("CoinHero", typeof(RectTransform));
        root.transform.SetParent(row, false);
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(totalW, totalH);

        int cornerBox = Mathf.Clamp(Mathf.RoundToInt(18f * (256f / coinCountBoxSize.x)), 10, 40);
        Sprite boxSprite = MakeRoundedRectSlicedSprite(256, 96, cornerBox);

        GameObject boxGo = new GameObject("CoinCountBox", typeof(RectTransform));
        boxGo.transform.SetParent(root.transform, false);
        RectTransform boxRt = boxGo.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0f, 0.5f);
        boxRt.anchorMax = new Vector2(0f, 0.5f);
        boxRt.pivot = new Vector2(0f, 0.5f);
        boxRt.anchoredPosition = new Vector2(edge, 0f);
        boxRt.sizeDelta = boxSz;

        Image boxImg = boxGo.AddComponent<Image>();
        boxImg.sprite = boxSprite;
        boxImg.type = Image.Type.Sliced;
        boxImg.color = coinCountBoxBg;

        Outline boxOl = boxGo.AddComponent<Outline>();
        boxOl.effectColor = new Color(0f, 0f, 0f, 0.2f);
        boxOl.effectDistance = new Vector2(1f, -1f);

        GameObject boxTxtGo = new GameObject("Count", typeof(RectTransform));
        boxTxtGo.transform.SetParent(boxGo.transform, false);
        RectTransform boxTxtRt = boxTxtGo.GetComponent<RectTransform>();
        boxTxtRt.anchorMin = Vector2.zero;
        boxTxtRt.anchorMax = Vector2.one;
        boxTxtRt.offsetMin = Vector2.zero;
        boxTxtRt.offsetMax = Vector2.zero;

        TextMeshProUGUI boxTmp = boxTxtGo.AddComponent<TextMeshProUGUI>();
        boxTmp.fontSize = 32f * s;
        boxTmp.alignment = TextAlignmentOptions.Midline;
        boxTmp.color = coinCountTextColor;
        boxTmp.text = "0";
        boxTmp.fontStyle = FontStyles.Bold;
        if (TMP_Settings.defaultFontAsset != null) boxTmp.font = TMP_Settings.defaultFontAsset;

        GameObject coinSlot = new GameObject("CoinCircle", typeof(RectTransform));
        coinSlot.transform.SetParent(root.transform, false);
        RectTransform coinRt = coinSlot.GetComponent<RectTransform>();
        coinRt.anchorMin = new Vector2(1f, 0.5f);
        coinRt.anchorMax = new Vector2(1f, 0.5f);
        coinRt.pivot = new Vector2(1f, 0.5f);
        coinRt.anchoredPosition = new Vector2(-edge, 0f);
        coinRt.sizeDelta = new Vector2(d, d);
        AddCircledIcon(coinSlot, null, () => coinIcon, d);

        return boxTmp;
    }

    private TextMeshProUGUI CreateArrowCircleBadgeHud(RectTransform row)
    {
        float s = uiScale;
        float d = arrowMainCircleDiameter * s;
        float bd = arrowBadgeDiameter * s;
        float inset = 7f * s;

        GameObject root = new GameObject("ArrowHero", typeof(RectTransform));
        root.transform.SetParent(row, false);
        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.sizeDelta = new Vector2(d + 8f * s, d + 8f * s);

        GameObject slot = new GameObject("ArrowMainCircle", typeof(RectTransform));
        slot.transform.SetParent(root.transform, false);
        RectTransform slotRt = slot.GetComponent<RectTransform>();
        slotRt.anchorMin = slotRt.anchorMax = new Vector2(0.5f, 0.5f);
        slotRt.pivot = new Vector2(0.5f, 0.5f);
        slotRt.sizeDelta = new Vector2(d, d);
        slotRt.anchoredPosition = Vector2.zero;

        AddCircledIcon(slot, arrowHudIcon, MakeArrowIconSprite, d);

        GameObject badge = new GameObject("ArrowCountBadge", typeof(RectTransform));
        badge.transform.SetParent(root.transform, false);
        RectTransform badgeRt = badge.GetComponent<RectTransform>();
        badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(0.5f, 0.5f);
        badgeRt.pivot = new Vector2(0.5f, 0.5f);
        badgeRt.sizeDelta = new Vector2(bd, bd);
        badgeRt.anchoredPosition = new Vector2(
            -d * 0.5f + bd * 0.5f + inset,
            d * 0.5f - bd * 0.5f - inset);

        Sprite badgeSprite = MakeCircleRingSprite(96, arrowBadgeColor, arrowBadgeRingColor);
        Image badgeImg = badge.AddComponent<Image>();
        badgeImg.sprite = badgeSprite;
        badgeImg.color = Color.white;
        badgeImg.preserveAspect = true;

        GameObject badgeTxtGo = new GameObject("Count", typeof(RectTransform));
        badgeTxtGo.transform.SetParent(badge.transform, false);
        RectTransform btRt = badgeTxtGo.GetComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.offsetMin = Vector2.zero;
        btRt.offsetMax = Vector2.zero;

        TextMeshProUGUI badgeTmp = badgeTxtGo.AddComponent<TextMeshProUGUI>();
        badgeTmp.fontSize = arrowBadgeFontSize * s;
        badgeTmp.alignment = TextAlignmentOptions.Midline;
        badgeTmp.color = Color.white;
        badgeTmp.text = "0";
        badgeTmp.fontStyle = FontStyles.Bold;
        if (TMP_Settings.defaultFontAsset != null) badgeTmp.font = TMP_Settings.defaultFontAsset;

        return badgeTmp;
    }

    private void AddCircledIcon(GameObject slot, Sprite userIcon, System.Func<Sprite> fallback, float diameter)
    {
        Sprite circleSprite = MakeCircleRingSprite(128, iconCircleFill, iconCircleRing);

        GameObject maskHost = new GameObject("CircleMask", typeof(RectTransform));
        maskHost.transform.SetParent(slot.transform, false);
        RectTransform maskRt = maskHost.GetComponent<RectTransform>();
        maskRt.anchorMin = Vector2.zero;
        maskRt.anchorMax = Vector2.one;
        maskRt.offsetMin = Vector2.zero;
        maskRt.offsetMax = Vector2.zero;

        Image maskImg = maskHost.AddComponent<Image>();
        maskImg.sprite = circleSprite;
        maskImg.color = Color.white;
        maskImg.preserveAspect = true;
        Mask mask = maskHost.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        GameObject iconChild = new GameObject("Icon", typeof(RectTransform));
        iconChild.transform.SetParent(maskHost.transform, false);
        RectTransform iconRt = iconChild.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(diameter * 0.86f, diameter * 0.86f);
        iconRt.anchoredPosition = Vector2.zero;

        Image iconImg = iconChild.AddComponent<Image>();
        iconImg.sprite = userIcon != null ? userIcon : fallback();
        iconImg.preserveAspect = true;
        iconImg.color = Color.white;
    }

    private static Sprite MakeRoundedRectSlicedSprite(int width, int height, int cornerRadius)
    {
        cornerRadius = Mathf.Clamp(cornerRadius, 4, Mathf.Min(width, height) / 2 - 2);
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32 fill = new Color32(255, 255, 255, 255);
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32[] px = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                px[y * width + x] = InsideRoundedRect(x, y, width, height, cornerRadius) ? fill : clear;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        float b = cornerRadius;
        return Sprite.Create(
            tex,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(b, b, b, b));
    }

    private static bool InsideRoundedRect(int x, int y, int w, int h, int r)
    {
        if (x < 0 || y < 0 || x >= w || y >= h) return false;
        r = Mathf.Min(r, Mathf.Min(w, h) / 2);
        if (x >= r && x < w - r) return true;
        if (y >= r && y < h - r) return true;

        int x0 = r;
        int y0 = r;
        if (x < r && y < r)
            return DistSq(x, y, x0, y0) <= r * r;

        int x1 = w - r - 1;
        if (x > x1 && y < r)
            return DistSq(x, y, x1, y0) <= r * r;

        int y1 = h - r - 1;
        if (x < r && y > y1)
            return DistSq(x, y, x0, y1) <= r * r;

        if (x > x1 && y > y1)
            return DistSq(x, y, x1, y1) <= r * r;

        return false;
    }

    private static int DistSq(int x, int y, int cx, int cy)
    {
        int dx = x - cx;
        int dy = y - cy;
        return dx * dx + dy * dy;
    }

    private Sprite MakeCircleRingSprite(int texSize, Color fill, Color ring)
    {
        Color32 f = fill;
        Color32 rCol = ring;
        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        int cx = texSize / 2;
        int cy = texSize / 2;
        float radius = texSize * 0.5f - 3f;
        float ringOuter = radius;
        float ringInner = radius - 3.5f;

        Color32[] px = new Color32[texSize * texSize];
        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                Color32 c;
                if (d > ringOuter)
                    c = new Color32(0, 0, 0, 0);
                else if (d > ringInner)
                    c = rCol;
                else
                    c = f;
                px[y * texSize + x] = c;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), 100f);
    }

    // ----- Runtime placeholder icons (no PNGs needed) -----

    private static Sprite MakeArrowIconSprite() => MakeIconSprite(64, DrawArrow);
    private static Sprite MakeQuestIconSprite() => MakeIconSprite(64, DrawScroll);
    private static Sprite MakeCoinIconSprite() => MakeIconSprite(64, DrawCoin);

    private delegate void IconDrawer(Color32[] px, int size);

    private static Sprite MakeIconSprite(int size, IconDrawer draw)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32[] px = new Color32[size * size];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);
        draw(px, size);

        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void Plot(Color32[] px, int size, int x, int y, Color32 c)
    {
        if ((uint)x >= (uint)size || (uint)y >= (uint)size) return;
        px[y * size + x] = c;
    }

    private static void FillRect(Color32[] px, int size, int x0, int y0, int x1, int y1, Color32 c)
    {
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                Plot(px, size, x, y, c);
    }

    private static void DrawArrow(Color32[] px, int size)
    {
        Color32 white = new Color32(245, 245, 245, 255);
        // Shaft
        FillRect(px, size, 14, 30, 44, 34, white);
        // Head
        for (int i = 0; i < 12; i++)
        {
            int x = 44 + i;
            int yMid = 32;
            int half = i / 2;
            Plot(px, size, x, yMid + half, white);
            Plot(px, size, x, yMid - half, white);
        }
        // Fletching
        FillRect(px, size, 10, 28, 14, 30, white);
        FillRect(px, size, 10, 34, 14, 36, white);
    }

    private static void DrawCoin(Color32[] px, int size)
    {
        Color32 gold = new Color32(255, 210, 70, 255);
        Color32 dark = new Color32(180, 120, 25, 255);
        int cx = size / 2;
        int cy = size / 2;
        int r = 20;
        for (int y = -r; y <= r; y++)
        {
            for (int x = -r; x <= r; x++)
            {
                int d = x * x + y * y;
                if (d <= r * r)
                {
                    Color32 c = (d >= (r - 2) * (r - 2)) ? dark : gold;
                    Plot(px, size, cx + x, cy + y, c);
                }
            }
        }
        // highlight
        FillRect(px, size, cx - 6, cy + 6, cx - 2, cy + 10, new Color32(255, 245, 190, 220));
    }

    private static void DrawScroll(Color32[] px, int size)
    {
        Color32 paper = new Color32(245, 235, 210, 255);
        Color32 ink = new Color32(120, 85, 45, 255);
        // body
        FillRect(px, size, 18, 18, 46, 46, paper);
        // rolled edges
        FillRect(px, size, 14, 20, 18, 44, paper);
        FillRect(px, size, 46, 20, 50, 44, paper);
        // lines
        FillRect(px, size, 22, 38, 42, 39, ink);
        FillRect(px, size, 22, 32, 40, 33, ink);
        FillRect(px, size, 22, 26, 38, 27, ink);
    }
}

