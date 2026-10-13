using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MouseMotionInspector.Native;

internal static class AboutPage
{
    internal const string Repository = "https://github.com/Almo7aya/mouse-motion-inspector";
    internal static string Version => typeof(AboutPage).Assembly.GetName().Version?.ToString(3) ?? "Unknown";

    internal static Control Create()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.Background, AccessibleName = "About Mouse Motion Inspector" };
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new(18), ColumnCount = 2 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 55)); layout.ColumnStyles.Add(new(SizeType.Percent, 45));
        var left = Column(); var right = Column(); right.Margin = new(22, 0, 0, 0);
        left.Controls.Add(Text("Mouse Motion Inspector", 20, AppTheme.Text, true));
        left.Controls.Add(Text($"Version {Version}   ·   Windows x64   ·   MIT license", 10, AppTheme.Accent));
        left.Controls.Add(Text("Find the source of unexpected pointer movement.\nCompare hands-off tests across surfaces, DPI and connections.", 10, AppTheme.Text));
        left.Controls.Add(Text("Created by Almo7aya and contributors.", 10, AppTheme.Muted));
        var links = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Margin = new(0, 8, 0, 0) };
        foreach (var (label, path) in new[] { ("GitHub", ""), ("Releases", "/releases"), ("Report a bug", "/issues/new?template=bug_report.yml"), ("MIT license", "/blob/main/LICENSE") })
        {
            var button = new ThemeButton { Text = label, AutoSize = true, MinimumSize = new(0, 34), Padding = new(10, 4, 10, 4), Margin = new(0, 0, 6, 6), AccessibleDescription = "Opens in your default browser." };
            var url = Repository + path; button.Click += (_, _) => OpenLink(scroll, url); links.Controls.Add(button);
        }
        left.Controls.Add(links);
        right.Controls.Add(Text("Local by design", 12, AppTheme.Cyan, true));
        right.Controls.Add(Text("No accounts, telemetry or automatic uploads. No keystroke recording, drivers or mouse-setting changes. Project links open in your browser only when clicked.", 10, AppTheme.Text));
        right.Controls.Add(Text("Recordings", 11, AppTheme.Cyan, true));
        right.Controls.Add(Text("%LOCALAPPDATA%\\MouseMotionInspector\\recordings\nReview device identifiers and notes before sharing exports.", 9, AppTheme.Muted));
        right.Controls.Add(Text("Independent project; not affiliated with Razer or any manufacturer. Raw Input identifies Windows interfaces; it does not certify sensor health.", 9, AppTheme.Muted));
        right.Controls.Add(Text($"Running on {RuntimeInformation.FrameworkDescription}", 9, AppTheme.Muted));
        layout.Controls.Add(left, 0, 0); layout.Controls.Add(right, 1, 0); scroll.Controls.Add(layout);
        return scroll;
    }

    private static TableLayoutPanel Column()
    {
        var column = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Margin = Padding.Empty };
        column.ColumnStyles.Add(new(SizeType.Percent, 100));
        column.SizeChanged += (_, _) =>
        {
            foreach (var label in column.Controls.OfType<Label>())
                label.MaximumSize = new(Math.Max(100, column.ClientSize.Width), 0);
        };
        return column;
    }
    private static Label Text(string text, float size, Color color, bool bold = false) => new()
    {
        Text = text, AutoSize = true, MaximumSize = new(420, 0), UseMnemonic = false,
        Font = new("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color, Margin = new(0, 0, 0, 12)
    };
    private static void OpenLink(IWin32Window owner, string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception e) { MessageBox.Show(owner, "Could not open the browser.\n\n" + url + "\n\n" + e.Message, "Open link", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }
}
