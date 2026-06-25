using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using SameGame.Model;
using System.Numerics;
using Windows.UI;

namespace SameGame.UI;

/// <summary>
/// Renders board tiles for all game skins using Win2D.
/// </summary>
public static class TileRenderer
{
    private static readonly Color FrameLight = Color.FromArgb(255, 168, 168, 168);
    private static readonly Color FrameMid = Color.FromArgb(255, 112, 112, 112);
    private static readonly Color FrameDark = Color.FromArgb(255, 80, 80, 80);

    private static readonly Color[] BlockcraftBase =
    [
        Color.FromArgb(255, 31, 67, 140),
        Color.FromArgb(255, 142, 32, 32),
        Color.FromArgb(255, 36, 120, 56),
        Color.FromArgb(255, 198, 168, 36),
        Color.FromArgb(255, 92, 214, 224),
        Color.FromArgb(255, 134, 96, 67)
    ];

    /// <summary>
    /// Draws a single board cell using the skin-specific renderer selected in <paramref name="settings"/>.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the cell in pixels.</param>
    /// <param name="y">The top edge of the cell in pixels.</param>
    /// <param name="size">The width and height of the cell in pixels.</param>
    /// <param name="colorIndex">The palette index of the tile color.</param>
    /// <param name="settings">Game settings that supply skin and color information.</param>
    /// <param name="highlighted">Whether the cell is currently selected or highlighted.</param>
    /// <param name="spinDegrees">Optional Y-axis spin angle in degrees (used by the Gems skin).</param>
    public static void DrawCell(
        CanvasDrawingSession ds, float x, float y, float size, int colorIndex,
        GameSettings settings, bool highlighted, float spinDegrees = 0f)
    {
        var baseColor = settings.ColorAt(colorIndex);
        // Dispatch to the skin-specific drawing routine.
        switch (settings.SkinValue)
        {
            case GameSettings.Skin.Marbles:
                DrawMarbleCell(ds, x, y, size, baseColor, highlighted);
                break;
            case GameSettings.Skin.Classic:
                DrawLetterTile(ds, x, y, size, baseColor, colorIndex, highlighted);
                break;
            case GameSettings.Skin.Blockcraft:
                DrawBlockcraft(ds, x, y, size, baseColor, colorIndex, highlighted);
                break;
            case GameSettings.Skin.Bricks:
                DrawBrick(ds, x, y, size, baseColor, highlighted);
                break;
            case GameSettings.Skin.Shapes:
                DrawShapeTile(ds, x, y, size, baseColor, colorIndex, highlighted);
                break;
            case GameSettings.Skin.Gems:
                GemRenderer.Draw(ds, x, y, size, baseColor, colorIndex, highlighted, spinDegrees);
                break;
            default:
                DrawModernTile(ds, x, y, size, baseColor, colorIndex, highlighted);
                break;
        }
    }

