using UnityEngine;
using UnityEngine.UI;

/// <summary>World-space health bar above an enemy; faces the main camera.</summary>
[DisallowMultipleComponent]
public class EnemyHealthBar : MonoBehaviour
{
    const float DefaultHeightOffset = 2.15f;

    Enemy enemy;
    Image fillImage;
    [SerializeField] float heightOffset = DefaultHeightOffset;
    private static Camera s_mainCam;
    private static float s_nextCamRefreshAt;

    public static void EnsureForEnemy(Enemy host)
    {
        if (host == null) return;
        if (host.GetComponentInChildren<EnemyHealthBar>(true) != null) return;

        GameObject root = new GameObject("EnemyHealthBar");
        root.transform.SetParent(host.transform, false);
        var bar = root.AddComponent<EnemyHealthBar>();
        bar.Build(host);
    }

    void Build(Enemy e)
    {
        enemy = e;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500;

        // Canvas replaces Transform with RectTransform — do not AddComponent<RectTransform> again (null / error).
        RectTransform rt = gameObject.GetComponent<RectTransform>();
        if (rt == null)
        {
            Debug.LogError("EnemyHealthBar: RectTransform missing after Canvas.");
            Object.Destroy(gameObject);
            return;
        }

        rt.sizeDelta = new Vector2(160f, 16f);
        transform.localScale = Vector3.one * 0.0075f;

        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(transform, false);
        var bg = bgGo.AddComponent<Image>();
        bg.sprite = UiSpriteUtility.WhiteSprite();
        bg.color = new Color(0.08f, 0.08f, 0.1f, 0.92f);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(transform, false);
        fillImage = fillGo.AddComponent<Image>();
        fillImage.sprite = UiSpriteUtility.WhiteSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.color = new Color(0.35f, 0.82f, 0.4f, 1f);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
    }

    void LateUpdate()
    {
        if (enemy == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (enemy.IsEnemyDead)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        transform.position = enemy.transform.position + Vector3.up * heightOffset;

        Camera cam = GetMainCamera();
        if (cam != null)
            transform.rotation = cam.transform.rotation;

        float n = enemy.HealthNormalized;
        if (fillImage != null)
        {
            fillImage.fillAmount = n;
            fillImage.color = Color.Lerp(new Color(0.92f, 0.22f, 0.2f, 1f), new Color(0.32f, 0.82f, 0.38f, 1f), n);
        }
    }

    private static Camera GetMainCamera()
    {
        // Camera.main is expensive; refresh occasionally in case main camera changes.
        if (s_mainCam == null || Time.unscaledTime >= s_nextCamRefreshAt)
        {
            s_nextCamRefreshAt = Time.unscaledTime + 1.0f;
            s_mainCam = Camera.main;
        }
        return s_mainCam;
    }
}
