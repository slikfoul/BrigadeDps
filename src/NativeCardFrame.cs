using UnityEngine;
using UnityEngine.UI;

namespace BrigadeDps;

// Draw the game's actual card atlas. The upper row samples the lower ornament
// with reversed V coordinates, so no top emblem, arrow or gap is introduced.
internal sealed class NativeCardFrame
{
    private readonly RectTransform parent, root;
    private readonly Sprite stone, lines;
    private readonly RawImage[] stonePieces, linePieces;
    private float lastWidth = -1, lastHeight = -1, lastOpacity = -1;
    internal NativeCardFrame(RectTransform parent, Sprite stone, Sprite lines)
    {
        this.parent = parent; this.stone = stone; this.lines = lines;
        root = new GameObject("NativeCardFrame").AddComponent<RectTransform>();
        root.SetParent(parent, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0, 1);
        root.anchoredPosition = Vector2.zero;
        root.SetAsFirstSibling();
        stonePieces = CreatePieces(stone.texture, true);
        linePieces = CreatePieces(lines.texture, false);
    }

    private RawImage[] CreatePieces(Texture texture, bool center)
    {
        var pieces = new RawImage[9];
        for (int i = 0; i < 9; i++)
        {
            if (!center && i == 4) continue;
            var rt = new GameObject("CardSlice" + i).AddComponent<RectTransform>();
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            var image = rt.gameObject.AddComponent<RawImage>();
            image.texture = texture; image.raycastTarget = false;
            pieces[i] = image;
        }
        return pieces;
    }

    internal void Layout(float opacity)
    {
        float width = parent.rect.width, height = parent.rect.height;
        if (lastWidth != width || lastHeight != height)
        {
            // Preserve the source corner proportions, including on a very short meter.
            float divisor = Mathf.Max(5, 560 / Mathf.Max(1, height));
            var stoneRect = stone.textureRect;
            var lineRect = lines.textureRect;
            float insetX = (stoneRect.width - lineRect.width) / (2 * divisor);
            float insetY = (stoneRect.height - lineRect.height) / (2 * divisor);
            LayoutLayer(stonePieces, stone, -5, -5, width + 10, height + 10, divisor);
            LayoutLayer(linePieces, lines, -5 + insetX, -5 + insetY,
                width + 10 - 2 * insetX, height + 10 - 2 * insetY, divisor);
            lastWidth = width; lastHeight = height;
        }
        if (lastOpacity == opacity) return;
        foreach (var image in stonePieces) image.color = new Color(1, 1, 1, opacity);
        // This is the subdued, non-glowing card layer from the game.
        foreach (var image in linePieces) if (image != null) image.color = Color.white;
        lastOpacity = opacity;
    }

    private static void LayoutLayer(RawImage[] pieces, Sprite sprite, float left, float top, float width, float height, float divisor)
    {
        var source = sprite.textureRect;
        const float side = 100, cap = 280;
        float dx = side / divisor, dy = cap / divisor;
        float[] xs = { left, left + dx, left + width - dx, left + width };
        float[] ys = { top, top + dy, top + height - dy, top + height };
        float[] us = { source.x, source.x + side, source.xMax - side, source.xMax };
        for (int row = 0; row < 3; row++)
        for (int column = 0; column < 3; column++)
        {
            var image = pieces[row * 3 + column];
            if (image == null) continue;
            image.rectTransform.anchoredPosition = new Vector2(xs[column], -ys[row]);
            image.rectTransform.sizeDelta = new Vector2(xs[column + 1] - xs[column], ys[row + 1] - ys[row]);
            float v = row == 0 ? source.y + cap : row == 1 ? source.y + cap : source.y;
            // Continue the exact edge at the cap boundary; the original upper
            // half has different bends and must not leak into this mirrored frame.
            float vh = row == 0 ? -cap : row == 1 ? 1 : cap;
            image.uvRect = new Rect(us[column] / sprite.texture.width, v / sprite.texture.height,
                (us[column + 1] - us[column]) / sprite.texture.width, vh / sprite.texture.height);
        }
    }
}
