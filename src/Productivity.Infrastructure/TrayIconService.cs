using System.Windows.Forms;
using Productivity.Application.Abstractions;
using Productivity.Domain;

namespace Productivity.Infrastructure;

/// <summary>Ícone da bandeja (RF-01) e notificações do Windows (RF-11).</summary>
public sealed class TrayIconService : INotificationSender, IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _startItem = new("Iniciar demanda…");
    private readonly ToolStripMenuItem _pauseItem = new("Pausar");
    private readonly ToolStripMenuItem _finishItem = new("Finalizar demanda");

    public TrayIconService()
    {
        var openItem = new ToolStripMenuItem("Abrir Monitor de produtividade") { Font = new System.Drawing.Font(SystemFonts.MenuFont!, System.Drawing.FontStyle.Bold) };
        var exitItem = new ToolStripMenuItem("Sair");

        openItem.Click += (_, _) => OpenRequested?.Invoke();
        _startItem.Click += (_, _) => StartRequested?.Invoke();
        _pauseItem.Click += (_, _) => PauseResumeRequested?.Invoke();
        _finishItem.Click += (_, _) => FinishRequested?.Invoke();
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[] { openItem, new ToolStripSeparator(), _startItem, _pauseItem, _finishItem, new ToolStripSeparator(), exitItem });

        _icon = new NotifyIcon
        {
            Icon = AppIconFactory.Create(),
            Text = "Monitor de produtividade",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                OpenRequested?.Invoke();
        };

        UpdateState(null);
    }

    public event Action? OpenRequested;
    public event Action? StartRequested;
    public event Action? PauseResumeRequested;
    public event Action? FinishRequested;
    public event Action? ExitRequested;

    public void UpdateState(DemandStatus? status)
    {
        var active = status is DemandStatus.Running or DemandStatus.Paused;
        _pauseItem.Enabled = active;
        _finishItem.Enabled = active;
        _pauseItem.Text = status == DemandStatus.Paused ? "Retomar" : "Pausar";
        _icon.Text = status switch
        {
            DemandStatus.Running => "Monitor de produtividade - trabalhando",
            DemandStatus.Paused => "Monitor de produtividade - em descanso",
            _ => "Monitor de produtividade"
        };
    }

    public void Notify(string title, string message) =>
        _icon.ShowBalloonTip(6000, title, message, ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
