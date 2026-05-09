using UnityEngine;

/// <summary>
/// Makes collectibles easier to spot: pulsing point light + optional emission pulse (URP Lit).
/// Add to coin / arrow pickup / bow pickup prefabs. Tune color per type (gold vs cyan etc.).
/// For a stronger HDR "bloom" look, add Bloom to your Global Volume and use intense glow colors.
/// </summary>
public class CollectibleGlow : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Point light (works without Bloom)")]
    [SerializeField] private bool useLight = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.92f, 0.35f, 1f);
    [SerializeField] private float lightRange = 3.2f;
    [SerializeField] private float lightIntensityMin = 0.6f;
    [SerializeField] private float lightIntensityMax = 2.2f;
    [SerializeField] private float lightPulseSpeed = 2f;

    [Header("Emission pulse (URP Lit / materials with Emission enabled)")]
    [SerializeField] private bool pulseEmission = true;
    [SerializeField] private Color emissionColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private float emissionDim = 0.35f;
    [SerializeField] private float emissionBright = 2.5f;
    [SerializeField] private float emissionPulseSpeed = 2.2f;

    [Header("Optional size pulse")]
    [SerializeField] private bool pulseScale;
    [SerializeField] private float scalePulseAmount = 0.04f;
    [SerializeField] private float scalePulseSpeed = 2f;

    private Light _light;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private Vector3 _baseScale;
    private float _nextTickAt;

    [Header("Performance")]
    [Tooltip("Higher = cheaper. 0 = every frame.")]
    [SerializeField] private float tickIntervalSeconds = 0.08f;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        _baseScale = transform.localScale;
        if (useLight)
            EnsureLight();

        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void EnsurePropertyBlock()
    {
        if (_mpb == null)
            _mpb = new MaterialPropertyBlock();
    }

    private void EnsureLight()
    {
        Transform child = transform.Find("CollectibleGlowLight");
        GameObject go;
        if (child != null)
            go = child.gameObject;
        else
        {
            go = new GameObject("CollectibleGlowLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.15f;
        }

        _light = go.GetComponent<Light>();
        if (_light == null)
            _light = go.AddComponent<Light>();

        _light.type = LightType.Point;
        _light.color = lightColor;
        _light.range = lightRange;
        _light.shadows = LightShadows.None;
        _light.renderMode = LightRenderMode.Auto;
    }

    private void Update()
    {
        if (tickIntervalSeconds > 0f && Time.unscaledTime < _nextTickAt)
            return;
        _nextTickAt = Time.unscaledTime + Mathf.Max(0.02f, tickIntervalSeconds);

        float t = Time.unscaledTime;

        if (useLight && _light != null)
        {
            float n = 0.5f + 0.5f * Mathf.Sin(t * lightPulseSpeed);
            _light.intensity = Mathf.Lerp(lightIntensityMin, lightIntensityMax, n);
            _light.color = lightColor;
            _light.range = lightRange;
        }

        if (pulseEmission && _renderers != null && _renderers.Length > 0)
        {
            EnsurePropertyBlock();
            float n = 0.5f + 0.5f * Mathf.Sin(t * emissionPulseSpeed);
            float mul = Mathf.Lerp(emissionDim, emissionBright, n);
            Color c = emissionColor * mul;

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                if (!r.enabled) continue;
                // Avoid GetPropertyBlock (alloc/overhead). We overwrite the block fully.
                _mpb.SetColor(EmissionColorId, c);
                r.SetPropertyBlock(_mpb);
            }
        }

        if (pulseScale && scalePulseAmount > 0f)
        {
            float n = 0.5f + 0.5f * Mathf.Sin(t * scalePulseSpeed);
            float s = 1f + Mathf.Lerp(-scalePulseAmount, scalePulseAmount, n);
            transform.localScale = _baseScale * s;
        }
    }

    private void OnDisable()
    {
        if (_light != null)
            _light.enabled = false;
    }

    private void OnEnable()
    {
        if (_light != null)
            _light.enabled = true;
    }
}
