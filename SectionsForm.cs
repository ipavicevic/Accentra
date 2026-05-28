using SkiaSharp;
using Svg.Skia;

namespace Accentra;

class SectionsForm : Form
{
    public SectionsForm()
    {
        Text = "Accentra";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(300, 0);

        var iconBox = new PictureBox
        {
            Image = LoadSvgLogo(48),
            SizeMode = PictureBoxSizeMode.Normal,
            Left = 16,
            Top = 16,
            Width = 48,
            Height = 48,
        };
        Controls.Add(iconBox);

        var titleLabel = new Label
        {
            Text = "Accent Sections",
            Left = 76,
            Top = 16,
            Width = ClientSize.Width - 32,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true,
        };
        Controls.Add(titleLabel);

        var subLabel = new Label
        {
            Text = "Choose which accent sections are active.",
            Left = 76,
            Top = 40,
            Width = ClientSize.Width - 92,
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 9f),
            AutoSize = true,
        };
        Controls.Add(subLabel);

        int sepY = 68;
        Controls.Add(new Panel
        {
            Left = 0,
            Top = sepY,
            Width = ClientSize.Width,
            Height = 1,
            BackColor = SystemColors.ControlLight,
        });

        int y = sepY + 14;
        var sections = AccentMaps.GetSections();
        var checkBoxes = new List<CheckBox>();
        foreach (var (name, enabled) in sections)
        {
            var cb = new CheckBox
            {
                Text = char.ToUpper(name[0]) + name[1..],
                Checked = enabled,
                Left = 16,
                Top = y,
                Width = ClientSize.Width - 32,
                Font = new Font("Segoe UI", 9f),
            };
            checkBoxes.Add(cb);
            Controls.Add(cb);
            y += 26;
        }

        y += 8;
        Controls.Add(new Panel
        {
            Left = 0,
            Top = y,
            Width = ClientSize.Width,
            Height = 1,
            BackColor = SystemColors.ControlLight,
        });
        y += 12;

        var btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Left = ClientSize.Width - 176,
            Top = y,
            Width = 75,
        };
        var btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = ClientSize.Width - 92,
            Top = y,
            Width = 75,
        };

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        Controls.Add(btnOk);
        Controls.Add(btnCancel);

        ClientSize = new Size(ClientSize.Width, y + btnOk.Height + 16);

        btnOk.Click += (_, _) =>
        {
            var newSections = AccentMaps.GetSections();
            for (int i = 0; i < newSections.Count; i++)
            {
                if (checkBoxes[i].Checked != newSections[i].Enabled)
                    AccentMaps.ToggleSection(newSections[i].Name);
            }
        };
    }

    private static Bitmap? LoadSvgLogo(int size)
    {
        using var stream = typeof(SectionsForm).Assembly.GetManifestResourceStream("Accentra.logo.svg");
        if (stream is null) return null;

        var svg = new SKSvg();
        svg.Load(stream);
        if (svg.Picture is null) return null;

        var info = new SKImageInfo(size, size);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var bounds = svg.Picture.CullRect;
        float scale = size / Math.Max(bounds.Width, bounds.Height);
        canvas.Scale(scale);
        canvas.DrawPicture(svg.Picture);

        using var skImage = surface.Snapshot();
        using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
        using var ms = new System.IO.MemoryStream(data.ToArray());
        return new Bitmap(ms);
    }
}
