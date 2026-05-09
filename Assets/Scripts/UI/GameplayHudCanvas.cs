using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared overlay canvas for runtime-built gameplay HUD (health, booster, etc.).</summary>
public static class GameplayHudCanvas
{
    public const string CanvasObjectName = "GameplayHUD";

    /// <summary>Bottom HUD stack: booster sits at <see cref="HudStackBottomPadding"/>; health sits above it (reference res 1080p).</summary>
    public const float HudStackBottomPadding = 32f;
    public const float HudStackPanelHeight = 78f;
    public const float HudStackGap = 12f;

    /// <summary>Set by <see cref="PlayerSuperMeterUI"/> before HUD builds. Default: booster at bottom, health above.</summary>
    public static bool BoosterBelowHealthBar = true;

    public static float HealthPanelBottomFromScreen()
    {
        return BoosterBelowHealthBar
            ? HudStackBottomPadding + HudStackPanelHeight + HudStackGap
            : HudStackBottomPadding;
    }

    public static float BoosterPanelBottomFromScreen(float extraOffset = 0f)
    {
        float y = BoosterBelowHealthBar
            ? HudStackBottomPadding
            : HudStackBottomPadding + HudStackPanelHeight + HudStackGap;
        return y + extraOffset;
    }

    public static Canvas GetOrCreate()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name == CanvasObjectName)
            {
                if (c.sortingOrder < 900)
                    c.sortingOrder = 900;
                return c;
            }
        }

        GameObject go = new GameObject(CanvasObjectName);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }
}
