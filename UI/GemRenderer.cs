using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using SameGame.Model;
using System.Numerics;
using Windows.UI;

namespace SameGame.UI;

/// <summary>
/// Bejeweled-style faceted gemstones with optional Y-axis spin animation.
/// </summary>
internal static class GemRenderer
{
    /// <summary>
    /// Draws a faceted gemstone tile with optional Y-axis spin animation.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the cell in pixels.</param>
    /// <param name="y">The top edge of the cell in pixels.</param>
    /// <param name="size">The width and height of the cell in pixels.</param>
    /// <param name="baseColor">The base color of the gem.</param>
    /// <param name="colorIndex">The palette index that selects the gem cut shape.</param>
    /// <param name="highlighted">Whether to draw a selection overlay.</param>
    /// <param name="spinDegrees">The Y-axis spin angle in degrees.</param>
    public static void Draw(
        CanvasDrawingSession ds, float x, float y, float size, Color baseColor,
        int colorIndex, bool highlighted, float spinDegrees)
    {
        float pad = Math.Max(2f, size / 14f);
        float cx = x + size / 2f;
        float cy = y + size / 2f + size * 0.02f;
        float s = size / 2f - pad;

        // Normalize spin to [0, 360) for consistent lighting.
        float spinNorm = spinDegrees % 360f;
        if (spinNorm < 0f)
        {
            spinNorm += 360f;
        }

        // Drop shadow when facing the viewer (front face).
        if (spinNorm < 0.5f || spinNorm > 359.5f)
        {
            ds.FillEllipse(cx + 1, cy - s * 0.35f + 2, s * 0.82f, s * 0.28f, Color.FromArgb(60, 0, 0, 0));
        }

        // Compute spin transform and lighting direction.
        float spinRad = spinNorm * MathF.PI / 180f;
        float cos = MathF.Cos(spinRad);
        float sin = MathF.Sin(spinRad);
        float scaleX = Math.Max(0.1f, MathF.Abs(cos));
        float scaleY = 0.88f + 0.12f * scaleX;
        float frontWeight = Smoothstep((cos + 1f) * 0.5f);
        float lightX = -0.55f + sin * 0.65f;
        float lightY = -0.65f;

        var oldTransform = ds.Transform;
        ds.Transform = Matrix3x2.Multiply(
            Matrix3x2.CreateScale(scaleX, scaleY, new Vector2(cx, cy)),
            oldTransform);

        // Draw the gem cut matching the color index.
        switch (colorIndex % 6)
        {
            case 0: DrawShieldBrilliant(ds, cx, cy, s, baseColor, lightX, lightY, frontWeight); break;
            case 1: DrawSquareCushion(ds, cx, cy, s, baseColor, lightX, lightY, frontWeight); break;
            case 2: DrawTriangleCut(ds, cx, cy, s, baseColor, lightX, lightY, frontWeight); break;
            case 3: DrawDiamondBrilliant(ds, cx, cy, s, baseColor, lightX, lightY, frontWeight); break;
            case 4: DrawRoundBrilliant(ds, cx, cy, s, baseColor, lightX, lightY, frontWeight); break;
            default: DrawHexCut(ds, cx, cy, s, baseColor, lightX, lightY, frontWeight); break;
        }

        DrawSpecular(ds, cx, cy, s, sin, frontWeight);
        ds.Transform = oldTransform;

        if (highlighted)
        {
            ds.FillRoundedRectangle(x + 1, y + 1, size - 2, size - 2, 4, 4, Color.FromArgb(75, 255, 255, 255));
        }
    }

