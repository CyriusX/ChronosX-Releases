using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace TimeTrack.DesktopHost.Notifications;

/// <summary>
/// Centered modal notification from admin panel.
/// Shows title + body with an OK button. Auto-dismisses after 30s.
/// </summary>
public sealed class RemoteNotificationForm : Form
{
    private readonly System.Windows.Forms.Timer _timer;
    private int _elapsedTicks;
    private const int AutoDismissSeconds = 30;

    // Theme
    private static readonly Color BgCard = Color.FromArgb(22, 25, 36);
    private static readonly Color BorderColor = Color.FromArgb(48, 52, 70);
    private static readonly Color TextWhite = Color.FromArgb(245, 247, 251);
    private static readonly Color TextBody = Color.FromArgb(180, 186, 205);
    private static readonly Color TextDim = Color.FromArgb(100, 108, 130);
    private static readonly Color Purple = Color.FromArgb(139, 92, 246);
    private static readonly Color PurpleDim = Color.FromArgb(80, 50, 160);
    private static readonly Color BtnBg = Color.FromArgb(139, 92, 246);

    private const int W = 420;
    private const int SidePad = 32;
    private const int Radius = 18;
    private const int BtnH = 44;

    public RemoteNotificationForm(string title, string body)
    {
        SuspendLayout();
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(14, 16, 24);
        Opacity = 1.0;
        DoubleBuffered = true;

        var cw = W - SidePad * 2;
        var y = 32;

        // Admin badge
        var badgeFont = new Font("Segoe UI", 8f, FontStyle.Bold);
        var badge = new Label
        {
            Text = "MENSAGEM DO ADMINISTRADOR",
            Font = badgeFont,
            ForeColor = Purple,
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(cw, 18),
            Location = new Point(SidePad, y),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(badge);
        y += 26;

        // Title
        var titleFont = new Font("Segoe UI Semibold", 15f);
        var titleSize = TextRenderer.MeasureText(title, titleFont, new Size(cw, 0), TextFormatFlags.WordBreak);
        var titleLabel = new Label
        {
            Text = title,
            Font = titleFont,
            ForeColor = TextWhite,
            Size = new Size(cw, titleSize.Height + 4),
            Location = new Point(SidePad, y),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(titleLabel);
        y += titleLabel.Height + 12;

        // Body
        if (!string.IsNullOrWhiteSpace(body))
        {
            var bodyFont = new Font("Segoe UI", 10.5f);
            var bodySize = TextRenderer.MeasureText(body, bodyFont, new Size(cw, 0), TextFormatFlags.WordBreak);
            var bodyLabel = new Label
            {
                Text = body,
                Font = bodyFont,
                ForeColor = TextBody,
                Size = new Size(cw, bodySize.Height + 8),
                Location = new Point(SidePad, y),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            Controls.Add(bodyLabel);
            y += bodyLabel.Height + 24;
        }
        else
        {
            y += 16;
        }

        // OK button
        var okBtn = new Button
        {
            Text = "OK",
            Font = new Font("Segoe UI Semibold", 11f),
            Size = new Size(cw, BtnH),
            Location = new Point(SidePad, y),
            FlatStyle = FlatStyle.Flat,
            BackColor = BtnBg,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
        };
        okBtn.FlatAppearance.BorderSize = 0;
        okBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(155, 110, 255);
        okBtn.Click += (_, _) => Close();
        Controls.Add(okBtn);
        y += BtnH + 20;

        // Auto-dismiss label
        var dimLabel = new Label
        {
            Text = $"Fecha automaticamente em {AutoDismissSeconds}s",
            Font = new Font("Segoe UI", 8f),
            ForeColor = TextDim,
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(cw, 16),
            Location = new Point(SidePad, y),
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(dimLabel);
        y += 28;

        ClientSize = new Size(W, y);

        // Center on screen
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(
            wa.Left + (wa.Width - Width) / 2,
            wa.Top + (wa.Height - Height) / 2);

        ResumeLayout(true);
        PerformLayout();
        ApplyRegion();
        EnableRoundedCorners();

        // Auto-dismiss timer
        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) =>
        {
            _elapsedTicks++;
            var remaining = AutoDismissSeconds - _elapsedTicks;
            dimLabel.Text = $"Fecha automaticamente em {remaining}s";
            if (remaining <= 0) Close();
        };
    }

    protected override void OnShown(EventArgs e) { base.OnShown(e); _timer.Start(); }
    protected override void OnFormClosed(FormClosedEventArgs e) { _timer.Stop(); _timer.Dispose(); base.OnFormClosed(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Card fill
        using (var b = new SolidBrush(BgCard))
        using (var p = MakeRoundedRect(0, 0, Width, Height, Radius))
            g.FillPath(b, p);

        // Border
        using (var pen = new Pen(BorderColor, 1))
        using (var p = MakeRoundedRect(0, 0, Width - 1, Height - 1, Radius))
            g.DrawPath(pen, p);

        // Top glow line (purple)
        using (var b = new LinearGradientBrush(
            new Point(Radius, 0), new Point(Width - Radius, 0), Purple, PurpleDim))
            g.FillRectangle(b, Radius, 1, Width - Radius * 2, 2);
    }

    private void ApplyRegion()
    {
        using var p = MakeRoundedRect(0, 0, Width, Height, Radius);
        Region = new Region(p);
    }

    private static GraphicsPath MakeRoundedRect(int x, int y, int w, int h, int r)
    {
        var d = r * 2;
        var p = new GraphicsPath();
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
            return cp;
        }
    }

    private void EnableRoundedCorners()
    { try { var p = 2; DwmSetWindowAttribute(Handle, 33, ref p, 4); } catch { } }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
}
