namespace MouseMotionInspector.Native;

// A short, navigable walkthrough. Detailed reference material is opt-in.
internal sealed class InterpretationGuide : UserControl
{
    private static readonly string[] Steps = ["Identify", "Record", "Read", "Compare"];
    private readonly GuideChoice[] stepButtons = new GuideChoice[4], examples = new GuideChoice[3];
    private readonly Label heading = Copy("", 16, true), introduction = Copy(""), hint = Copy("", 9);
    private readonly Label[] instructions = [Copy(""), Copy(""), Copy("")];
    private readonly Label position = Copy("", 9);
    private readonly GuidePicture picture = new();
    private readonly TableLayoutPanel content = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = Padding.Empty };
    private readonly TableLayoutPanel words = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = new(0, 6, 22, 6) };
    private readonly FlowLayoutPanel patterns = new() { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Margin = new(0, 0, 0, 6) };
    private readonly Panel scroll = new() { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
    private readonly ThemeButton back = ActionButton("Back"), next = ActionButton("Next: Record", true);
    private readonly Action showGraphs;
    private int step;

    internal static Control Create(Action showGraphs) => new InterpretationGuide(showGraphs);

    internal InterpretationGuide(Action showGraphs)
    {
        this.showGraphs = showGraphs;
        Dock = DockStyle.Fill; BackColor = AppTheme.Background; ForeColor = AppTheme.Text;
        Font = new("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        AccessibleName = "Four-step guide to mouse movement tests";
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(12, 4, 12, 4), ColumnCount = 1, RowCount = 3 };
        root.RowStyles.Add(new(SizeType.AutoSize)); root.RowStyles.Add(new(SizeType.Percent, 100)); root.RowStyles.Add(new(SizeType.AutoSize));
        var navigation = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Margin = new(0, 0, 0, 10), AccessibleName = "Guide steps" };
        for (var i = 0; i < Steps.Length; i++)
        {
            var index = i; navigation.ColumnStyles.Add(new(SizeType.Percent, 25));
            var button = new GuideChoice { Text = $"{i + 1}   {Steps[i]}", Dock = DockStyle.Fill, Height = 38, Margin = new(0, 0, 6, 0), TabIndex = i };
            button.Click += (_, _) => ShowStep(index);
            stepButtons[i] = button; navigation.Controls.Add(button, i, 0);
        }
        root.Controls.Add(navigation, 0, 0);
        content.ColumnStyles.Add(new(SizeType.Percent, 43)); content.ColumnStyles.Add(new(SizeType.Percent, 57));
        words.Controls.Add(heading); words.Controls.Add(introduction);
        foreach (var instruction in instructions) words.Controls.Add(instruction);
        hint.ForeColor = AppTheme.Muted; hint.Margin = new(0, 4, 0, 0); words.Controls.Add(hint);
        content.Controls.Add(words, 0, 0);
        var visual = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = new(0, 6, 0, 6) };
        foreach (var (name, index) in new[] { ("Quiet", 0), ("Back-and-forth", 1), ("Drift", 2) })
        {
            var button = new GuideChoice { Text = name, AutoSize = true, MinimumSize = new(84, 30), Padding = new(10, 4, 10, 4), Margin = new(0, 0, 6, 0) };
            button.Click += (_, _) => ShowExample(index); examples[index] = button; patterns.Controls.Add(button);
        }
        visual.Controls.Add(patterns); visual.Controls.Add(picture); content.Controls.Add(visual, 1, 0);
        scroll.Controls.Add(content); root.Controls.Add(scroll, 0, 1);
        content.SizeChanged += (_, _) => WrapText();
        scroll.SizeChanged += (_, _) => SizePicture();
        var footer = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Margin = new(0, 8, 0, 0) };
        footer.ColumnStyles.Add(new(SizeType.AutoSize)); footer.ColumnStyles.Add(new(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new(SizeType.AutoSize)); footer.ColumnStyles.Add(new(SizeType.AutoSize));
        var help = ActionButton("Help with a result"); help.Click += (_, _) => GuideReference.Show(this);
        position.Anchor = AnchorStyles.Left; position.Margin = new(16, 0, 4, 0); position.ForeColor = AppTheme.Muted;
        back.Click += (_, _) => ShowStep(step - 1);
        next.Click += (_, _) => { if (step == 3) this.showGraphs(); else ShowStep(step + 1); };
        footer.Controls.Add(help, 0, 0); footer.Controls.Add(position, 1, 0); footer.Controls.Add(back, 2, 0); footer.Controls.Add(next, 3, 0);
        root.Controls.Add(footer, 0, 2); Controls.Add(root);
        ShowExample(1); ShowStep(0);
    }

    private void WrapText()
    {
        var width = Math.Max(120, words.Width - words.Padding.Horizontal);
        foreach (Control label in words.Controls)
            if (label.MaximumSize.Width != width) label.MaximumSize = new(width, 0);
    }

    private void SizePicture()
    {
        var scale = DeviceDpi / 96f;
        var toolbar = step == 2 ? patterns.Height + patterns.Margin.Vertical : 0;
        var height = Math.Clamp(scroll.ClientSize.Height - toolbar - (int)(14 * scale), (int)(140 * scale), (int)(190 * scale));
        if (picture.Height != height) picture.Height = height;
    }

    internal void ShowStep(int index)
    {
        if (index < 0 || index >= Steps.Length) return;
        SuspendLayout(); step = index;
        for (var i = 0; i < Steps.Length; i++) stepButtons[i].Choose(i == step);
        patterns.Visible = step == 2; back.Enabled = step > 0;
        next.Text = step == 3 ? "Open graphs" : $"Next: {Steps[step + 1]}";
        position.Text = $"Step {step + 1} of 4";
        var page = step switch
        {
            0 => new[] { "Find the right device", "Move one device. Follow its counter.",
                "1. Choose Live identification above.", "2. Move only the mouse you want to check.", "3. Select the row whose Live reports increase. Save a label in Device identity.",
                "Two rows can belong to one mouse. The name alone is not enough." },
            1 => new[] { "Record a quiet minute", "Keep a deliberate-motion test separate.",
                "1. Enter your surface, DPI and polling rate.", "2. Use 5 seconds to settle and 60 to record.", "3. Start test, let go, then keep your hands off the mouse and desk.",
                "DPI and polling rate are labels here. Change the actual settings in your mouse software." },
            2 => new[] { "Learn the movement patterns", "Switch examples to see what the numbers mean.",
                "Path: where the device moved from its start.", "Displacement: when X or Y changed.", "Activity: how much it moved, including steps that came back.",
                "Illustrations use raw relative units. Your recorded graphs stay in Movement graphs." },
            _ => new[] { "Change one thing at a time", "Look for a pattern that repeats.",
                "1. Repeat the same hands-off test 2–3 times.", "2. Change only the surface; repeat the tests.", "3. Compare travel, direction and timing.",
                "Check warnings first. A trace cannot confirm a sensor fault." }
        };
        heading.Text = page[0]; introduction.Text = page[1]; introduction.ForeColor = AppTheme.Muted;
        for (var i = 0; i < instructions.Length; i++) instructions[i].Text = page[i + 2];
        hint.Text = page[5]; picture.SetPage(step); WrapText();
        ResumeLayout(true); SizePicture(); scroll.AutoScrollPosition = Point.Empty;
    }

    internal void ShowExample(int index)
    {
        for (var i = 0; i < examples.Length; i++) examples[i].Choose(i == index);
        picture.SetExample(index);
    }

    private static Label Copy(string text, float size = 10, bool bold = false) => new()
    {
        Text = text, AutoSize = true, Font = new("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = AppTheme.Text, Margin = new(0, 0, 0, 6), UseMnemonic = false
    };
    private static ThemeButton ActionButton(string text, bool primary = false) => new()
    {
        Text = text, Primary = primary, AutoSize = true, MinimumSize = new(0, 32), Padding = new(12, 4, 12, 4), Margin = new(0, 0, 6, 0)
    };
}

internal sealed class GuideChoice : Button
{
    private bool selected;
    internal GuideChoice()
    {
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; UseVisualStyleBackColor = false;
        ForeColor = AppTheme.Text; BackColor = AppTheme.Panel;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.ApplyThemingImplicitly, false);
    }
    internal void Choose(bool value)
    {
        selected = value; AccessibleName = Text + (value ? " (selected)" : ""); Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(selected ? AppTheme.Selection : AppTheme.Panel);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, selected ? AppTheme.Accent : AppTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        if (selected) { using var line = new Pen(AppTheme.Accent, Math.Max(2, DeviceDpi / 48f)); e.Graphics.DrawLine(line, 0, Height - 2, Width, Height - 2); }
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), AppTheme.Accent, BackColor);
    }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
}
