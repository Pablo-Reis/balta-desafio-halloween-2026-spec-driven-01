using System.Drawing.Drawing2D;
using Productivity.Setup.Core;

namespace Productivity.Installer;

/// <summary>Assistente: Bem-vindo, Pasta, Pronto para instalar, Progresso e Conclusão.</summary>
internal sealed class SetupForm : Form
{
    private enum Step { Welcome, Location, Ready, Progress, Finish }

    private const int BannerWidth = 210;
    private const int BarHeight = 70;

    private readonly SetupArguments _args;
    private readonly InstalledInfo? _installed;
    private readonly InstallKind _kind;
    private readonly long _payloadBytes;
    private readonly InstallEngine _engine = new();

    private readonly Dictionary<Step, Panel> _pages = new();
    private readonly BannerPanel _banner = new();
    private readonly AuroraButton _back = new() { Text = "Voltar" };
    private readonly AuroraButton _next = new() { Text = "Próximo", Primary = true };
    private readonly AuroraButton _cancel = new() { Text = "Cancelar" };

    private readonly TextBox _dirBox = new();
    private readonly AuroraButton _browse = new() { Text = "Procurar…", Size = new Size(110, 32) };
    private readonly CheckBox _desktopCheck = new() { Text = "Criar atalho na Área de Trabalho", Checked = true };
    private readonly Label _locationError = new();
    private readonly Label _readyBody = new();
    private readonly Label _readyNote = new();
    private readonly Label _status = new();
    private readonly Label _percent = new();
    private readonly Label _detail = new();
    private readonly AuroraProgressBar _bar = new();
    private readonly Label _finishTitle = new();
    private readonly Label _finishBody = new();
    private readonly Panel _badge = new();

    private Step _step = Step.Welcome;
    private CancellationTokenSource? _cts;
    private bool _installing;
    private bool _closing;
    private bool _succeeded;
    private bool _finishedWithError;
    private string _installedExe = string.Empty;

    public SetupForm(SetupArguments args, InstalledInfo? installed, InstallKind kind, string defaultDirectory, long payloadBytes)
    {
        _args = args;
        _installed = installed;
        _kind = kind;
        _payloadBytes = payloadBytes;

        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Text = $"Instalador - {Branding.ProductName}";
        ClientSize = new Size(720, 490);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Back;
        ForeColor = Theme.Text;
        Font = Theme.Body();
        DoubleBuffered = true;
        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch (Exception)
        {
            // Sem ícone associado: usa o padrão do Windows.
        }

        _banner.SetBounds(0, 0, BannerWidth, ClientSize.Height);
        _banner.VersionText = Branding.Version;
        Controls.Add(_banner);

        BuildWelcome();
        BuildLocation(defaultDirectory);
        BuildReady();
        BuildProgress();
        BuildFinish();

        foreach (var button in new[] { _back, _next, _cancel })
            Controls.Add(button);
        _back.Click += (_, _) => GoBack();
        _next.Click += async (_, _) => await GoNextAsync();
        _cancel.Click += (_, _) => OnCancelClicked();
        FormClosing += OnFormClosing;
        Shown += async (_, _) =>
        {
            Native.UseDarkTitleBar(Handle);
            if (_args.Resume)
            {
                _dirBox.Text = defaultDirectory;
                _desktopCheck.Checked = !_args.NoDesktop;
                await StartInstallAsync();
            }
        };

        ShowStep(Step.Welcome);
    }

    // ---- Construção das páginas -------------------------------------------------------------------------------

    private Panel NewPage(Step step)
    {
        var panel = new Panel
        {
            Bounds = new Rectangle(BannerWidth, 0, ClientSize.Width - BannerWidth, ClientSize.Height - BarHeight),
            BackColor = Theme.Back,
            Visible = false
        };
        _pages[step] = panel;
        Controls.Add(panel);
        return panel;
    }

    private static Label NewLabel(string text, Font font, Color color, Rectangle bounds) => new()
    {
        Text = text,
        Font = font,
        ForeColor = color,
        BackColor = Color.Transparent,
        Bounds = bounds,
        AutoSize = false
    };

    private int PageWidth => ClientSize.Width - BannerWidth;

