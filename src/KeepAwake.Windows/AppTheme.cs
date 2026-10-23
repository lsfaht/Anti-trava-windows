using System.Drawing.Drawing2D;

namespace KeepAwake.Windows;

internal static class AppTheme
{
    internal static readonly Color Background = Color.FromArgb(18, 20, 23);
    internal static readonly Color Surface = Color.FromArgb(27, 31, 35);
    internal static readonly Color Card = Color.FromArgb(36, 42, 48);
    internal static readonly Color Border = Color.FromArgb(52, 59, 67);
    internal static readonly Color Selected = Color.FromArgb(36, 70, 54);
    internal static readonly Color Hover = Color.FromArgb(48, 56, 64);
    internal static readonly Color Disabled = Color.FromArgb(58, 64, 71);
    internal static readonly Color Green = Color.FromArgb(25, 135, 84);
    internal static readonly Color GreenLight = Color.FromArgb(47, 191, 113);
    internal static readonly Color Text = Color.FromArgb(241, 243, 244);
    internal static readonly Color Muted = Color.FromArgb(174, 183, 194);
    internal static readonly Color Warning = Color.FromArgb(229, 168, 59);
    internal static readonly Color Error = Color.FromArgb(227, 93, 106);

    internal static void Apply(Control control)
    {
        foreach (Control child in control.Controls)
        {
            switch (child)
            {
                case Button button:
                    if (button.FlatStyle != FlatStyle.Flat) StyleButton(button);
                    break;
                case TextBox textBox:
                    textBox.BackColor = Surface;
                    textBox.ForeColor = Text;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case RichTextBox richTextBox:
                    richTextBox.BackColor = Surface;
                    richTextBox.ForeColor = Text;
                    richTextBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox comboBox:
                    comboBox.BackColor = Surface;
                    comboBox.ForeColor = Text;
                    comboBox.FlatStyle = FlatStyle.Flat;
                    break;
                case CheckBox checkBox:
                    checkBox.BackColor = Card;
                    checkBox.ForeColor = Text;
                    checkBox.FlatStyle = FlatStyle.Standard;
                    break;
                case ListBox listBox:
                    listBox.BackColor = Surface;
                    listBox.ForeColor = Text;
                    listBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ListView listView:
                    listView.BackColor = Surface;
                    listView.ForeColor = Text;
                    listView.BorderStyle = BorderStyle.FixedSingle;
                    listView.FullRowSelect = true;
                    StyleListView(listView);
                    break;
                case ProgressBar:
                    break;
                case Label label:
                    label.ForeColor = label.ForeColor == SystemColors.ControlText ? Text : label.ForeColor;
                    label.BackColor = Color.Transparent;
                    break;
                default:
                    if (child is CardPanel) child.BackColor = Card;
                    else if (child is Panel or TableLayoutPanel or FlowLayoutPanel) child.BackColor = control is CardPanel ? Card : Background;
                    child.ForeColor = Text;
                    break;
            }
            Apply(child);
        }
    }

    internal static void StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseDownBackColor = Green;
        button.FlatAppearance.MouseOverBackColor = primary ? GreenLight : Hover;
        button.BackColor = primary ? Green : Surface;
        button.ForeColor = Text;
        button.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
        button.Cursor = Cursors.Hand;
        button.Padding = new Padding(12, 6, 12, 6);
        button.MinimumSize = new Size(120, 38);
        button.UseVisualStyleBackColor = false;
    }

    private static void StyleListView(ListView listView)
    {
        if (listView.OwnerDraw) return;
        listView.OwnerDraw = true;
        listView.DrawColumnHeader += (_, e) =>
        {
            using var brush = new SolidBrush(Card);
            using var font = new Font("Segoe UI Semibold", 9);
            e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? string.Empty, font, e.Bounds, Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.LeftAndRightPadding);
        };
        listView.DrawItem += (_, e) =>
        {
            if (listView.View != View.Details) e.DrawDefault = true;
        };
        listView.DrawSubItem += (_, e) =>
        {
            var selected = e.Item?.Selected == true;
            using var brush = new SolidBrush(selected ? Selected : Surface);
            e.Graphics.FillRectangle(brush, e.Bounds);
            var color = selected ? GreenLight : Text;
            TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? string.Empty, listView.Font, e.Bounds, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.LeftAndRightPadding);
        };
    }

    internal static Label CreateLabel(string text, float size = 9.5F, bool bold = false, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = color ?? Text,
        Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size, FontStyle.Regular, GraphicsUnit.Point),
        BackColor = Color.Transparent,
        Margin = new Padding(0, 3, 0, 3)
    };

    internal static CardPanel CreateCard() => new();
}

internal sealed class CardPanel : Panel
{
    internal Panel? BodyHost { get; set; }

    internal CardPanel()
    {
        BackColor = AppTheme.Card;
        ForeColor = AppTheme.Text;
        Padding = new Padding(16);
        Margin = new Padding(0, 0, 0, 14);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.None;
        using var pen = new Pen(AppTheme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
    }
}