    /// <summary>
    /// Draws a shield-brilliant cut gem with crown and pavilion facets.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="cx">The horizontal center of the gem in pixels.</param>
    /// <param name="cy">The vertical center of the gem in pixels.</param>
    /// <param name="s">The half-size scale factor of the gem.</param>
    /// <param name="baseColor">The base color of the gem.</param>
    /// <param name="lightX">The normalized horizontal light direction.</param>
    /// <param name="lightY">The normalized vertical light direction.</param>
    /// <param name="frontWeight">The front-face lighting weight (0–1).</param>
    private static void DrawShieldBrilliant(
        CanvasDrawingSession ds, float cx, float cy, float s, Color baseColor,
        float lightX, float lightY, float frontWeight)
    {
        var crown = new Vector2[]
        {
            new(cx, cy - s * 0.88f),
            new(cx + s * 0.72f, cy - s * 0.2f),
            new(cx + s * 0.55f, cy + s * 0.35f),
            new(cx, cy + s * 0.55f),
            new(cx - s * 0.55f, cy + s * 0.35f),
            new(cx - s * 0.72f, cy - s * 0.2f)
        };
        FillFacets(ds, crown, baseColor, lightX, lightY, frontWeight, cx, cy);
        var pavilion = new Vector2[]
        {
            new(cx - s * 0.55f, cy + s * 0.35f),
            new(cx + s * 0.55f, cy + s * 0.35f),
            new(cx, cy + s * 0.92f)
        };
        FillFacets(ds, pavilion, baseColor, lightX, lightY + 0.3f, frontWeight * 0.85f, cx, cy);
    }

    /// <summary>
    /// Draws a square cushion cut gem.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="cx">The horizontal center of the gem in pixels.</param>
    /// <param name="cy">The vertical center of the gem in pixels.</param>
    /// <param name="s">The half-size scale factor of the gem.</param>
    /// <param name="baseColor">The base color of the gem.</param>
    /// <param name="lightX">The normalized horizontal light direction.</param>
    /// <param name="lightY">The normalized vertical light direction.</param>
    /// <param name="frontWeight">The front-face lighting weight (0–1).</param>
    private static void DrawSquareCushion(
        CanvasDrawingSession ds, float cx, float cy, float s, Color baseColor,
        float lightX, float lightY, float frontWeight)
    {
        float r = s * 0.62f;
        var points = new Vector2[]
        {
            new(cx - r, cy - r * 0.75f),
            new(cx - r * 0.75f, cy - r),
            new(cx + r * 0.75f, cy - r),
            new(cx + r, cy - r * 0.75f),
            new(cx + r, cy + r * 0.75f),
            new(cx + r * 0.75f, cy + r),
            new(cx - r * 0.75f, cy + r),
            new(cx - r, cy + r * 0.75f)
        };
        FillFacets(ds, points, baseColor, lightX, lightY, frontWeight, cx, cy);
    }

    /// <summary>
    /// Draws a triangle cut gem with an inner table facet.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="cx">The horizontal center of the gem in pixels.</param>
    /// <param name="cy">The vertical center of the gem in pixels.</param>
    /// <param name="s">The half-size scale factor of the gem.</param>
    /// <param name="baseColor">The base color of the gem.</param>
    /// <param name="lightX">The normalized horizontal light direction.</param>
    /// <param name="lightY">The normalized vertical light direction.</param>
    /// <param name="frontWeight">The front-face lighting weight (0–1).</param>
    private static void DrawTriangleCut(
        CanvasDrawingSession ds, float cx, float cy, float s, Color baseColor,
        float lightX, float lightY, float frontWeight)
    {
        var outer = new Vector2[]
        {
            new(cx, cy - s * 0.9f),
            new(cx + s * 0.82f, cy + s * 0.65f),
            new(cx - s * 0.82f, cy + s * 0.65f)
        };
        FillFacets(ds, outer, baseColor, lightX, lightY, frontWeight, cx, cy);
        var inner = new Vector2[]
        {
            new(cx, cy - s * 0.35f),
            new(cx + s * 0.38f, cy + s * 0.28f),
            new(cx - s * 0.38f, cy + s * 0.28f)
        };
        FillFacet(ds, inner, FacetColor(baseColor, lightX, lightY, frontWeight, 0.35f, cx, cy));
    }

