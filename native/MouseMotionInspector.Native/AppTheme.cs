using System.Drawing.Drawing2D;

namespace MouseMotionInspector.Native;

internal static class AppTheme
{
    internal static readonly Color Background = Color.FromArgb(13, 17, 21);
    internal static readonly Color Panel = Color.FromArgb(21, 27, 33);
    internal static readonly Color Input = Color.FromArgb(15, 21, 26);
    internal static readonly Color Raised = Color.FromArgb(32, 42, 51);
    internal static readonly Color Hover = Color.FromArgb(43, 57, 69);
    internal static readonly Color Border = Color.FromArgb(44, 53, 62);
    internal static readonly Color Text = Color.FromArgb(236, 242, 246);
    internal static readonly Color Muted = Color.FromArgb(153, 169, 183);
    internal static readonly Color DisabledText = Color.FromArgb(132, 146, 158);
    internal static readonly Color Accent = Color.FromArgb(194, 249, 112);
    internal static readonly Color AccentHover = Color.FromArgb(212, 255, 150);
    internal static readonly Color AccentPressed = Color.FromArgb(166, 218, 87);
    internal static readonly Color AccentText = Color.FromArgb(23, 32, 8);
    internal static readonly Color Selection = Color.FromArgb(44, 61, 40);
    internal static readonly Color Cyan = Color.FromArgb(109, 219, 214);

    // Apply dark mode to native surfaces that honor it, including popup lists
    // and spinners. The tab strip below owns its whole surface and frame.
    internal static void Initialize() => Application.SetColorMode(SystemColorMode.Dark);
}

internal sealed class ThemeButton : Button
{
    [System.ComponentModel.DefaultValue(false)]
    internal bool Primary { get; init; }
    private bool hovered, pressed;

    internal ThemeButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.ApplyThemingImplicitly, false);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        BackColor = AppTheme.Raised;
        ForeColor = AppTheme.Text;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? AppTheme.Background);
        var fill = !Enabled ? AppTheme.Panel : Primary
            ? pressed ? AppTheme.AccentPressed : hovered ? AppTheme.AccentHover : AppTheme.Accent
            : pressed ? AppTheme.Input : hovered ? AppTheme.Hover : AppTheme.Raised;
        var text = !Enabled ? AppTheme.DisabledText : Primary ? AppTheme.AccentText : AppTheme.Text;
        var border = Enabled && (Focused || hovered) ? AppTheme.Accent : Primary && Enabled ? fill : AppTheme.Border;
        var bounds = new RectangleF(.5f, .5f, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
        var radius = Math.Min(5f * DeviceDpi / 96f, Math.Min(bounds.Width, bounds.Height) / 2);
        var diameter = radius * 2;
        using var outline = new GraphicsPath();
        outline.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        outline.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        outline.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        outline.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        outline.CloseFigure();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var fillBrush = new SolidBrush(fill);
        using var borderPen = new Pen(border);
        e.Graphics.FillPath(fillBrush, outline);
        e.Graphics.DrawPath(borderPen, outline);
        var textBounds = Rectangle.Inflate(ClientRectangle, -4, -2);
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        if (!ShowKeyboardCues) flags |= TextFormatFlags.HidePrefix;
        TextRenderer.DrawText(e.Graphics, Text, Font, textBounds, text, flags);
        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), text, fill);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) pressed = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
    protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) pressed = false; Invalidate(); }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.KeyCode == Keys.Space) pressed = true; Invalidate(); }
    protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); pressed = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); pressed = false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); pressed = hovered = false; Invalidate(); }
}

internal sealed class ThemeComboBox : ComboBox
{
    internal ThemeComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        BackColor = AppTheme.Input;
        ForeColor = AppTheme.Text;
        UpdateItemHeight();
    }

    // Editable combos use a native single-line edit, whose text does not center
    // in the extra height used by our owner-drawn, non-editable selections.
    private void UpdateItemHeight() => ItemHeight = Font.Height +
        (DropDownStyle == ComboBoxStyle.DropDown ? 0 : (int)Math.Round(6 * DeviceDpi / 96.0));
    protected override void OnDropDownStyleChanged(EventArgs e) { base.OnDropDownStyleChanged(e); UpdateItemHeight(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); UpdateItemHeight(); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); UpdateItemHeight(); }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        var fill = selected ? AppTheme.Selection : AppTheme.Input;
        var foreground = !Enabled ? AppTheme.DisabledText : selected ? AppTheme.Accent : AppTheme.Text;
        using var brush = new SolidBrush(fill);
        e.Graphics.FillRectangle(brush, e.Bounds);
        var text = e.Index >= 0 && e.Index < Items.Count ? GetItemText(Items[e.Index]) : Text;
        var bounds = e.Bounds;
        bounds.X += (int)Math.Round(6 * DeviceDpi / 96.0);
        bounds.Width -= (int)Math.Round(10 * DeviceDpi / 96.0);
        TextRenderer.DrawText(e.Graphics, text, Font, bounds, foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if ((e.State & DrawItemState.Focus) != 0 && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(e.Bounds, -2, -2), foreground, fill);
    }
}

