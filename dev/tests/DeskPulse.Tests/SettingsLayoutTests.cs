using System.Drawing;
using System.Windows.Forms;
using DeskPulse;
using Xunit;

namespace DeskPulse.Tests;

public sealed class SettingsLayoutTests
{
    [Fact]
    public void SettingsFormsRemainScrollableAtCompactSizes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                RenderAndVerify(administratorMode: false, "current-user");
                RenderAndVerify(administratorMode: true, "administrator");
                foreach (var scale in new[] { 1.25f, 1.5f, 2f })
                {
                    RenderScaledAndVerify(administratorMode: false, "current-user", scale);
                    RenderScaledAndVerify(administratorMode: true, "administrator", scale);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }

    private static void RenderScaledAndVerify(bool administratorMode, string name, float scale)
    {
        using var form = new SettingsForm(administratorMode);
        form.Scale(new SizeF(scale, scale));
        form.Size = new Size(920, 720);
        form.Show();
        form.PerformLayout();
        Application.DoEvents();

        Assert.True(form.AutoScroll);
        Assert.All(
            Descendants(form).OfType<TabPage>(),
            page => Assert.True(page.AutoScroll));

        var artifactFolder = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            @"..\..\..\..\..\test-artifacts\layout"));
        Directory.CreateDirectory(artifactFolder);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(
            artifactFolder,
            $"{name}-scale-{scale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}.png"));
        form.Close();
    }

    private static void RenderAndVerify(bool administratorMode, string name)
    {
        using var form = new SettingsForm(administratorMode);
        form.Show();
        Application.DoEvents();

        Assert.True(form.AutoScroll);
        Assert.True(form.AutoScrollMinSize.Width > 0);
        Assert.True(form.AutoScrollMinSize.Height > 0);
        Assert.All(
            Descendants(form).OfType<TabPage>(),
            page => Assert.True(page.AutoScroll));

        var artifactFolder = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            @"..\..\..\..\..\test-artifacts\layout"));
        Directory.CreateDirectory(artifactFolder);

        foreach (var size in new[]
                 {
                     new Size(920, 720),
                     new Size(760, 560),
                     new Size(620, 460)
                 })
        {
            form.Size = size;
            form.PerformLayout();
            Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(artifactFolder, $"{name}-{size.Width}x{size.Height}.png"));
        }

        form.Close();
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
