using System.Drawing.Drawing2D;

namespace TimeTrack.DesktopHost.Notifications;

public sealed class IdleJustificationPromptForm : Form
{
    public sealed class SubmissionEventArgs : EventArgs
    {
        public bool IsSkipped { get; init; }
        public string IdlePeriodId { get; init; } = string.Empty;
        public string? ReasonCode { get; init; }
        public string? Note { get; init; }
    }

    private static readonly (string Code, string Label)[] ReasonOptions =
    [
        ("break", "Break"),
        ("meeting", "Meeting"),
        ("personal", "Personal"),
        ("technical_issue", "Technical issue"),
        ("context_switch", "Context switch"),
        ("other", "Other")
    ];

    private static readonly Color CardBg = Color.FromArgb(22, 25, 36);
    private static readonly Color BorderColor = Color.FromArgb(48, 52, 70);
    private static readonly Color TextPrimary = Color.FromArgb(245, 247, 251);
    private static readonly Color TextSecondary = Color.FromArgb(180, 186, 205);
    private static readonly Color Accent = Color.FromArgb(125, 211, 252);
    private static readonly Color AccentDim = Color.FromArgb(23, 37, 56);
    private static readonly Color InputBg = Color.FromArgb(18, 21, 31);
    private static readonly Color ButtonMuted = Color.FromArgb(36, 41, 56);

    private readonly string _idlePeriodId;
    private readonly TextBox _noteTextBox;
    private readonly Label _counterLabel;
    private readonly Button _saveButton;
    private readonly List<Button> _reasonButtons = [];
    private string? _selectedReasonCode;
    private bool _resultRaised;

    public event EventHandler<SubmissionEventArgs>? Submitted;

    public IdleJustificationPromptForm(string idlePeriodId, DateTime startedAt, DateTime endedAt, int durationSeconds)
    {
        _idlePeriodId = idlePeriodId;

        SuspendLayout();
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.FromArgb(12, 15, 22);
        DoubleBuffered = true;
        Padding = new Padding(20);

        var duration = TimeSpan.FromSeconds(Math.Max(0, durationSeconds));
        var subtitle = $"{startedAt.ToLocalTime():HH:mm} - {endedAt.ToLocalTime():HH:mm}  •  {FormatDuration(duration)}";

        var titleLabel = new Label
        {
            Text = "You were idle for a while",
            Font = new Font("Segoe UI Semibold", 13f),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(20, 20),
            BackColor = Color.Transparent
        };
        Controls.Add(titleLabel);

        var subtitleLabel = new Label
        {
            Text = subtitle,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(20, 48),
            BackColor = Color.Transparent
        };
        Controls.Add(subtitleLabel);

        var helperLabel = new Label
        {
            Text = "Add a quick reason if you want. You can also skip this.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(20, 76),
            BackColor = Color.Transparent
        };
        Controls.Add(helperLabel);

        var reasonsPanel = new FlowLayoutPanel
        {
            Location = new Point(20, 108),
            Size = new Size(360, 94),
            WrapContents = true,
            AutoScroll = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        foreach (var (code, label) in ReasonOptions)
        {
            var button = BuildReasonButton(code, label);
            _reasonButtons.Add(button);
            reasonsPanel.Controls.Add(button);
        }

        Controls.Add(reasonsPanel);

        _noteTextBox = new TextBox
        {
            Multiline = true,
            Size = new Size(360, 90),
            Location = new Point(20, 214),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = InputBg,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9.5f),
            MaxLength = 500
        };
        _noteTextBox.TextChanged += (_, _) => UpdateCounter();
        Controls.Add(_noteTextBox);

        _counterLabel = new Label
        {
            Text = "0 / 500",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = TextSecondary,
            AutoSize = true,
            BackColor = Color.Transparent,
            Location = new Point(321, 309)
        };
        Controls.Add(_counterLabel);

        var skipButton = new Button
        {
            Text = "Skip",
            Font = new Font("Segoe UI", 9.5f),
            Size = new Size(90, 36),
            Location = new Point(20, 336),
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonMuted,
            ForeColor = TextSecondary
        };
        skipButton.FlatAppearance.BorderColor = BorderColor;
        skipButton.FlatAppearance.BorderSize = 1;
        skipButton.Click += (_, _) => RaiseResult(isSkipped: true);
        Controls.Add(skipButton);

        _saveButton = new Button
        {
            Text = "Save reason",
            Font = new Font("Segoe UI Semibold", 9.5f),
            Size = new Size(120, 36),
            Location = new Point(260, 336),
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.FromArgb(9, 12, 18),
            Enabled = false
        };
        _saveButton.FlatAppearance.BorderSize = 0;
        _saveButton.Click += (_, _) => RaiseResult(isSkipped: false);
        Controls.Add(_saveButton);

        ClientSize = new Size(400, 392);
        PositionInCorner();
        ApplyRoundedRegion();
        ResumeLayout(false);
    }

    private Button BuildReasonButton(string code, string label)
    {
        var button = new Button
        {
            Text = label,
            Tag = code,
            Size = new Size(110, 34),
            Margin = new Padding(0, 0, 8, 8),
            FlatStyle = FlatStyle.Flat,
            BackColor = ButtonMuted,
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI", 9f)
        };

        button.FlatAppearance.BorderColor = BorderColor;
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) => SelectReason(code);
        return button;
    }

    private void SelectReason(string code)
    {
        _selectedReasonCode = code;
        foreach (var button in _reasonButtons)
        {
            var isSelected = string.Equals(button.Tag as string, code, StringComparison.Ordinal);
            button.BackColor = isSelected ? AccentDim : ButtonMuted;
            button.ForeColor = isSelected ? Accent : TextSecondary;
            button.FlatAppearance.BorderColor = isSelected ? Accent : BorderColor;
        }

        _saveButton.Enabled = true;
    }

    private void UpdateCounter()
    {
        _counterLabel.Text = $"{_noteTextBox.TextLength} / 500";
    }

    private void RaiseResult(bool isSkipped)
    {
        if (_resultRaised)
            return;

        _resultRaised = true;
        Submitted?.Invoke(this, new SubmissionEventArgs
        {
            IsSkipped = isSkipped,
            IdlePeriodId = _idlePeriodId,
            ReasonCode = isSkipped ? null : _selectedReasonCode,
            Note = isSkipped || string.IsNullOrWhiteSpace(_noteTextBox.Text) ? null : _noteTextBox.Text.Trim()
        });
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (!_resultRaised)
        {
            RaiseResult(isSkipped: true);
            return;
        }

        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var brush = new SolidBrush(CardBg);
        using var path = CreateRoundedPath(new Rectangle(0, 0, Width, Height), 18);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);

        using var borderPen = new Pen(BorderColor, 1);
        using var borderPath = CreateRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 18);
        e.Graphics.DrawPath(borderPen, borderPath);
    }

    protected override bool ShowWithoutActivation => true;

    private void PositionInCorner()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        Location = new Point(area.Right - Width - 24, area.Top + 24);
    }

    private void ApplyRoundedRegion()
    {
        using var path = CreateRoundedPath(new Rectangle(0, 0, Width, Height), 18);
        Region = new Region(path);
    }

    private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";

        if (duration.TotalMinutes >= 1)
            return $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))}m";

        return $"{Math.Max(1, (int)Math.Round(duration.TotalSeconds))}s";
    }
}
