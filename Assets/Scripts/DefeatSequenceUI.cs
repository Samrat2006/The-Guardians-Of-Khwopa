using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen defeat presentation: reveal "DEFEATED" text, hold, then fade to black.
/// Assign references in the Inspector, or call <see cref="GetOrCreate"/> for a runtime-built overlay.
/// </summary>
public class DefeatSequenceUI : MonoBehaviour
{
    [Header("Manual UI (optional)")]
    [Tooltip("Leave empty to build a simple fullscreen UI at runtime.")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private CanvasGroup textGroup;
    [SerializeField] private TextMeshProUGUI defeatedLabel;

    [Header("Runtime-built UI (when references above are empty)")]
    [SerializeField] private string defeatTitle = "DEFEATED";
    [SerializeField] private float titleFontSize = 96f;
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color fadeToBlackColor = Color.black;

    private bool built;

    public static DefeatSequenceUI GetOrCreate()
    {
        DefeatSequenceUI existing = FindFirstObjectByType<DefeatSequenceUI>();
        if (existing != null)
            return existing;

        GameObject host = new GameObject("DefeatSequenceUI");
        DefeatSequenceUI ui = host.AddComponent<DefeatSequenceUI>();
        ui.BuildRuntimeUI();
        return ui;
    }

    private void Awake()
    {
        if (canvas != null && fadeGroup != null && textGroup != null && defeatedLabel != null)
            built = true;
    }

    private void BuildRuntimeUI()
    {
        if (built) return;

        canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject fadeGo = new GameObject("Fade");
        fadeGo.transform.SetParent(transform, false);
        RectTransform fadeRt = fadeGo.AddComponent<RectTransform>();
        StretchFull(fadeRt);
        Image fadeImg = fadeGo.AddComponent<Image>();
        fadeImg.color = fadeToBlackColor;
        fadeImg.raycastTarget = true;
        fadeGroup = fadeGo.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;

        GameObject textGo = new GameObject("DefeatedText");
        textGo.transform.SetParent(transform, false);
        RectTransform textRt = textGo.AddComponent<RectTransform>();
        StretchFull(textRt);
        defeatedLabel = textGo.AddComponent<TextMeshProUGUI>();
        defeatedLabel.text = defeatTitle;
        defeatedLabel.fontSize = titleFontSize;
        defeatedLabel.alignment = TextAlignmentOptions.Center;
        defeatedLabel.color = titleColor;
        if (TMP_Settings.defaultFontAsset != null)
            defeatedLabel.font = TMP_Settings.defaultFontAsset;
        textGroup = textGo.AddComponent<CanvasGroup>();
        textGroup.alpha = 0f;
        textGroup.blocksRaycasts = false;

        built = true;
        gameObject.SetActive(false);
    }

    private void EnsureReady()
    {
        if (!built || canvas == null || fadeGroup == null || textGroup == null || defeatedLabel == null)
            BuildRuntimeUI();

        fadeGroup.alpha = 0f;
        textGroup.alpha = 0f;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Fades in the defeated text, holds, then fades the full screen to black. Caller loads the scene after this completes.
    /// </summary>
    public IEnumerator RunPresentation(float textFadeInDuration, float defeatedHoldDuration, float fadeToBlackDuration)
    {
        EnsureReady();

        yield return FadeGroup(textGroup, 0f, 1f, textFadeInDuration);
        yield return new WaitForSecondsRealtime(defeatedHoldDuration);
        fadeGroup.blocksRaycasts = true;
        yield return FadeGroup(fadeGroup, 0f, 1f, fadeToBlackDuration);
    }

    private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
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
        rt.localScale = Vector3.one;
    }
}
