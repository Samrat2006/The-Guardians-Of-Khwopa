using UnityEngine;

/// <summary>Single white pixel sprite for runtime UI Images.</summary>
public static class UiSpriteUtility
{
    static Sprite s_white;

    public static Sprite WhiteSprite()
    {
        if (s_white != null) return s_white;

        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.name = "UIWhitePixel";
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixel(0, 0, Color.white);
        tex.Apply(false, true);

        s_white = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
        return s_white;
    }
}