    private void BuildWelcome()
    {
        var page = NewPage(Step.Welcome);
        var (title, body) = WelcomeText();
        page.Controls.Add(NewLabel(title, Theme.Title(17f), Theme.Text, new Rectangle(30, 34, PageWidth - 60, 70)));
        page.Controls.Add(NewLabel(body, Theme.Body(10f), Color.FromArgb(205, 205, 218), new Rectangle(30, 112, PageWidth - 60, 250)));
    }

    private (string Title, string Body) WelcomeText()
    {
        var current = Branding.Version;
        return _kind switch
        {
            InstallKind.Update => (
                $"Atualizar o {Branding.ProductName}",
                $"Foi encontrada a versão {_installed!.Version} instalada em:\n{_installed.Directory}\n\nEste assistente vai atualizá-la para a versão {current}.\n\n" +
                "Seus dados (demandas, anotações e configurações) serão mantidos. Se o programa estiver aberto, ele será fechado durante a atualização.\n\nClique em Próximo para continuar."),
            InstallKind.Reinstall => (
                $"Reinstalar o {Branding.ProductName}",
                $"A versão {current} já está instalada em:\n{_installed!.Directory}\n\nVocê pode reinstalar para reparar arquivos danificados. Seus dados serão mantidos.\n\nClique em Próximo para continuar."),
            InstallKind.Downgrade => (
                $"Instalar uma versão anterior",
                $"A versão instalada ({_installed!.Version}) é mais nova que a deste instalador ({current}).\n\nContinuar vai substituí-la pela versão {current}. Seus dados serão mantidos.\n\nClique em Próximo para continuar."),
            _ => (
                $"Bem-vindo ao {Branding.ProductName}",
                "Este assistente vai instalar o programa no seu computador.\n\nCronometre o tempo de cada demanda com atalhos de teclado, controle pausas e descansos e acompanhe seu histórico e seus indicadores dos últimos 30 dias.\n\nClique em Próximo para continuar.")
        };
    }

    private void BuildLocation(string defaultDirectory)
    {
        var page = NewPage(Step.Location);
        page.Controls.Add(NewLabel("Pasta de instalação", Theme.Title(17f), Theme.Text, new Rectangle(30, 34, PageWidth - 60, 40)));
        page.Controls.Add(NewLabel("O programa será instalado na pasta abaixo. Para instalar em outra pasta, clique em Procurar.",
            Theme.Body(10f), Color.FromArgb(205, 205, 218), new Rectangle(30, 84, PageWidth - 60, 44)));

        _dirBox.SetBounds(30, 140, PageWidth - 60 - 118, 30);
        _dirBox.Text = defaultDirectory;
        _dirBox.BackColor = Theme.Surface;
        _dirBox.ForeColor = Theme.Text;
        _dirBox.BorderStyle = BorderStyle.FixedSingle;
        _dirBox.Font = Theme.Body(10f);
        _dirBox.AccessibleName = "Pasta de instalação";
        _dirBox.TextChanged += (_, _) => _locationError.Text = string.Empty;
        page.Controls.Add(_dirBox);

        _browse.Location = new Point(PageWidth - 30 - 110, 139);
        _browse.Click += (_, _) => BrowseForFolder();
        page.Controls.Add(_browse);

        _locationError.Font = Theme.Body(9.5f);
        _locationError.ForeColor = Theme.Red;
        _locationError.Bounds = new Rectangle(30, 176, PageWidth - 60, 40);
        page.Controls.Add(_locationError);

        var needed = Math.Max(1, _payloadBytes / (1024 * 1024));
        page.Controls.Add(NewLabel($"Espaço em disco necessário: cerca de {needed} MB.", Theme.Body(9.5f), Theme.Muted,
            new Rectangle(30, 222, PageWidth - 60, 24)));

        _desktopCheck.Bounds = new Rectangle(30, 268, PageWidth - 60, 28);
        _desktopCheck.ForeColor = Theme.Text;
        _desktopCheck.BackColor = Color.Transparent;
        _desktopCheck.Font = Theme.Body(10f);
        page.Controls.Add(_desktopCheck);

        page.Controls.Add(NewLabel("Um atalho no Menu Iniciar é sempre criado.", Theme.Body(9.5f), Theme.Muted,
            new Rectangle(52, 298, PageWidth - 82, 24)));

        if (_installed is not null)
        {
            // Atualização/reinstalação mantém o local para não deixar uma cópia antiga para trás.
            _dirBox.Text = _installed.Directory;
            _dirBox.Enabled = false;
            _browse.Enabled = false;
        }
    }

