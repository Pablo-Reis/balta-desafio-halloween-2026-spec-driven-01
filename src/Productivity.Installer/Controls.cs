using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Productivity.Installer;

/// <summary>Cores e fontes do tema "Aurora" usadas no assistente.</summary>
internal static class Theme
{
    public static readonly Color Back = Color.FromArgb(15, 15, 26);
    public static readonly Color Surface = Color.FromArgb(28, 28, 44);
    public static readonly Color Text = Color.FromArgb(245, 245, 247);
    public static readonly Color Muted = Color.FromArgb(160, 160, 175);
    public static readonly Color Violet = Color.FromArgb(139, 92, 246);
    public static readonly Color Cyan = Color.FromArgb(34, 211, 238);
    public static readonly Color Green = Color.FromArgb(52, 211, 153);
    public static readonly Color Amber = Color.FromArgb(251, 191, 36);
    public static readonly Color Red = Color.FromArgb(248, 113, 113);

    public static Font Body(float size = 10f, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);

    public static Font Title(float size = 18f) => new("Segoe UI Semibold", size);

    public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>Botão arredondado: principal (degradê) ou secundário (vidro).</summary>
internal sealed class AuroraButton : Button
{
    private bool _hover;
    private bool _pressed;

    public AuroraButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
        Font = Theme.Body(10f, FontStyle.Bold);
        Size = new Size(120, 38);
        UseVisualStyleBackColor = false;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Primary { get; set; }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }

    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs mevent) { _pressed = true; Invalidate(); base.OnMouseDown(mevent); }

    protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }

    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new RectangleF(1, 1, Width - 3, Height - 3);
        if (_pressed)
            rect.Inflate(-1, -1);

        using var path = Theme.RoundedRect(rect, 12);
        var alpha = Enabled ? 255 : 90;

        if (Primary)
        {
            using var brush = new LinearGradientBrush(rect, Color.FromArgb(alpha, Theme.Violet), Color.FromArgb(alpha, Theme.Cyan), 25f);
            g.FillPath(brush, path);
            if (_hover && Enabled)
            {
                using var glow = new SolidBrush(Color.FromArgb(36, Color.White));
                g.FillPath(glow, path);
            }
        }
        else
        {
            using var fill = new SolidBrush(Color.FromArgb(_hover && Enabled ? 50 : 30, Color.White));
            using var pen = new Pen(Color.FromArgb(Enabled ? 70 : 30, Color.White));
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        var textColor = Color.FromArgb(Enabled ? 255 : 120, Theme.Text);
        TextRenderer.DrawText(g, Text, Font, Rectangle.Round(rect), textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

/// <summary>Barra de progresso em degradê com cantos arredondados.</summary>
internal sealed class AuroraProgressBar : Control
{
    private double _value;

    public AuroraProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 14;
    }

    /// <summary>Valor de 0 a 1.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 1);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Back);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var track = new RectangleF(0, 0, Width - 1, Height - 1);
        using (var trackPath = Theme.RoundedRect(track, track.Height / 2))
        using (var trackBrush = new SolidBrush(Color.FromArgb(40, Color.White)))
            g.FillPath(trackBrush, trackPath);

        var width = (float)(track.Width * _value);
        if (width < track.Height)
            width = _value <= 0 ? 0 : track.Height;
        if (width <= 0)
            return;

        var fill = new RectangleF(0, 0, width, track.Height);
        using var fillPath = Theme.RoundedRect(fill, fill.Height / 2);
        using var brush = new LinearGradientBrush(new RectangleF(0, 0, Math.Max(width, 2), fill.Height), Theme.Violet, Theme.Cyan, LinearGradientMode.Horizontal);
        g.FillPath(brush, fillPath);
    }
}

/// <summary>Faixa lateral com degradê, brilho suave e logotipo do programa.</summary>
internal sealed class BannerPanel : Panel
{
    public BannerPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string VersionText { get; set; } = string.Empty;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using (var back = new LinearGradientBrush(ClientRectangle, Color.FromArgb(40, 22, 86), Color.FromArgb(12, 12, 24), 70f))
            g.FillRectangle(back, ClientRectangle);

        Glow(g, new RectangleF(-110, -90, 330, 330), Color.FromArgb(150, Theme.Violet));
        Glow(g, new RectangleF(-40, Height - 230, 320, 320), Color.FromArgb(110, Theme.Cyan));

        // Logotipo: quadrado arredondado em degradê com um relógio.
        var logo = new RectangleF(28, 34, 60, 60);
        using (var path = Theme.RoundedRect(logo, 18))
        using (var brush = new LinearGradientBrush(logo, Theme.Violet, Theme.Cyan, 45f))
            g.FillPath(brush, path);

        using (var pen = new Pen(Color.White, 3.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            var c = new PointF(logo.X + 30, logo.Y + 32);
            g.DrawEllipse(pen, c.X - 15, c.Y - 15, 30, 30);
            g.DrawLine(pen, c.X, c.Y, c.X, c.Y - 9);
            g.DrawLine(pen, c.X, c.Y, c.X + 7, c.Y + 4);
            g.DrawLine(pen, c.X - 4, logo.Y + 11, c.X + 4, logo.Y + 11);
        }

        TextRenderer.DrawText(g, "Monitor de\nprodutividade", Theme.Title(15f), new Rectangle(24, 112, Width - 40, 60), Theme.Text,
            TextFormatFlags.Left | TextFormatFlags.Top);
        TextRenderer.DrawText(g, "Cronometre cada demanda\ncom um atalho.", Theme.Body(9f), new Rectangle(26, 172, Width - 40, 44),
            Color.FromArgb(200, 200, 215), TextFormatFlags.Left | TextFormatFlags.Top);

        if (VersionText.Length > 0)
            TextRenderer.DrawText(g, $"Versão {VersionText}", Theme.Body(9f), new Rectangle(26, Height - 38, Width - 40, 22),
                Color.FromArgb(170, 170, 190), TextFormatFlags.Left | TextFormatFlags.Top);
    }

    private static void Glow(Graphics g, RectangleF bounds, Color center)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(bounds);
        using var brush = new PathGradientBrush(path)
        {
            CenterColor = center,
            SurroundColors = new[] { Color.FromArgb(0, center) }
        };
        g.FillPath(brush, path);
    }
}

internal static class Native
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Pinta a barra de título do Windows 10/11 de escuro (ignora versões sem suporte).</summary>
    public static void UseDarkTitleBar(IntPtr handle)
    {
        var enabled = 1;
        _ = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
        _ = DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
    }
}
