namespace MouseMotionInspector.Native;

internal static class GuidePreview
{
    // Renders the actual controls without opening the capture window.
    internal static void Render(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var size in new[] { new Size(1240, 360), new Size(1000, 300) })
        {
            using (var about = AboutPage.Create())
            {
                about.Dock = DockStyle.None; about.Size = size; about.CreateControl(); about.PerformLayout();
                using var bitmap = new Bitmap(size.Width, size.Height); about.DrawToBitmap(bitmap, new(Point.Empty, size));
                bitmap.Save(Path.Combine(directory, $"about-{size.Width}.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            using var guide = new InterpretationGuide(() => { }) { Dock = DockStyle.None, Size = size };
            guide.CreateControl();
            for (var step = 0; step < 4; step++)
            {
                guide.ShowStep(step);
                for (var example = 0; example < (step == 2 ? 3 : 1); example++)
                {
                    if (step == 2) guide.ShowExample(example);
                    guide.PerformLayout();
                    using var bitmap = new Bitmap(size.Width, size.Height);
                    guide.DrawToBitmap(bitmap, new(Point.Empty, size));
                    bitmap.Save(Path.Combine(directory, $"guide-{size.Width}-step-{step + 1}-{example}.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }
        using var dialog = GuideReference.Create();
        // Draw its client controls in a hidden standalone host: DrawToBitmap on
        // an unshown top-level Form otherwise omits its non-visible children.
        using var host = new UserControl { Size = dialog.ClientSize, Font = dialog.Font, BackColor = dialog.BackColor };
        var layout = dialog.Controls[0]; host.Controls.Add(layout); host.CreateControl();
        var choose = layout.Controls.OfType<ComboBox>().Single();
        for (var topic = 0; topic < choose.Items.Count; topic++)
        {
            choose.SelectedIndex = topic; host.PerformLayout();
            using var bitmap = new Bitmap(host.Width, host.Height);
            host.DrawToBitmap(bitmap, new(Point.Empty, host.Size));
            bitmap.Save(Path.Combine(directory, $"help-{topic + 1}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