    private void BuildReady()
    {
        var page = NewPage(Step.Ready);
        page.Controls.Add(NewLabel("Pronto para instalar", Theme.Title(17f), Theme.Text, new Rectangle(30, 34, PageWidth - 60, 40)));

        _readyBody.Font = Theme.Body(10f);
        _readyBody.ForeColor = Color.FromArgb(205, 205, 218);
        _readyBody.Bounds = new Rectangle(30, 92, PageWidth - 60, 170);
        page.Controls.Add(_readyBody);

        _readyNote.Font = Theme.Body(9.5f);
        _readyNote.ForeColor = Theme.Amber;
        _readyNote.Bounds = new Rectangle(30, 276, PageWidth - 60, 60);
        page.Controls.Add(_readyNote);
    }

    private void BuildProgress()
    {
        var page = NewPage(Step.Progress);
        page.Controls.Add(NewLabel(_kind == InstallKind.Update ? "Atualizando…" : "Instalando…", Theme.Title(17f), Theme.Text,
            new Rectangle(30, 34, PageWidth - 60, 40)));

        _status.Font = Theme.Body(10.5f);
        _status.ForeColor = Theme.Text;
        _status.Bounds = new Rectangle(30, 120, PageWidth - 60, 26);
        _status.Text = "Preparando…";
        page.Controls.Add(_status);

        _bar.Bounds = new Rectangle(30, 156, PageWidth - 60, 14);
        page.Controls.Add(_bar);

        _percent.Font = Theme.Body(9.5f, FontStyle.Bold);
        _percent.ForeColor = Theme.Cyan;
        _percent.Bounds = new Rectangle(30, 180, 90, 22);
        _percent.Text = "0%";
        page.Controls.Add(_percent);

        _detail.Font = Theme.Body(9f);
        _detail.ForeColor = Theme.Muted;
        _detail.AutoEllipsis = true;
        _detail.Bounds = new Rectangle(30, 208, PageWidth - 60, 22);
        page.Controls.Add(_detail);
    }

    private void BuildFinish()
    {
        var page = NewPage(Step.Finish);

        _badge.Bounds = new Rectangle(30, 34, 56, 56);
        _badge.BackColor = Theme.Back;
        _badge.Paint += PaintBadge;
        page.Controls.Add(_badge);

        _finishTitle.Font = Theme.Title(17f);
        _finishTitle.ForeColor = Theme.Text;
        _finishTitle.Bounds = new Rectangle(100, 40, PageWidth - 130, 44);
        page.Controls.Add(_finishTitle);

        _finishBody.Font = Theme.Body(10f);
        _finishBody.ForeColor = Color.FromArgb(205, 205, 218);
        _finishBody.Bounds = new Rectangle(30, 116, PageWidth - 60, 220);
        page.Controls.Add(_finishBody);
    }

