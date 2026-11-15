using UnityEngine;
using UnityEngine.UI;

namespace BrigadeDps;

// Reuse only the line ornament, without a second stone rim or opaque backing.
// The enclosing window supplies one continuous frame and background.
// A separate full-width mask lets the ornament reach the outer frame while
// remaining clipped vertically with the scrolling table.
internal sealed class NativeCardDivider
{
    internal const float Height = 24;
    private readonly RectTransform clip, root;
    private readonly Sprite stone, lines;
    private readonly RawImage[] linePieces;
    private readonly Texture2D curlTexture;
    private readonly RawImage leftCurl, rightCurl;
    private float lastWidth = -1;

    internal NativeCardDivider(RectTransform parent, Sprite stone, Sprite lines)
    {
        this.stone = stone; this.lines = lines;
        clip = Rect("BossDividerClip", parent);
        clip.gameObject.AddComponent<RectMask2D>();
        root = Rect("BossDivider", clip);
        linePieces = Pieces(lines.texture);
        // The card atlas ends at the lower edge and clips the last part of each
        // outside curl. Continue that same 30px-radius arc below the atlas edge.
        curlTexture = CreateCurlCompletion();
        leftCurl = Rect("LeftCurlCompletion", root).gameObject.AddComponent<RawImage>();
        rightCurl = Rect("RightCurlCompletion", root).gameObject.AddComponent<RawImage>();
        leftCurl.texture = rightCurl.texture = curlTexture;
        leftCurl.raycastTarget = rightCurl.raycastTarget = false;
        rightCurl.uvRect = new Rect(1, 0, -1, 1);
        clip.gameObject.SetActive(false);
    }

    private static RectTransform Rect(string name, RectTransform parent)
    {
        var rt = new GameObject(name).AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        return rt;
    }

    private RawImage[] Pieces(Texture texture)
    {
        var result = new RawImage[3];
        for (int i = 0; i < result.Length; i++)
        {
            var image = Rect("RimSlice" + i, root).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            result[i] = image;
        }
        return result;
    }

    internal void Place(bool visible, float width, float viewportHeight, float padding, float contentY, float scrollY)
    {
        clip.gameObject.SetActive(visible);
        if (!visible) return;
        clip.anchoredPosition = new Vector2(-5, -padding);
        clip.sizeDelta = new Vector2(width + 10, viewportHeight);
        root.anchoredPosition = new Vector2(0, scrollY - contentY);
        root.sizeDelta = new Vector2(width + 10, Height);
        if (lastWidth != width)
        {
            float inset = (stone.textureRect.width - lines.textureRect.width) / 10;
            Layout(linePieces, lines, inset, 1, width + 10 - 2 * inset);
            leftCurl.rectTransform.anchoredPosition = new Vector2(inset + 14f / 5, -16.6f);
            rightCurl.rectTransform.anchoredPosition = new Vector2(width + 10 - inset - 58f / 5, -16.6f);
            leftCurl.rectTransform.sizeDelta = rightCurl.rectTransform.sizeDelta = new Vector2(44f / 5, 12f / 5);
            lastWidth = width;
        }
    }

    internal void DestroyArtwork() => UnityEngine.Object.Destroy(curlTexture);

    private static Texture2D CreateCurlCompletion()
    {
        const int width = 88, height = 24;
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float px = 14 + (x + .5f) / 2, py = -10 + (y + .5f) / 2;
            float dx = px - 32, dy = py - 24;
            float distance = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - 30);
            float alpha = .72f * Mathf.Clamp01(1.15f - distance)
                * Mathf.Clamp01(dx + 13) * Mathf.Clamp01(.5f - py);
            pixels[y * width + x] = new Color(1, 1, 1, alpha);
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "BrigadeDps.CurlCompletion";
        texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(pixels); texture.Apply();
        return texture;
    }

    private static void Layout(RawImage[] pieces, Sprite sprite, float left, float top, float width)
    {
        var source = sprite.textureRect;
        float[] xs = { left, left + 20, left + width - 20, left + width };
        float[] us = { source.x, source.x + 100, source.xMax - 100, source.xMax };
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].rectTransform.anchoredPosition = new Vector2(xs[i], -top);
            pieces[i].rectTransform.sizeDelta = new Vector2(xs[i + 1] - xs[i], 16);
            pieces[i].uvRect = new Rect(us[i] / sprite.texture.width, source.y / sprite.texture.height,
                (us[i + 1] - us[i]) / sprite.texture.width, 80f / sprite.texture.height);
        }
    }
}
