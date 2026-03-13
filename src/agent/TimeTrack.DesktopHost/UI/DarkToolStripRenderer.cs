using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TimeTrack.DesktopHost.UI;

/// <summary>
/// Custom dark theme renderer for ToolStrip/ContextMenuStrip with rounded corners
/// </summary>
public class DarkToolStripRenderer : ToolStripRenderer
{
    private const int CornerRadius = 8;

    // Color palette matching the app theme
    private static readonly Color BackgroundColor = Color.FromArgb(30, 30, 30);
    private static readonly Color HoverColor = Color.FromArgb(45, 45, 45);
    private static readonly Color AccentColor = Color.FromArgb(99, 102, 241); // Purple
    private static readonly Color TextColor = Color.FromArgb(235, 235, 235);
    private static readonly Color TextMutedColor = Color.FromArgb(180, 180, 180);
    private static readonly Color SeparatorColor = Color.FromArgb(55, 55, 55);
    private static readonly Color BorderColor = Color.FromArgb(50, 50, 50);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        var toolStrip = e.ToolStrip;
        var bounds = e.AffectedBounds;

        // Create rounded rectangle path
        using var path = GetRoundedRectanglePath(bounds, CornerRadius);

        // Fill background
        using var brush = new SolidBrush(BackgroundColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);

        // Draw border
        using var pen = new Pen(BorderColor, 1);
        e.Graphics.DrawPath(pen, path);

        // Apply rounded region to the toolStrip
        if (toolStrip is ContextMenuStrip)
        {
            toolStrip.Region = new Region(path);
        }
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        var bounds = new Rectangle(Point.Empty, item.Size);

        if (item.Selected || item.Pressed)
        {
            // Hover background
            using var brush = new SolidBrush(HoverColor);
            e.Graphics.FillRectangle(brush, bounds);

            // Left accent bar
            using var accentBrush = new SolidBrush(AccentColor);
            e.Graphics.FillRectangle(accentBrush, 0, 0, 3, bounds.Height);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var item = e.Item;

        // Determine text color based on state
        if (item.Selected)
        {
            e.TextColor = Color.White;
        }
        else if (item.Font.Bold)
        {
            e.TextColor = AccentColor;
        }
        else
        {
            e.TextColor = TextColor;
        }

        // Use base rendering with custom color
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var bounds = e.Item.Bounds;
        var y = bounds.Top + bounds.Height / 2;

        using var pen = new Pen(SeparatorColor, 1);
        e.Graphics.DrawLine(pen, bounds.Left + 12, y, bounds.Right - 12, y);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // Remove default image margin for cleaner look
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item.Selected ? Color.White : TextMutedColor;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var bounds = e.ImageRectangle;
        bounds.Inflate(-2, -2);

        // Draw check background
        using var brush = new SolidBrush(AccentColor);
        e.Graphics.FillRectangle(brush, bounds);

        // Draw checkmark
        using var pen = new Pen(Color.White, 2);
        var points = new[]
        {
            new Point(bounds.Left + 3, bounds.Top + bounds.Height / 2),
            new Point(bounds.Left + bounds.Width / 3, bounds.Bottom - 4),
            new Point(bounds.Right - 3, bounds.Top + 4)
        };
        e.Graphics.DrawLines(pen, points);
    }

    /// <summary>
    /// Creates a rounded rectangle graphics path
    /// </summary>
    private static GraphicsPath GetRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();

        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        // Top left arc
        path.AddArc(arc, 180, 90);

        // Top right arc
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);

        // Bottom right arc
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        // Bottom left arc
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }
}