// TabControl owner drawing covers tab labels only. The native control still
// paints its unused header area and page frame, which can remain bright white.
// Compose ordinary dark panels and buttons instead so no native tab chrome exists.
internal sealed class ThemeTabs : UserControl
{
    private readonly FlowLayoutPanel strip = new()
    {
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true, BackColor = AppTheme.Background, Margin = Padding.Empty,
        Padding = Padding.Empty, AccessibleRole = AccessibleRole.PageTabList,
        AccessibleName = "Movement details tabs", TabStop = false
    };
    private readonly Panel content = new()
    {
        Dock = DockStyle.Fill, BackColor = AppTheme.Panel, BorderStyle = BorderStyle.None,
        Margin = Padding.Empty, TabStop = false
    };
    private readonly List<(TabButton Button, Panel Page)> pages = [];
    private int selectedIndex = -1;

    internal ThemeTabs()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.ApplyThemingImplicitly, false);
        BorderStyle = BorderStyle.None;
        Padding = new(1);
        BackColor = AppTheme.Background;
        ForeColor = AppTheme.Text;
        TabStop = false;
        Controls.Add(content);
        Controls.Add(strip);
    }

    internal void AddPage(string title, Control child)
    {
        var index = pages.Count;
        var page = new Panel
        {
            Dock = DockStyle.Fill, Padding = new(8), BorderStyle = BorderStyle.None,
            BackColor = AppTheme.Panel, ForeColor = AppTheme.Text, Visible = false,
            AccessibleName = title, AccessibleRole = AccessibleRole.Pane, TabStop = false
        };
        page.Controls.Add(child);
        var button = new TabButton { Text = title, AccessibleName = title, TabIndex = index, TabStop = false };
        button.Click += (_, _) => SelectPage(index, false);
        button.KeyDown += (_, e) =>
        {
            var target = e.KeyCode switch
            {
                Keys.Right or Keys.Down => (index + 1) % pages.Count,
                Keys.Left or Keys.Up => (index - 1 + pages.Count) % pages.Count,
                Keys.Home => 0,
                Keys.End => pages.Count - 1,
                _ => -1
            };
            if (target < 0) return;
            e.Handled = e.SuppressKeyPress = true;
            SelectPage(target, true);
        };
        pages.Add((button, page));
        content.Controls.Add(page);
        strip.Controls.Add(button);
        if (selectedIndex < 0) SelectPage(0, false);
    }

    internal void ShowPage(int index) => SelectPage(index, true);

    private void SelectPage(int index, bool focus)
    {
        if (index < 0 || index >= pages.Count) return;
        content.SuspendLayout();
        selectedIndex = index;
        for (var i = 0; i < pages.Count; i++)
        {
            var selected = i == index;
            var (button, page) = pages[i];
            button.Selected = selected;
            button.TabStop = selected;
            button.AccessibleName = button.Text + (selected ? " (selected)" : "");
            page.Visible = selected;
        }
        pages[index].Page.BringToFront();
        content.ResumeLayout(true);
        if (focus) pages[index].Button.Focus();
        Invalidate();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (pages.Count > 0 && (keyData == (Keys.Control | Keys.Tab) || keyData == (Keys.Control | Keys.Shift | Keys.Tab)))
        {
            var step = (keyData & Keys.Shift) != 0 ? -1 : 1;
            SelectPage((selectedIndex + step + pages.Count) % pages.Count, true);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(AppTheme.Border);
        if (Width > 0 && Height > 0) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    private sealed class TabButton : Button
    {
        private bool selected, hovered;
        [System.ComponentModel.DefaultValue(false)]
        internal bool Selected
        {
            get => selected;
            set { if (selected == value) return; selected = value; Invalidate(); }
        }

        internal TabButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.ApplyThemingImplicitly, false);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            BackColor = AppTheme.Background;
            ForeColor = AppTheme.Text;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            MinimumSize = new(0, 34);
            Padding = new(12, 6, 12, 6);
            Margin = Padding.Empty;
            AccessibleRole = AccessibleRole.PageTab;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var fill = selected ? AppTheme.Panel : hovered ? AppTheme.Raised : AppTheme.Background;
            e.Graphics.Clear(fill);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                selected ? AppTheme.Accent : AppTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (selected)
            {
                using var accent = new SolidBrush(AppTheme.Accent);
                var lineHeight = Math.Max(2, (int)Math.Round(2 * DeviceDpi / 96.0));
                e.Graphics.FillRectangle(accent, 8, Height - lineHeight, Math.Max(0, Width - 16), lineHeight);
            }
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), AppTheme.Muted, fill);
        }

        protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }
}