    private void PaintBadge(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var color = _succeeded ? Theme.Green : _finishedWithError ? Theme.Red : Theme.Amber;
        var area = new Rectangle(1, 1, _badge.Width - 3, _badge.Height - 3);

        using (var fill = new SolidBrush(Color.FromArgb(40, color)))
        using (var ring = new Pen(color, 2.5f))
        {
            g.FillEllipse(fill, area);
            g.DrawEllipse(ring, area);
        }

        using var pen = new Pen(color, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        if (_succeeded)
        {
            g.DrawLines(pen, new[] { new PointF(16, 29), new PointF(25, 38), new PointF(41, 19) });
        }
        else if (_finishedWithError)
        {
            g.DrawLine(pen, 19, 19, 37, 37);
            g.DrawLine(pen, 37, 19, 19, 37);
        }
        else
        {
            g.DrawLine(pen, 28, 16, 28, 31);
            g.DrawLine(pen, 28, 39, 28, 39.5f);
        }
    }

    // ---- Navegação --------------------------------------------------------------------------------------------

    private void ShowStep(Step step)
    {
        _step = step;
        foreach (var (key, page) in _pages)
            page.Visible = key == step;

        switch (step)
        {
            case Step.Welcome:
                SetButtons(back: false, next: "Próximo", cancel: "Cancelar");
                break;
            case Step.Location:
                SetButtons(back: true, next: "Próximo", cancel: "Cancelar");
                break;
            case Step.Ready:
                FillReady();
                SetButtons(back: true, next: "Instalar", cancel: "Cancelar");
                break;
            case Step.Progress:
                SetButtons(back: false, next: null, cancel: "Cancelar");
                break;
            case Step.Finish:
                SetButtons(back: false, next: _succeeded ? "Abrir programa" : null, cancel: "Fechar");
                break;
        }
    }

    private void SetButtons(bool back, string? next, string cancel)
    {
        const int margin = 24;
        const int gap = 10;
        var y = ClientSize.Height - BarHeight + (BarHeight - _next.Height) / 2;

        _cancel.Text = cancel;
        _cancel.Size = new Size(110, 38);
        _cancel.Location = new Point(ClientSize.Width - margin - _cancel.Width, y);

        _next.Visible = next is not null;
        if (next is not null)
        {
            _next.Text = next;
            _next.Size = new Size(next.Length > 10 ? 160 : 120, 38);
            _next.Location = new Point(_cancel.Left - gap - _next.Width, y);
        }

        _back.Visible = back;
        _back.Size = new Size(110, 38);
        _back.Location = new Point((next is not null ? _next.Left : _cancel.Left) - gap - _back.Width, y);

        AcceptButton = next is not null ? _next : _cancel;
    }

    private void GoBack()
    {
        if (_step == Step.Location)
            ShowStep(Step.Welcome);
        else if (_step == Step.Ready)
            ShowStep(Step.Location);
    }

    private async Task GoNextAsync()
    {
        switch (_step)
        {
            case Step.Welcome:
                ShowStep(Step.Location);
                break;
            case Step.Location:
                if (ValidateLocation())
                    ShowStep(Step.Ready);
                break;
            case Step.Ready:
                await StartInstallAsync();
                break;
            case Step.Finish:
                LaunchAndClose();
                break;
        }
    }

    private bool ValidateLocation()
    {
        var path = _dirBox.Text.Trim();
        var error = InstallPathRules.Validate(path);

        if (error is null)
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(path))!;
                var free = new DriveInfo(root).AvailableFreeSpace;
                // A atualização guarda a versão antiga até terminar, por isso reserva o dobro.
                var required = _payloadBytes * 2;
                if (free < required)
                    error = $"Espaço em disco insuficiente: são necessários cerca de {required / (1024 * 1024)} MB livres em {root}.";
            }
            catch (Exception)
            {
                // Unidade de rede ou sem informação de espaço: a cópia acusa o problema se houver.
            }
        }

        _locationError.Text = error ?? string.Empty;
        return error is null;
    }

    private void BrowseForFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Escolha a pasta onde o programa será instalado",
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(_dirBox.Text) ? _dirBox.Text : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var chosen = dialog.SelectedPath;
            // Escolher uma pasta existente qualquer instalaria misturado com outros arquivos: usa uma subpasta própria.
            if (Directory.Exists(chosen) && Directory.EnumerateFileSystemEntries(chosen).Any()
                && !File.Exists(Path.Combine(chosen, Branding.ExeName)))
                chosen = Path.Combine(chosen, Branding.ProductName);
            _dirBox.Text = chosen;
        }
    }

    private void FillReady()
    {
        var directory = _dirBox.Text.Trim();
        var action = _kind == InstallKind.Update ? "atualizado" : "instalado";
        _readyBody.Text =
            $"O {Branding.ProductName} {Branding.Version} será {action} com estas opções:\n\n" +
            $"Pasta:   {directory}\n" +
            $"Atalhos:   Menu Iniciar{(_desktopCheck.Checked ? " e Área de Trabalho" : string.Empty)}\n" +
            "Dados:   mantidos em %LOCALAPPDATA%\\ProductivityMonitor\n\n" +
            "Clique em Instalar para começar.";

        _readyNote.Text = Elevation.NeedsElevation(directory)
            ? "O Windows vai pedir permissão de administrador para gravar nesta pasta."
            : string.Empty;
    }

    // ---- Instalação -------------------------------------------------------------------------------------------

    private async Task StartInstallAsync()
    {
        var directory = Path.GetFullPath(_dirBox.Text.Trim());

        if (Elevation.NeedsElevation(directory))
        {
            var arguments = new List<string> { "/resume", $"/dir={directory}" };
            if (!_desktopCheck.Checked)
                arguments.Add("/nodesktop");

            if (Elevation.RelaunchElevated(arguments))
            {
                _closing = true;
                Close();
            }
            else
            {
                _readyNote.Text = "A permissão de administrador foi negada. Ela é necessária para instalar nesta pasta; escolha outra pasta ou tente de novo.";
            }

            return;
        }

        ShowStep(Step.Progress);
        _installing = true;
        _cts = new CancellationTokenSource();
        var progress = new Progress<InstallProgress>(OnProgress);

        try
        {
            await Task.Run(() => _engine.InstallAsync(new InstallRequest(directory, _desktopCheck.Checked), progress, _cts.Token));
            _installedExe = Path.Combine(directory, Branding.ExeName);
            Finish(success: true, directory);
        }
        catch (OperationCanceledException)
        {
            Finish(success: false, directory, cancelled: true);
        }
        catch (Exception ex)
        {
            Logging.Write(ex);
            Finish(success: false, directory, error: ex.Message);
        }
        finally
        {
            _installing = false;
        }
    }

    private void OnProgress(InstallProgress progress)
    {
        _bar.Value = progress.Fraction;
        _percent.Text = progress.Fraction.ToString("P0");
        _status.Text = progress.Status;
        _detail.Text = progress.Detail.Replace('/', '\\');
    }

    private void Finish(bool success, string directory, bool cancelled = false, string? error = null)
    {
        _succeeded = success;
        _finishedWithError = !success && !cancelled;

        if (success)
        {
            _finishTitle.Text = _kind == InstallKind.Update ? "Atualização concluída" : "Instalação concluída";
            _finishBody.Text =
                $"O {Branding.ProductName} {Branding.Version} foi instalado em:\n{directory}\n\n" +
                "O programa fica na bandeja do Windows, perto do relógio. Use Ctrl+Alt+S para iniciar uma demanda.\n\n" +
                "Clique em Abrir programa para começar agora ou em Fechar para sair do instalador.";
        }
        else if (cancelled)
        {
            _finishTitle.Text = "Instalação cancelada";
            _finishBody.Text = "A instalação foi cancelada e nenhuma alteração foi mantida.\n\nVocê pode executar o instalador novamente quando quiser.";
        }
        else
        {
            _finishTitle.Text = "Não foi possível concluir";
            _finishBody.Text =
                $"Ocorreu um erro durante a instalação:\n{error}\n\nNenhuma alteração foi mantida; se havia uma versão instalada, ela continua como estava.\n" +
                $"Detalhes técnicos foram salvos em:\n{Logging.Path}";
        }

        _badge.Invalidate();
        ShowStep(Step.Finish);
    }

    private void LaunchAndClose()
    {
        try
        {
            Elevation.LaunchApp(_installedExe);
        }
        catch (Exception ex)
        {
            Logging.Write(ex);
            MessageBox.Show(this, "A instalação terminou, mas não foi possível abrir o programa agora. Abra-o pelo Menu Iniciar.",
                Branding.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        _closing = true;
        Close();
    }

    // ---- Cancelar e fechar ------------------------------------------------------------------------------------

    private void OnCancelClicked()
    {
        if (_step == Step.Finish)
        {
            _closing = true;
            Close();
            return;
        }

        if (!ConfirmCancel())
            return;

        if (_installing)
            _cts?.Cancel(); // a instalação desfaz o que copiou e mostra a página de cancelamento
        else
        {
            _closing = true;
            Close();
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closing || _step == Step.Finish)
            return;

        e.Cancel = true;
        if (ConfirmCancel())
        {
            if (_installing)
                _cts?.Cancel();
            else
            {
                _closing = true;
                Close();
            }
        }
    }

    private bool ConfirmCancel() =>
        MessageBox.Show(this, "Deseja realmente cancelar a instalação?", Branding.ProductName,
            MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
}
