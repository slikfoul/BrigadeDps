using System;

namespace BrigadeDps
{
    // Original nine-slice ornament: stone silhouette, luminous rails and corner loops.
    // No game artwork is embedded, and the top edge has no emblem or opening.
    public static class FrameArtwork
    {
        public const int Size = 192, Slice = 48;

        public static uint[] CreatePixels()
        {
            var pixels = new uint[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                double r = 0, g = 0, b = 0, a = 0;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    double u = Math.Min(x + (sx + .5) / 2, Size - x - (sx + .5) / 2);
                    double v = Math.Min(y + (sy + .5) / 2, Size - y - (sy + .5) / 2);
                    double edge = Math.Min(Math.Min(u - 2, v - 2), (u + v - 13) * .7071);
                    if ((u > 35 && u < 42 && v < 6) || (v > 35 && v < 42 && u < 6)) edge -= 3;
                    if (edge < 0 || Math.Min(u, v) > 34) continue;
                    double tone = edge < 3 ? .16 : edge < 8 ? .27 : .19;
                    // A thin bevel and irregular dark chips add depth without noisy stretch seams.
                    if (u + v < 26 && edge > 3 && edge < 7) tone = .34;
                    if (u > 38 && u < 43 && v > 26 || v > 38 && v < 43 && u > 26) tone *= .7;
                    double distance = Math.Abs(Math.Sqrt((u - 22) * (u - 22) + (v - 22) * (v - 22)) - 7);
                    if (u >= 30) distance = Math.Min(distance, Math.Min(Math.Abs(v - 14), Math.Abs(v - 23)));
                    if (v >= 30) distance = Math.Min(distance, Math.Min(Math.Abs(u - 14), Math.Abs(u - 23)));
                    distance = Math.Min(distance, Segment(u, v, 22, 15, 34, 14));
                    distance = Math.Min(distance, Segment(u, v, 15, 22, 14, 34));
                    distance = Math.Min(distance, Segment(u, v, 29, 22, 34, 23));
                    distance = Math.Min(distance, Segment(u, v, 22, 29, 23, 34));
                    // Small angular flourishes outside and inside each circular corner.
                    distance = Math.Min(distance, Segment(u, v, 9, 22, 10, 10));
                    distance = Math.Min(distance, Segment(u, v, 10, 10, 22, 9));
                    distance = Math.Min(distance, Segment(u, v, 10, 17, 17, 10));
                    distance = Math.Min(distance, Segment(u, v, 30, 34, 34, 30));
                    distance = Math.Min(distance, Segment(u, v, 34, 30, 34, 34));
                    distance = Math.Min(distance, Segment(u, v, 34, 34, 30, 34));
                    double glow = .30 * Math.Exp(-distance * distance / 7);
                    double light = Math.Clamp(1.25 - distance, 0, 1);
                    r += Math.Min(1, (tone + glow) * (1 - light) + .98 * light);
                    g += Math.Min(1, (tone * .97 + glow) * (1 - light) + .98 * light);
                    b += Math.Min(1, (tone * 1.13 + glow) * (1 - light) + 1 * light);
                    a++;
                }
                if (a == 0) continue;
                pixels[y * Size + x] = ((uint)Math.Round(a / 4 * 255) << 24)
                    | ((uint)Math.Round(r / a * 255) << 16)
                    | ((uint)Math.Round(g / a * 255) << 8) | (uint)Math.Round(b / a * 255);
            }
            return pixels;
        }

        private static double Segment(double x, double y, double ax, double ay, double bx, double by)
        {
            double dx = bx - ax, dy = by - ay;
            double t = Math.Clamp(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy), 0, 1);
            dx = x - ax - t * dx; dy = y - ay - t * dy;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