    /// <summary>
    /// Draws a diamond brilliant cut gem with crown and pavilion.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="cx">The horizontal center of the gem in pixels.</param>
    /// <param name="cy">The vertical center of the gem in pixels.</param>
    /// <param name="s">The half-size scale factor of the gem.</param>
    /// <param name="baseColor">The base color of the gem.</param>
    /// <param name="lightX">The normalized horizontal light direction.</param>
    /// <param name="lightY">The normalized vertical light direction.</param>
    /// <param name="frontWeight">The front-face lighting weight (0–1).</param>
    private static void DrawDiamondBrilliant(
        CanvasDrawingSession ds, float cx, float cy, float s, Color baseColor,
        float lightX, float lightY, float frontWeight)
    {
        var top = new Vector2[]
        {
            new(cx, cy - s * 0.85f),
            new(cx + s * 0.55f, cy - s * 0.15f),
            new(cx, cy + s * 0.05f),
            new(cx - s * 0.55f, cy - s * 0.15f)
        };
        FillFacets(ds, top, baseColor, lightX, lightY, frontWeight, cx, cy);
        var bottom = new Vector2[]
        {
            new(cx - s * 0.55f, cy - s * 0.15f),
            new(cx + s * 0.55f, cy - s * 0.15f),
            new(cx, cy + s * 0.88f)
        };
        FillFacets(ds, bottom, baseColor, lightX, lightY + 0.25f, frontWeight * 0.9f, cx, cy);
    }

    /// <summary>
    /// Draws a round brilliant cut gem with a table facet.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="cx">The horizontal center of the gem in pixels.</param>
    /// <param name="cy">The vertical center of the gem in pixels.</param>
    /// <param name="s">The half-size scale factor of the gem.</param>
    /// <param name="baseColor">The base color of the gem.</param>
    /// <param name="lightX">The normalized horizontal light direction.</param>
    /// <param name="lightY">The normalized vertical light direction.</param>
    /// <param name="frontWeight">The front-face lighting weight (0–1).</param>
    private static void DrawRoundBrilliant(
        CanvasDrawingSession ds, float cx, float cy, float s, Color baseColor,
        float lightX, float lightY, float frontWeight)
    {
        int segments = 12;
        var points = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            double angle = Math.PI / 2 + i * 2 * Math.PI / segments;
            points[i] = new Vector2(cx + s * 0.78f * (float)Math.Cos(angle), cy - s * 0.78f * (float)Math.Sin(angle));
        }

