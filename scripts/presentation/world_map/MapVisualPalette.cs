using Godot;
using SpaceFactory.Core.World.Asteroids;
using SpaceFactory.Core.World.Exploration;

namespace SpaceFactory.Presentation.WorldMap;

internal static class MapVisualPalette
{
    public static readonly Color Cyan = new(0.12f, 0.72f, 0.95f);
    public static readonly Color CyanMuted = new(0.08f, 0.36f, 0.48f);
    public static readonly Color TextPrimary = new(0.86f, 0.94f, 0.98f);
    public static readonly Color TextMuted = new(0.46f, 0.61f, 0.68f);
    public static readonly Color MapBackground = new(0.008f, 0.018f, 0.027f, 0.97f);

    public static Color GetCometColor(AsteroidSize? size) => size switch
    {
        AsteroidSize.Tiny => new Color(0.40f, 0.43f, 0.44f),
        AsteroidSize.Small => new Color(0.47f, 0.49f, 0.48f),
        AsteroidSize.Medium => new Color(0.55f, 0.55f, 0.52f),
        AsteroidSize.Large => new Color(0.60f, 0.57f, 0.51f),
        AsteroidSize.Huge => new Color(0.67f, 0.61f, 0.51f),
        _ => new Color(0.48f, 0.50f, 0.51f),
    };

    public static float GetMinimumMarkerRadius(AsteroidSize? size) => size switch
    {
        AsteroidSize.Tiny => 2.5f,
        AsteroidSize.Small => 3.5f,
        AsteroidSize.Medium => 5.0f,
        AsteroidSize.Large => 7.0f,
        AsteroidSize.Huge => 10.0f,
        _ => 3.0f,
    };

    public static Vector2[] CreateCometPolygon(
        DiscoveredCometData item,
        Vector2 center,
        float radius,
        int pointCount = 10)
    {
        var random = new RandomNumberGenerator { Seed = item.VisualSeed ^ 0x4D4150564953554CUL };
        var points = new Vector2[pointCount];
        var phase = random.RandfRange(0, Mathf.Tau);
        var aspect = random.RandfRange(0.72f, 1.0f);
        for (var index = 0; index < pointCount; index++)
        {
            var angle = phase + (Mathf.Tau * index / pointCount);
            var unevenRadius = radius * random.RandfRange(0.76f, 1.13f);
            var point = Vector2.FromAngle(angle) * unevenRadius;
            point.Y *= aspect;
            points[index] = center + point;
        }

        return points;
    }

    public static string GetCometDisplayName(AsteroidSize size) => size switch
    {
        AsteroidSize.Tiny => "Kleiner Komet",
        AsteroidSize.Small => "Kleiner Komet",
        AsteroidSize.Medium => "Mittlerer Komet",
        AsteroidSize.Large => "Großer Komet",
        AsteroidSize.Huge => "Riesenkomet",
        _ => "Komet",
    };

    public static Color GetResourceColor(string resourceId)
    {
        uint hash = 2_166_136_261;
        foreach (var character in resourceId)
        {
            hash ^= character;
            hash *= 16_777_619;
        }

        var hue = (hash % 360) / 360.0f;
        return Color.FromHsv(hue, 0.54f, 0.94f);
    }

    public static Color ParseColor(string colorHex, Color fallback)
    {
        try
        {
            return Color.FromHtml(colorHex);
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }
}
