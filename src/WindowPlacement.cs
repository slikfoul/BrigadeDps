namespace BrigadeDps;

// Coordinates are measured from the left/top, independent of Unity's Y direction.
public readonly record struct WindowAnchorAxis(bool FarEdge, float MarginFraction)
{
    public static WindowAnchorAxis Capture(float position, float size, float screen, float padding, bool far)
    {
        position = Clamp(position, size, screen, padding);
        float margin = far ? screen - size - position : position;
        return new WindowAnchorAxis(far, screen > 0 ? Math.Clamp(margin / screen, 0, 1) : 0);
    }

    public float Resolve(float size, float screen, float padding)
    {
        float margin = MarginFraction * screen;
        return Clamp(FarEdge ? screen - size - margin : margin, size, screen, padding);
    }

    public static float Clamp(float position, float size, float screen, float padding) =>
        Math.Clamp(position, padding, Math.Max(padding, screen - size - padding));
}