        FillFacets(ds, points, baseColor, lightX, lightY, frontWeight, cx, cy);
        ds.FillEllipse(cx, cy, s * 0.28f, s * 0.28f, FacetColor(baseColor, lightX, lightY, frontWeight, 0.5f, cx, cy));
    }

    /// <summary>
    /// Draws a hexagonal cut gem.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="cx">The horizontal center of the gem in pixels.</param>
    /// <param name="cy">The vertical center of the gem in pixels.</param>
    /// <param name="s">The half-size scale factor of the gem.</param>
    /// <param name="baseColor">The base color of the gem.</param>
    /// <param name="lightX">The normalized horizontal light direction.</param>
    /// <param name="lightY">The normalized vertical light direction.</param>
    /// <param name="frontWeight">The front-face lighting weight (0–1).</param>
    private static void DrawHexCut(
        CanvasDrawingSession ds, float cx, float cy, float s, Color baseColor,
        float lightX, float lightY, float frontWeight)
    {
        var points = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 2 + i * Math.PI / 3;
            points[i] = new Vector2(cx + s * 0.8f * (float)Math.Cos(angle), cy - s * 0.8f * (float)Math.Sin(angle));
        }

        FillFacets(ds, points, baseColor, lightX, lightY, frontWeight, cx, cy);
    }

    /// <summary>
    /// Fills triangular facets radiating from the center of a polygon outline.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="points">The outer vertices of the gem outline.</param>
    /// <param name="baseColor">The base color of the gem.</param>
    /// <param name="lightX">The normalized horizontal light direction.</param>
    /// <param name="lightY">The normalized vertical light direction.</param>
    /// <param name="frontWeight">The front-face lighting weight (0–1).</param>
    /// <param name="cx">The horizontal center used as the facet hub.</param>
    /// <param name="cy">The vertical center used as the facet hub.</param>
    private static void FillFacets(
        CanvasDrawingSession ds, Vector2[] points, Color baseColor,
        float lightX, float lightY, float frontWeight, float cx, float cy)
    {
        for (int i = 0; i < points.Length; i++)
        {
            int next = (i + 1) % points.Length;
            var facet = new[] { points[i], points[next], new Vector2(cx, cy) };
            float t = (i + 1f) / points.Length;
            FillFacet(ds, facet, FacetColor(baseColor, lightX, lightY, frontWeight, t, cx, cy));
        }
    }

    /// <summary>
    /// Fills and outlines a single triangular facet polygon.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="points">The three vertices of the facet.</param>
    /// <param name="color">The fill color of the facet.</param>
    private static void FillFacet(CanvasDrawingSession ds, Vector2[] points, Color color)
    {
        using var geom = CanvasGeometry.CreatePolygon(ds, points);
        ds.FillGeometry(geom, color);
        ds.DrawGeometry(geom, Blend(color, Color.FromArgb(255, 255, 255, 255), 0.15f), 0.5f);
    }

    /// <summary>
    /// Draws a specular highlight ellipse on the gem surface.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="cx">The horizontal center of the gem in pixels.</param>
    /// <param name="cy">The vertical center of the gem in pixels.</param>
    /// <param name="s">The half-size scale factor of the gem.</param>
    /// <param name="sin">The sine of the spin angle, used to shift the highlight.</param>
    /// <param name="frontWeight">The front-face lighting weight (0–1).</param>
    private static void DrawSpecular(CanvasDrawingSession ds, float cx, float cy, float s, float sin, float frontWeight)
    {
        float alpha = 80f + frontWeight * 120f;
        float sx = cx - s * 0.22f + sin * s * 0.12f;
        float sy = cy - s * 0.38f;
        ds.FillEllipse(sx, sy, s * 0.18f, s * 0.1f, Color.FromArgb((byte)alpha, 255, 255, 255));
    }

    /// <summary>
    /// Computes the shaded color for a single facet based on lighting and position.
    /// </summary>
    /// <param name="baseColor">The base color of the gem.</param>
    /// <param name="lightX">The normalized horizontal light direction.</param>
    /// <param name="lightY">The normalized vertical light direction.</param>
    /// <param name="frontWeight">The front-face lighting weight (0–1).</param>
    /// <param name="facetT">The facet position factor along the outline (0–1).</param>
    /// <param name="cx">The horizontal center of the gem.</param>
    /// <param name="cy">The vertical center of the gem.</param>
    /// <returns>The computed facet fill color.</returns>
    private static Color FacetColor(
        Color baseColor, float lightX, float lightY, float frontWeight, float facetT, float cx, float cy)
    {
        float shade = 0.35f + facetT * 0.4f - lightX * 0.15f - lightY * 0.1f;
        shade = Math.Clamp(shade, 0f, 1f);
        float highlight = frontWeight * (1f - Math.Abs(facetT - 0.3f));
        return Blend(
            Blend(baseColor, Color.FromArgb(255, 0, 0, 0), shade * 0.45f),
            Color.FromArgb(255, 255, 255, 255),
            highlight * 0.35f);
    }

    /// <summary>
    /// Applies a smooth Hermite interpolation to <paramref name="t"/>.
    /// </summary>
    /// <param name="t">The input value (typically 0–1).</param>
    /// <returns>The smoothstep-interpolated value.</returns>
    private static float Smoothstep(float t) => t * t * (3f - 2f * t);

    /// <summary>
    /// Linearly interpolates between two colors in RGB space.
    /// </summary>
    /// <param name="a">The start color.</param>
    /// <param name="b">The end color.</param>
    /// <param name="t">The blend factor (0 = <paramref name="a"/>, 1 = <paramref name="b"/>).</param>
    /// <returns>The blended color with full opacity.</returns>
    private static Color Blend(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(
            255,
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }
}
