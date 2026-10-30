using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Productivity.Infrastructure;

/// <summary>Desenha o ícone do app (relógio) sem depender de arquivo de imagem.</summary>
public static class AppIconFactory
{
    public static Icon Create(int size = 32, Color? accent = null)
    {
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var pad = size / 16f;
            var rect = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);
            using (var fill = new SolidBrush(accent ?? Color.FromArgb(0x2B, 0x6C, 0xE0)))
                g.FillEllipse(fill, rect);

            var center = size / 2f;
            using var pen = new Pen(Color.White, Math.Max(1.5f, size / 11f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, center, center, center, size * 0.22f);
            g.DrawLine(pen, center, center, size * 0.72f, size * 0.58f);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
