namespace Accentra;

class SectionReferenceForm : Form
{
    public SectionReferenceForm(string sectionName, IReadOnlyDictionary<char, char[]> maps)
    {
        Text = $"Accentra — {char.ToUpper(sectionName[0]) + sectionName[1..]}";
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(280, 200);
        ClientSize = new Size(320, 400);

        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12),
        };
        Controls.Add(panel);

        int y = 0;
        foreach (var (baseChar, variants) in maps.OrderBy(kv => kv.Key))
        {
            var display = variants.Length > 0 && variants[^1] == baseChar
                ? variants[..^1]
                : variants;
            var row = new Label
            {
                Text = $"{baseChar}  →  {string.Join("  ", display)}",
                Left = 0,
                Top = y,
                Width = panel.ClientSize.Width - 24,
                Font = new Font("Segoe UI", 10f),
                AutoSize = true,
            };
            panel.Controls.Add(row);
            y += 28;
        }
    }
}