    /// <summary>
    /// Draws a rounded gradient tile with a centered letter (Modern skin).
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the cell in pixels.</param>
    /// <param name="y">The top edge of the cell in pixels.</param>
    /// <param name="size">The width and height of the cell in pixels.</param>
    /// <param name="baseColor">The base fill color of the tile.</param>
    /// <param name="colorIndex">The palette index used to resolve the letter glyph.</param>
    /// <param name="highlighted">Whether to draw a selection outline.</param>
    private static void DrawModernTile(
        CanvasDrawingSession ds, float x, float y, float size, Color baseColor, int colorIndex, bool highlighted)
    {
        float pad = Math.Max(1, size * 0.06f);
        float left = x + pad;
        float top = y + pad;
        float w = size - pad * 2;
        float h = w;
        float radius = size * 0.18f;

        using var brush = new CanvasLinearGradientBrush(ds,
            Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.35f),
            Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.25f))
        {
            StartPoint = new Vector2(left, top),
            EndPoint = new Vector2(left, top + h)
        };
        ds.FillRoundedRectangle(left, top, w, h, radius, radius, brush);

        if (highlighted)
        {
            ds.DrawRoundedRectangle(left, top, w, h, radius, radius, Color.FromArgb(255, 255, 255, 255), Math.Max(1f, size * 0.06f));
        }

        DrawModernLetter(ds, new Windows.Foundation.Rect(left, top, w, h), size, colorIndex);
    }

    /// <summary>
    /// Draws the centered letter glyph on a Modern skin tile.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="rect">The inner bounds of the tile.</param>
    /// <param name="size">The outer cell size used to scale the font.</param>
    /// <param name="colorIndex">The palette index used to resolve the letter glyph.</param>
    private static void DrawModernLetter(CanvasDrawingSession ds, Windows.Foundation.Rect rect, float size, int colorIndex)
    {
        char letter = GameSettings.LetterForColorIndex(colorIndex);
        using var format = new CanvasTextFormat
        {
            FontSize = size * 0.42f,
            FontWeight = new global::Windows.UI.Text.FontWeight(700),
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center
        };
        ds.DrawText(letter.ToString(), rect, Color.FromArgb(255, 255, 255, 255), format);
    }

    /// <summary>
    /// Draws a beveled letter tile with a 3-D frame (Classic skin).
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the cell in pixels.</param>
    /// <param name="y">The top edge of the cell in pixels.</param>
    /// <param name="size">The width and height of the cell in pixels.</param>
    /// <param name="baseColor">The inner fill color of the tile.</param>
    /// <param name="colorIndex">The palette index used to resolve the letter glyph.</param>
    /// <param name="highlighted">Whether to draw a selection outline.</param>
    private static void DrawLetterTile(
        CanvasDrawingSession ds, float x, float y, float size, Color baseColor, int colorIndex, bool highlighted)
    {
        int bevel = Math.Max(1, (int)(size / 16));

        ds.FillRectangle(x, y, size, size, FrameMid);

        // Light bevel edges (top and left).
        ds.DrawLine(x, y, x + size - 1, y, FrameLight);
        ds.DrawLine(x, y, x, y + size - 1, FrameLight);
        if (bevel > 1)
        {
            ds.DrawLine(x + 1, y + 1, x + size - 2, y + 1, FrameLight);
            ds.DrawLine(x + 1, y + 1, x + 1, y + size - 2, FrameLight);
        }

        // Dark bevel edges (bottom and right).
        ds.DrawLine(x, y + size - 1, x + size - 1, y + size - 1, FrameDark);
        ds.DrawLine(x + size - 1, y, x + size - 1, y + size - 1, FrameDark);
        if (bevel > 1)
        {
            ds.DrawLine(x + 1, y + size - 2, x + size - 2, y + size - 2, FrameDark);
            ds.DrawLine(x + size - 2, y + 1, x + size - 2, y + size - 2, FrameDark);
        }

        float innerX = x + bevel;
        float innerY = y + bevel;
        float innerSize = size - bevel * 2;
        ds.FillRectangle(innerX, innerY, innerSize, innerSize, baseColor);

        char letter = GameSettings.LetterForColorIndex(colorIndex);
        var innerRect = new Windows.Foundation.Rect(innerX, innerY, innerSize, innerSize);
        using var format = new CanvasTextFormat
        {
            FontFamily = "Times New Roman",
            FontSize = Math.Max(10, innerSize * 0.92f),
            FontWeight = new global::Windows.UI.Text.FontWeight(700),
            HorizontalAlignment = CanvasHorizontalAlignment.Center,
            VerticalAlignment = CanvasVerticalAlignment.Center
        };
        ds.DrawText(letter.ToString(), innerRect, Color.FromArgb(255, 0, 0, 0), format);

        if (highlighted)
        {
            ds.DrawRectangle(x + 1, y + 1, size - 3, size - 3, Color.FromArgb(255, 255, 255, 255), 2);
        }
    }

    /// <summary>
    /// Draws a marble sphere inside the cell bounds (Marbles skin).
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the cell in pixels.</param>
    /// <param name="y">The top edge of the cell in pixels.</param>
    /// <param name="size">The width and height of the cell in pixels.</param>
    /// <param name="baseColor">The base color of the marble.</param>
    /// <param name="highlighted">Whether to draw a selection ring.</param>
    private static void DrawMarbleCell(CanvasDrawingSession ds, float x, float y, float size, Color baseColor, bool highlighted)
    {
        float inset = Math.Max(1, size / 12f);
        float inner = size - inset * 2;
        DrawMarble(ds, x + inset, y + inset, inner, baseColor);
        if (highlighted)
        {
            float cx = x + size / 2f;
            float cy = y + size / 2f;
            ds.DrawEllipse(cx, cy, inner / 2f, inner / 2f, Color.FromArgb(255, 255, 255, 255), 2);
        }
    }

    /// <summary>
    /// Draws a radial-gradient marble sphere at the given position and size.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the marble in pixels.</param>
    /// <param name="y">The top edge of the marble in pixels.</param>
    /// <param name="size">The diameter of the marble in pixels.</param>
    /// <param name="baseColor">The base color of the marble.</param>
    private static void DrawMarble(CanvasDrawingSession ds, float x, float y, float size, Color baseColor)
    {
        float cx = x + size / 2f;
        float cy = y + size / 2f;
        using var brush = new CanvasRadialGradientBrush(ds,
            Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.62f),
            Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.32f))
        {
            Center = new Vector2(cx - size * 0.15f, cy - size * 0.15f),
            RadiusX = size * 0.55f,
            RadiusY = size * 0.55f
        };
        ds.FillEllipse(cx, cy, size / 2f, size / 2f, brush);
    }

    /// <summary>
    /// Draws a Minecraft-style block tile with texture detail (Blockcraft skin).
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the cell in pixels.</param>
    /// <param name="y">The top edge of the cell in pixels.</param>
    /// <param name="size">The width and height of the cell in pixels.</param>
    /// <param name="userTint">The user-selected tint color for the block.</param>
    /// <param name="colorIndex">The palette index that selects the block type texture.</param>
    /// <param name="highlighted">Whether to draw a selection outline.</param>
    private static void DrawBlockcraft(CanvasDrawingSession ds, float x, float y, float size, Color userTint, int colorIndex, bool highlighted)
    {
        int inset = Math.Max(1, (int)(size / 18));
        float bx = x + inset;
        float by = y + inset;
        float bw = size - inset * 2;
        float bh = size - inset * 2;

        var blockColor = Blend(BlockcraftBase[colorIndex % BlockcraftBase.Length], userTint, 0.55f);
        ds.FillRectangle(bx, by, bw, bh, blockColor);
        DrawBlockcraftFaceShading(ds, bx, by, bw, bh, blockColor);

        // Pick block texture by color index.
        switch (colorIndex % 6)
        {
            case 0: DrawLapisBlock(ds, bx, by, bw, bh, blockColor); break;
            case 1: DrawRedstoneBlock(ds, bx, by, bw, bh, blockColor); break;
            case 2: DrawEmeraldBlock(ds, bx, by, bw, bh, blockColor); break;
            case 3: DrawGoldBlock(ds, bx, by, bw, bh, blockColor); break;
            case 4: DrawDiamondBlock(ds, bx, by, bw, bh, blockColor); break;
            default: DrawDirtBlock(ds, bx, by, bw, bh, blockColor); break;
        }

        ds.DrawRectangle(bx, by, bw - 1, bh - 1, Color.FromArgb(90, 0, 0, 0), 1);
        if (highlighted)
        {
            ds.DrawRectangle(x + 1, y + 1, size - 3, size - 3, Color.FromArgb(255, 255, 255, 255), 2);
        }
    }

    /// <summary>
    /// Draws beveled edge shading on a Blockcraft block face.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the block face in pixels.</param>
    /// <param name="y">The top edge of the block face in pixels.</param>
    /// <param name="w">The width of the block face in pixels.</param>
    /// <param name="h">The height of the block face in pixels.</param>
    /// <param name="baseColor">The base fill color of the block.</param>
    private static void DrawBlockcraftFaceShading(CanvasDrawingSession ds, float x, float y, float w, float h, Color baseColor)
    {
        int edge = Math.Max(1, (int)(w / 10));
        ds.FillRectangle(x, y, w, edge, Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.22f));
        ds.FillRectangle(x, y, edge, h, Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.18f));
        ds.FillRectangle(x + w - edge, y, edge, h, Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.28f));
        ds.FillRectangle(x, y + h - edge, w, edge, Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.28f));
    }

    /// <summary>
    /// Draws lapis-lazuli block speckle texture detail.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the block face in pixels.</param>
    /// <param name="y">The top edge of the block face in pixels.</param>
    /// <param name="w">The width of the block face in pixels.</param>
    /// <param name="h">The height of the block face in pixels.</param>
    /// <param name="baseColor">The base fill color of the block.</param>
    private static void DrawLapisBlock(CanvasDrawingSession ds, float x, float y, float w, float h, Color baseColor)
    {
        int cell = Math.Max(2, (int)(w / 8));
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                if ((row + col) % 3 == 0)
                {
                    ds.FillRectangle(x + col * cell, y + row * cell, cell, cell, Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.22f));
                }
            }
        }

        var fleck = Blend(baseColor, Color.FromArgb(255, 255, 220, 120), 0.45f);
        int dot = Math.Max(1, (int)(w / 14));
        ds.FillRectangle(x + w / 4, y + h / 3, dot, dot, fleck);
        ds.FillRectangle(x + w * 2 / 3, y + h / 2, dot, dot, fleck);
    }

    /// <summary>
    /// Draws redstone block band and glow detail.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the block face in pixels.</param>
    /// <param name="y">The top edge of the block face in pixels.</param>
    /// <param name="w">The width of the block face in pixels.</param>
    /// <param name="h">The height of the block face in pixels.</param>
    /// <param name="baseColor">The base fill color of the block.</param>
    private static void DrawRedstoneBlock(CanvasDrawingSession ds, float x, float y, float w, float h, Color baseColor)
    {
        for (int i = 0; i < 4; i++)
        {
            ds.FillRectangle(x, y + i * h / 4, w, Math.Max(1, h / 16), Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.35f));
        }

        int dot = Math.Max(2, (int)(w / 10));
        var glow = Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.45f);
        ds.FillRectangle(x + w / 5, y + h / 4, dot, dot, glow);
        ds.FillRectangle(x + w / 2, y + h / 2, dot, dot, glow);
        ds.FillRectangle(x + w * 3 / 4, y + h * 2 / 3, dot, dot, glow);
    }

    /// <summary>
    /// Draws emerald block cross-cut detail.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the block face in pixels.</param>
    /// <param name="y">The top edge of the block face in pixels.</param>
    /// <param name="w">The width of the block face in pixels.</param>
    /// <param name="h">The height of the block face in pixels.</param>
    /// <param name="baseColor">The base fill color of the block.</param>
    private static void DrawEmeraldBlock(CanvasDrawingSession ds, float x, float y, float w, float h, Color baseColor)
    {
        float cx = x + w / 2;
        float cy = y + h / 2;
        ds.DrawLine(cx, y + h / 6, cx, y + h * 5 / 6, WithAlpha(Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.35f), 100), 1);
        ds.DrawLine(x + w / 6, cy, x + w * 5 / 6, cy, WithAlpha(Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.35f), 100), 1);
        ds.FillRectangle(cx - 1, y + h / 4, 2, h / 2, WithAlpha(Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.35f), 90));
    }

    /// <summary>
    /// Draws gold block horizontal band detail.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the block face in pixels.</param>
    /// <param name="y">The top edge of the block face in pixels.</param>
    /// <param name="w">The width of the block face in pixels.</param>
    /// <param name="h">The height of the block face in pixels.</param>
    /// <param name="baseColor">The base fill color of the block.</param>
    private static void DrawGoldBlock(CanvasDrawingSession ds, float x, float y, float w, float h, Color baseColor)
    {
        for (int band = 1; band <= 3; band++)
        {
            float by = y + band * h / 4;
            var color = band % 2 == 0
                ? Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.35f)
                : Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.2f);
            ds.FillRectangle(x + w / 8, by, w * 3 / 4, Math.Max(1, h / 14), color);
        }
    }

    /// <summary>
    /// Draws diamond block facet sparkle detail.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the block face in pixels.</param>
    /// <param name="y">The top edge of the block face in pixels.</param>
    /// <param name="w">The width of the block face in pixels.</param>
    /// <param name="h">The height of the block face in pixels.</param>
    /// <param name="baseColor">The base fill color of the block.</param>
    private static void DrawDiamondBlock(CanvasDrawingSession ds, float x, float y, float w, float h, Color baseColor)
    {
        int s = Math.Max(2, (int)(w / 10));
        ds.FillRectangle(x + w / 2 - s / 2, y + h / 5, s, s, WithAlpha(Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.65f), 200));
        ds.FillRectangle(x + w / 4, y + h / 2, s, s, WithAlpha(Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.65f), 200));
        ds.FillRectangle(x + w * 3 / 5, y + h * 3 / 5, s, s, WithAlpha(Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.65f), 200));
        ds.FillRectangle(x + w / 3, y + h / 3, w / 3, h / 3, WithAlpha(Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.35f), 80));
    }

    /// <summary>
    /// Draws dirt block grass-top and soil band detail.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the block face in pixels.</param>
    /// <param name="y">The top edge of the block face in pixels.</param>
    /// <param name="w">The width of the block face in pixels.</param>
    /// <param name="h">The height of the block face in pixels.</param>
    /// <param name="baseColor">The base fill color of the block.</param>
    private static void DrawDirtBlock(CanvasDrawingSession ds, float x, float y, float w, float h, Color baseColor)
    {
        for (int row = 0; row < 5; row++)
        {
            float ry = y + h / 6 + row * h / 7;
            ds.FillRectangle(x + w / 10, ry, w * 4 / 5, Math.Max(1, h / 18), Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.3f));
        }

        ds.FillRectangle(x, y, w, Math.Max(2, h / 6), Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.15f));
    }

    /// <summary>
    /// Draws a LEGO-style brick with corner studs (Bricks skin).
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the cell in pixels.</param>
    /// <param name="y">The top edge of the cell in pixels.</param>
    /// <param name="size">The width and height of the cell in pixels.</param>
    /// <param name="baseColor">The base fill color of the brick.</param>
    /// <param name="highlighted">Whether to draw a selection outline.</param>
    private static void DrawBrick(CanvasDrawingSession ds, float x, float y, float size, Color baseColor, bool highlighted)
    {
        int inset = Math.Max(1, (int)(size / 10));
        float bx = x + inset;
        float by = y + inset;
        float bw = size - inset * 2;
        float bh = size - inset * 2;
        float arc = Math.Max(3, size / 7);

        using var body = new CanvasLinearGradientBrush(ds,
            Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.18f),
            Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.15f))
        {
            StartPoint = new Vector2(bx, by),
            EndPoint = new Vector2(bx + bw, by + bh)
        };
        ds.FillRoundedRectangle(bx, by, bw, bh, arc, arc, body);

        int stud = Math.Max(3, (int)Math.Min(bw, bh) / 5);
        int margin = Math.Max(stud / 2, (int)Math.Min(bw, bh) / 7);
        DrawStud(ds, baseColor, bx + margin, by + margin, stud);
        DrawStud(ds, baseColor, bx + bw - margin - stud, by + margin, stud);
        DrawStud(ds, baseColor, bx + margin, by + bh - margin - stud, stud);
        DrawStud(ds, baseColor, bx + bw - margin - stud, by + bh - margin - stud, stud);

        ds.DrawRoundedRectangle(bx, by, bw - 1, bh - 1, arc, arc, Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.28f), 1);
        if (highlighted)
        {
            ds.DrawRoundedRectangle(x + 1, y + 1, size - 3, size - 3, arc + 2, arc + 2, Color.FromArgb(255, 255, 255, 255), 2);
        }
    }

    /// <summary>
    /// Draws a single circular stud on a brick tile.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="baseColor">The base color used to shade the stud.</param>
    /// <param name="sx">The left edge of the stud bounding box in pixels.</param>
    /// <param name="sy">The top edge of the stud bounding box in pixels.</param>
    /// <param name="stud">The diameter of the stud in pixels.</param>
    private static void DrawStud(CanvasDrawingSession ds, Color baseColor, float sx, float sy, int stud)
    {
        float radius = stud / 2f;
        float cx = sx + radius;
        float cy = sy + radius;
        ds.FillEllipse(cx, cy, radius, radius, Blend(baseColor, Color.FromArgb(255, 255, 255, 255), 0.45f));
        ds.DrawEllipse(cx, cy, radius - 0.5f, radius - 0.5f, Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.15f), 1);
    }

    /// <summary>
    /// Draws a geometric shape tile (Shapes skin).
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the cell in pixels.</param>
    /// <param name="y">The top edge of the cell in pixels.</param>
    /// <param name="size">The width and height of the cell in pixels.</param>
    /// <param name="baseColor">The fill color of the shape.</param>
    /// <param name="colorIndex">The palette index that selects the shape type.</param>
    /// <param name="highlighted">Whether to draw a selection overlay.</param>
    private static void DrawShapeTile(CanvasDrawingSession ds, float x, float y, float size, Color baseColor, int colorIndex, bool highlighted)
    {
        float pad = Math.Max(2, size / 8f);
        float cx = x + size / 2f;
        float cy = y + size / 2f;
        float radius = size / 2f - pad;
        float stroke = Math.Max(1.2f, size / 14f);

        using var shape = CreateShapeGeometry(ds, colorIndex, cx, cy, radius);
        ds.FillGeometry(shape, baseColor);
        ds.DrawGeometry(shape, Blend(baseColor, Color.FromArgb(255, 0, 0, 0), 0.55f), stroke);

        if (highlighted)
        {
            ds.FillGeometry(shape, Color.FromArgb(90, 255, 255, 255));
            DrawSelectionCellBorder(ds, x, y, size);
        }
    }

    /// <summary>
    /// Creates the Win2D geometry for a shape tile based on the color index.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="colorIndex">The palette index that selects the shape type.</param>
    /// <param name="cx">The horizontal center of the shape in pixels.</param>
    /// <param name="cy">The vertical center of the shape in pixels.</param>
    /// <param name="radius">The radius (or half-extent) of the shape in pixels.</param>
    /// <returns>The geometry describing the shape outline.</returns>
    private static CanvasGeometry CreateShapeGeometry(CanvasDrawingSession ds, int colorIndex, float cx, float cy, float radius)
    {
        return (colorIndex % 6) switch
        {
            0 => CanvasGeometry.CreateRectangle(ds, cx - radius, cy - radius, radius * 2, radius * 2),
            1 => CanvasGeometry.CreateCircle(ds, cx, cy, radius),
            2 => CreateTriangleGeometry(ds, cx, cy, radius),
            3 => CreateStarGeometry(ds, cx, cy, radius, radius * 0.45f, 5),
            4 => CreateDiamondGeometry(ds, cx, cy, radius),
            _ => CreateHexagonGeometry(ds, cx, cy, radius)
        };
    }

    /// <summary>
    /// Creates an upward-pointing triangle geometry centered at the given point.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="cx">The horizontal center of the triangle in pixels.</param>
    /// <param name="cy">The vertical center of the triangle in pixels.</param>
    /// <param name="r">The radius from center to vertex in pixels.</param>
    /// <returns>The triangle polygon geometry.</returns>
    private static CanvasGeometry CreateTriangleGeometry(CanvasDrawingSession ds, float cx, float cy, float r)
    {
        var points = new Vector2[]
        {
            new(cx, cy - r),
            new(cx + r, cy + r * 0.85f),
            new(cx - r, cy + r * 0.85f)
        };
        return CanvasGeometry.CreatePolygon(ds, points);
    }

    /// <summary>
    /// Creates a diamond (rotated square) geometry centered at the given point.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="cx">The horizontal center of the diamond in pixels.</param>
    /// <param name="cy">The vertical center of the diamond in pixels.</param>
    /// <param name="r">The radius from center to vertex in pixels.</param>
    /// <returns>The diamond polygon geometry.</returns>
    private static CanvasGeometry CreateDiamondGeometry(CanvasDrawingSession ds, float cx, float cy, float r)
    {
        var points = new Vector2[]
        {
            new(cx, cy - r),
            new(cx + r, cy),
            new(cx, cy + r),
            new(cx - r, cy)
        };
        return CanvasGeometry.CreatePolygon(ds, points);
    }

    /// <summary>
    /// Creates a regular hexagon geometry centered at the given point.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="cx">The horizontal center of the hexagon in pixels.</param>
    /// <param name="cy">The vertical center of the hexagon in pixels.</param>
    /// <param name="r">The circumradius of the hexagon in pixels.</param>
    /// <returns>The hexagon polygon geometry.</returns>
    private static CanvasGeometry CreateHexagonGeometry(CanvasDrawingSession ds, float cx, float cy, float r)
    {
        var points = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            double angle = Math.PI / 3 * i - Math.PI / 6;
            points[i] = new Vector2(cx + r * (float)Math.Cos(angle), cy + r * (float)Math.Sin(angle));
        }

        return CanvasGeometry.CreatePolygon(ds, points);
    }

    /// <summary>
    /// Creates a star polygon geometry with alternating inner and outer radii.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="cx">The horizontal center of the star in pixels.</param>
    /// <param name="cy">The vertical center of the star in pixels.</param>
    /// <param name="outerR">The outer vertex radius in pixels.</param>
    /// <param name="innerR">The inner vertex radius in pixels.</param>
    /// <param name="points">The number of star points.</param>
    /// <returns>The star polygon geometry.</returns>
    private static CanvasGeometry CreateStarGeometry(
        CanvasDrawingSession ds, float cx, float cy, float outerR, float innerR, int points)
    {
        var vertices = new Vector2[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            double angle = Math.PI * i / points - Math.PI / 2;
            float r = i % 2 == 0 ? outerR : innerR;
            vertices[i] = new Vector2(cx + r * (float)Math.Cos(angle), cy + r * (float)Math.Sin(angle));
        }

        return CanvasGeometry.CreatePolygon(ds, vertices);
    }

    /// <summary>
    /// Draws a thin white selection border around a cell.
    /// </summary>
    /// <param name="ds">The Win2D drawing session.</param>
    /// <param name="x">The left edge of the cell in pixels.</param>
    /// <param name="y">The top edge of the cell in pixels.</param>
    /// <param name="size">The width and height of the cell in pixels.</param>
    private static void DrawSelectionCellBorder(CanvasDrawingSession ds, float x, float y, float size)
    {
        ds.DrawRectangle(x + 1, y + 1, size - 3, size - 3, Color.FromArgb(255, 255, 255, 255), 1);
    }

    /// <summary>
    /// Returns a copy of <paramref name="color"/> with the alpha channel replaced.
    /// </summary>
    /// <param name="color">The source color.</param>
    /// <param name="alpha">The new alpha value (0–255).</param>
    /// <returns>The color with the specified alpha.</returns>
    private static Color WithAlpha(Color color, byte alpha) =>
        Color.FromArgb(alpha, color.R, color.G, color.B);

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
