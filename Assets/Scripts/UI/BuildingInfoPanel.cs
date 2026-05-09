using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared UI panel that shows building information. Put this on your BuildingInfoCanvas root (or the panel object).
/// </summary>
[DisallowMultipleComponent]
public class BuildingInfoPanel : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private Image imageView;
    [SerializeField] private TextMeshProUGUI hintText;

    [Header("Defaults")]
    [SerializeField] private string defaultHint = "Press E or Esc to close";

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    [Header("Open/Close animation (scroll reveal)")]
    [SerializeField] private bool animateReveal = true;
    [SerializeField] private float revealDuration = 0.22f;
    [Tooltip("RectTransform that will be resized left→right. If empty, uses panelRoot RectTransform.")]
    [SerializeField] private RectTransform revealTarget;
    [Tooltip("If set, used to fade content during reveal (optional).")]
    [SerializeField] private CanvasGroup fadeGroup;

    private Coroutine _revealRoutine;
    private float _fullWidth;

    private void Awake()
    {
        if (panelRoot == null)
            panelRoot = gameObject;

        if (revealTarget == null && panelRoot != null)
            revealTarget = panelRoot.GetComponent<RectTransform>();
        if (fadeGroup == null && panelRoot != null)
            fadeGroup = panelRoot.GetComponent<CanvasGroup>();

        if (revealTarget != null)
            _fullWidth = revealTarget.sizeDelta.x;

        panelRoot.SetActive(false);
    }

    public void Show(BuildingInfoSource source)
    {
        if (panelRoot == null) return;

        if (titleText != null)
            titleText.text = source != null ? (source.Title ?? "") : "";

        if (bodyText != null)
            bodyText.text = source != null ? (source.Description ?? "") : "";

        if (imageView != null)
        {
            Sprite s = source != null ? source.Image : null;
            imageView.gameObject.SetActive(s != null);
            if (s != null) imageView.sprite = s;
        }

        if (hintText != null)
        {
            string hint = source != null && !string.IsNullOrWhiteSpace(source.HintOverride)
                ? source.HintOverride
                : defaultHint;
            hintText.text = hint ?? "";
        }

        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();

        QuestManager.Resolve()?.NotifyBuildingDiscovered(source != null ? source.QuestBuildingId : "");

        if (animateReveal)
            PlayReveal(open: true);
    }

    public void Hide()
    {
        if (panelRoot == null) return;
        if (animateReveal)
        {
            PlayReveal(open: false);
            return;
        }

        panelRoot.SetActive(false);
    }

    private void PlayReveal(bool open)
    {
        if (revealTarget == null)
        {
            panelRoot.SetActive(open);
            return;
        }

        if (_fullWidth <= 0.01f)
            _fullWidth = revealTarget.sizeDelta.x;

        if (_revealRoutine != null)
            StopCoroutine(_revealRoutine);
        _revealRoutine = StartCoroutine(RevealRoutine(open, revealTarget));
    }

    private IEnumerator RevealRoutine(bool open, RectTransform target)
    {
        // Left-to-right reveal: keep anchored position but grow width.
        float dur = Mathf.Max(0.01f, revealDuration);

        Vector2 size = target.sizeDelta;
        float from = open ? 0f : _fullWidth;
        float to = open ? _fullWidth : 0f;

        // Ensure pivot is left so it "unrolls" to the right.
        target.pivot = new Vector2(0f, target.pivot.y);

        if (fadeGroup != null)
            fadeGroup.alpha = open ? 0f : 1f;

        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / dur);
            // Smoothstep easing.
            a = a * a * (3f - 2f * a);
            float w = Mathf.Lerp(from, to, a);
            target.sizeDelta = new Vector2(w, size.y);
            if (fadeGroup != null)
                fadeGroup.alpha = open ? a : (1f - a);
            yield return null;
        }

        target.sizeDelta = new Vector2(to, size.y);
        if (fadeGroup != null)
            fadeGroup.alpha = open ? 1f : 0f;

        if (!open)
            panelRoot.SetActive(false);

        _revealRoutine = null;
    }
}

