# Plano técnico

## Contexto

Programa Windows residente na bandeja que cronometra demandas (início, pausa, retomada, fim), controla atalhos globais, notifica descansos, exibe relatórios e dashboards dos últimos 30 dias e mantém um balão flutuante com o cronômetro (ver `spec.md`, RF-01 a RF-25). Regras da `constitution.md` valem acima deste plano: .NET 10, Windows, SQLite, DDD, regra de negócio e acesso a dados somente em services, testes unitários, logs em arquivo sem dados sensíveis.

Lacunas da spec foram cobertas por decisões marcadas como **premissa** (D-12 a D-14); a spec agora registra as regras de negócio, casos de borda e critérios de aceite correspondentes.

## Arquitetura

Solução em camadas, dependências apontam para dentro:

```
src/
  Productivity.Domain/          # Models DDD (sem dependências externas)
  Productivity.Application/     # Services, AppDbContext, interfaces (IClock, IHotkeyRegistrar...), BreakMonitor
  Productivity.Infrastructure/  # Atalhos globais, bandeja/notificações, início com o Windows, logging, composição do SQLite
  Productivity.App/             # WPF: Windows/, Views/, Controls/, Styles/ (tema Aurora), Support/, AppController
  Productivity.Setup.Core/      # Regras puras do instalador (sem interface): versão, pasta, argumentos, extração do pacote
  Productivity.Installer/       # Assistente de instalação (Windows Forms): páginas, motor de instalação, elevação
tests/
  Productivity.Domain.Tests/
  Productivity.Application.Tests/   # inclui busca/agrupamento do histórico e testes de arquitetura da constituição
  Productivity.Setup.Tests/         # regras do instalador
installer/                          # build.ps1 (publica o app), package.ps1 (gera o instalador único), uninstall.ps1 (vai dentro do pacote)
```

Regras verificáveis (os itens 1 e 3 têm teste automatizado em `ArchitectureTests`):
1. Somente classes `*Service` usam `AppDbContext`/`IDbContextFactory`. Janelas, views e infraestrutura nunca referenciam o contexto (exceto a composição em `DependencyInjection.cs`).
2. Toda regra de negócio fica em service ou no próprio model (invariantes). UI não decide nada.
3. Todo model tem sua service: `Demand` → `DemandService`, `DemandEvent` → `DemandEventService`, `Note` → `NoteService`, `AppSetting` → `SettingsService`.
4. Models são fechados para modificação externa (setters privados, fábricas e métodos de comportamento) e estendidos por novos tipos/serviços (Open-Closed).
5. Relógio injetado (`IClock`) para tornar cronômetros testáveis.
6. Erros: services lançam `DomainException` com mensagem pt-BR amigável; um tratador central na UI (`UiGuard`) exibe o aviso e registra em log os erros inesperados.
7. **Camada de apresentação** (estilos, controles, animações, posição de janelas) não contém regra de negócio nem acessa services além dos já usados pelas views; é isolada em `Styles/`, `Controls/`, `Support/` e nos `.xaml`.

## Decisões

| ID | Decisão | Justificativa |
|---|---|---|
| D-01 | WPF (.NET 10, `net10.0-windows`) | Constituição exige Windows Program; WPF permite janelas sem moldura, transparência e animações necessárias ao balão (RF-14/15/21), ao spotlight (RF-19) e à identidade visual (RF-22) |
| D-02 | Bandeja com `System.Windows.Forms.NotifyIcon` (`UseWindowsForms` na Infrastructure) | RF-01; faz parte do SDK, evita pacote de terceiros |
| D-03 | Atalhos globais via P/Invoke `RegisterHotKey`/`UnregisterHotKey` numa janela invisível (`NativeWindow`) | RF-03/04/05/17; API nativa, sem dependência nova. Atalho ocupado gera aviso amigável |
| D-04 | SQLite com EF Core (`Microsoft.EntityFrameworkCore.Sqlite`); `AppDbContext` fica na camada Application e o banco é criado com `EnsureCreated`; arquivo em `%LOCALAPPDATA%\ProductivityMonitor\data.db` | Constituição exige SQLite. *Revisada:* o contexto precisa estar acessível às services (que vivem na Application) e não há ainda evolução de esquema que exija migrations; quando houver, adotar migrations em nova decisão |
| D-05 | Notificações do Windows via `NotifyIcon.ShowBalloonTip` | RF-11. *Revisada:* o pacote de toast (`Microsoft.Toolkit.Uwp.Notifications`) exigiria TFM específico do Windows 10 e registro de AUMID; o balão do `NotifyIcon` já aparece como notificação nativa no Windows 10/11 e não adiciona dependência |
| D-06 | Logs com Serilog + sink de arquivo (`logs/app-.log`, rotação diária, 14 dias) | Constituição exige log em arquivo. Nunca registrar texto de demanda/anotação, apenas IDs e dados técnicos |
| D-07 | Início com o Windows via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (`--minimized`) | RF-02; não exige administrador |
| D-08 | Tema claro/escuro/automático: controles padrão usam o tema Fluent do WPF (`ThemeMode`) e a paleta Aurora alterna entre duas variantes de `ResourceDictionary` | RF-13/22. *Revisada:* o tema Fluent do .NET 10 cobre TextBox/ComboBox etc. sem reescrever templates; `AuroraTheme` troca a paleta própria e acompanha mudança do tema do Windows no modo automático |
| D-09 | Gráficos desenhados com controles próprios (`RingProgress`, `DonutChart`, barras em XAML) | RF-08/22. *Revisada:* evita a dependência `LiveCharts` (dispensável para barras, anel e rosca) |
| D-10 | Testes com xUnit + SQLite em memória; sem testes automatizados de UI | Constituição exige testes unitários de regras; UI é fina e validada manualmente |
| D-11 | Cronômetro derivado dos eventos (nunca de contador em memória): tempo trabalhado = soma dos intervalos Start/Resume → Pause/Finish | RF-06/07; consistência após crash/reabertura |
| D-12 | **Premissa**: apenas uma demanda ativa por vez; iniciar nova com outra ativa finaliza a anterior após confirmação | Spec não define concorrência; evita contagem dupla |
| D-13 | **Premissa**: descanso = pausa da demanda. Alerta de pausa (RF-09) após X min de trabalho contínuo; alerta de retorno (RF-10) após Y min pausado; 0 desativa. Apenas notificam (RF-12), uma vez por trecho | Pausa e descanso seguem o mesmo fluxo de eventos |
| D-14 | **Premissa**: ao reabrir o programa com demanda ativa/pausada, ela é restaurada a partir dos eventos | Caso de borda de crash/reinício do Windows |
| D-15 | Atalhos padrão: Iniciar `Ctrl+Alt+S`, Pausar/Retomar `Ctrl+Alt+P`, Finalizar `Ctrl+Alt+F`, Anotação `Ctrl+Alt+N`; todos configuráveis | RF-03/04/05/17. O atalho de pausa também retoma |
| D-16 | Spotlight de anotação: janela sem moldura, centralizada, sempre no topo, `Enter` confirma, `Esc` ou clique fora cancela; sem demanda ativa mostra aviso | RF-17/19/20 |
| D-17 | Balão: janela transparente sempre no topo, fora da barra de tarefas, que nasce no canto superior direito. Pílula 220×48 que se transforma (largura, altura e raio animam juntos em 0,55 s, `cubic-bezier(.32,.72,0,1)`) em cartão 400×282 | RF-14/15. *Revisada:* RF-15 passou de "~40% da largura da tela" para tamanho fixo definido no layout aprovado; a pílula ganhou 24px (de 196) para não ficar apertada |
| D-18 | **Arrastar o balão**: pressionar e mover o mouse sobre a pílula ou sobre o corpo do cartão move a janela (`DragMove`) após um limiar de movimento do sistema; clique sem movimento continua expandindo; botões (pausar, finalizar, recolher) não iniciam arrasto. A posição vale **apenas durante a sessão** (memória): ao fechar e reabrir o programa o balão volta ao canto superior direito. Ao soltar, a posição é limitada para o cartão (mesmo expandido) permanecer inteiro dentro da área virtual das telas. Se a demanda termina e outra começa na mesma sessão, o balão reaparece onde foi deixado | RF-21. Sem persistência por decisão do autor (não gravar posição em banco/arquivo); limite evita perder o balão fora da tela |
| D-19 | Identidade visual "Aurora": paleta e estilos em `Styles/` (escura e clara), ícones de linha em controle próprio (`Icon`), animações por propriedades anexadas (`Motion`: entrada escalonada, elevação no hover, crescimento das barras), janela principal sem moldura com `WindowChrome` e cantos de 28 px, manchas de cor animadas sem `BlurEffect` (gradientes radiais) | RF-22. Vidro quase opaco em vez de desfoque do desktop: janelas transparentes do WPF não permitem backdrop do Windows (DWM) e `BlurEffect` animado é renderizado por software. Fonte `Inter` com fallback `Segoe UI Variable` (não embutida: WPF não carrega woff2) |
| D-20 | Distribuição em **arquivo único**: `installer/package.ps1` publica o app autocontido (`win-x64`), junta o `uninstall.ps1`, compacta em `app.zip` e publica o assistente (`Productivity.Installer`) como um único `.exe` autocontido com o `app.zip` embutido, gerando `artifacts/ProductivityMonitor-Setup-<versão>.exe` (~123 MB). *Revisada:* substitui o zip com `Instalar.cmd` da primeira versão, que não tinha assistente | RF-23. Ferramentas de instalador (Inno Setup/WiX) não estão disponíveis e o IExpress não faz assistente; um instalador próprio em .NET 10 mantém a stack da constituição, funciona sem runtime instalado na máquina e é testável. Sem assinatura digital: o SmartScreen avisa na primeira execução |
| D-22 | Histórico: `IDemandService.SearchAsync(DemandFilter, take)` filtra por datas no banco (limites do dia local convertidos para UTC) e por título em memória, com comparação sem diferenciar maiúsculas e acentos (`CompareOptions.IgnoreCase \| IgnoreNonSpace`); agrupa por dia local e soma o trabalho por dia. A tela só formata e exibe (cabeçalhos "Hoje/Ontem/data", contagem, total) usando agrupamento do WPF; a busca espera 250 ms sem digitar e descarta respostas antigas | RF-24/25, RN-10 a RN-12. Regra de negócio e leitura do banco ficam na service (constituição); `LIKE` do SQLite não ignora acentos nem maiúsculas fora do ASCII; o volume local permite filtrar título em memória |
| D-23 | Modo isolado por variável de ambiente `PRODUCTIVITYMONITOR_HOME`: pasta de dados própria, instância única separada, sem alterar o início automático do Windows e, no instalador, nomes de atalho e de registro com sufixo de teste | Permite testar e demonstrar sem tocar nos dados nem na instalação real do usuário; sem efeito quando a variável não existe |
| D-24 | Instalador: Windows Forms (`net10.0-windows`) desenhado com o tema Aurora; páginas Bem-vindo, Pasta, Pronto, Progresso e Conclusão; pasta padrão `Arquivos de Programas\Monitor de produtividade`. A elevação é sob demanda: só ao clicar em Instalar, se a pasta exigir, o instalador se reabre como administrador (`/resume`) já na etapa de progresso; instalação com permissão grava para todos os usuários (menu Iniciar comum, registro em `HKLM`), sem permissão grava para o usuário (`HKCU`). O programa é aberto sem privilégios de administrador (via Explorer). Regras puras (versão, pasta, argumentos, extração com proteção contra caminhos fora da pasta) ficam em `Setup.Core` com testes. Modo `/silent` para automação | RF-23, RN-13/14. A elevação sob demanda evita pedir administrador à toa e permite testar sem UAC; abrir o app elevado daria privilégios desnecessários |
| D-25 | Troca segura de versão: extrai para `<pasta>.setup-new`, encerra instâncias da pasta, renomeia a atual para `.setup-old`, move a nova e só então apaga a antiga; falha ou cancelamento removem o temporário e restauram a antiga. Registro na lista de aplicativos (`Uninstall\ProductivityMonitor`) com `UninstallString` apontando para `uninstall.ps1`, que pede elevação quando necessário, pergunta se mantém os dados e remove pasta, atalhos e registro | RN-13/15, CA-12 a CA-14. Garante que o usuário nunca fica sem o programa por falha no meio; desinstalador em PowerShell evita embutir um segundo executável de dezenas de MB |
| D-21 | Dependências efetivas: EF Core + SQLite, Serilog (+ extensions e sink de arquivo) e `Microsoft.Extensions.DependencyInjection`/`Logging.Abstractions` | Injeção de dependência e logging estruturado exigidos pela arquitetura em services; todas justificadas aqui. `nuget.config` do repositório usa só nuget.org para não depender de feeds privados |

## Modelo de dados

Models (Domain) e tabelas (SQLite):

- **Demand** (aggregate root): `Id (Guid)`, `Title`, `Type`, `Description`, `Status (Running|Paused|Finished)`, `CreatedAt`, `FinishedAt?`
  - Comportamento: `Start`, `Pause`, `Resume`, `Finish`, `UpdateDetails`, além de `GetWorkedTime`, `GetPausedTime`, `GetWorkSegments`, `GetTimeSinceLastEvent`. Transições inválidas lançam `DomainException`.
- **DemandEvent**: `Id`, `DemandId`, `Sequence`, `Kind (Started|Paused|Resumed|Finished)`, `OccurredAt`. Imutável; base do relatório cronológico (RF-07) e do cálculo de horas (D-11).
- **Note**: `Id`, `DemandId`, `Text`, `CreatedAt`, `Source (Hotkey|Application)`.
- **AppSetting**: chave/valor (`StartWithWindows`, `Theme`, `StartHotkey`, `PauseHotkey`, `FinishHotkey`, `NoteHotkey`, `BreakAlertMinutes`, `BreakDurationMinutes`).
- Valor `Hotkey` (Domain): modificadores + tecla, validação de formato.

Índices: `DemandEvent(DemandId, Sequence)`, `Demand(CreatedAt)`, `Demand(Status)`, `Note(DemandId)`. Datas gravadas em UTC.

**Posição do balão não é persistida** (D-18): vive somente na janela, em memória.

## Contratos

Interfaces das services (Application), assíncronas:

- `IDemandService`: `StartAsync(title, type, description, finishCurrent)`, `PauseAsync()`, `ResumeAsync()`, `TogglePauseAsync()`, `FinishAsync()`, `GetActiveAsync()`, `GetByIdAsync(id)`, `ListAsync(take)`, `SearchAsync(DemandFilter, take)`, `UpdateDetailsAsync(id, ...)`, `ListTypesAsync()`, evento `DemandsChanged`.
  - `DemandFilter(Text?, From?, To?)`: texto parcial do título e dias do calendário local (extremos incluídos).
  - `DemandDayGroup(Day, Demands, Worked)`: demandas iniciadas no dia, da mais recente para a mais antiga, e o tempo trabalhado somado. `SearchAsync` devolve os grupos do dia mais novo para o mais antigo; datas invertidas lançam `DomainException`.
- `IDemandEventService`: `ListByDemandAsync(demandId)` (fluxo cronológico RF-07).
- `INoteService`: `AddAsync(demandId, text, source)`, `AddToActiveAsync(text, source)`, `ListByDemandAsync(demandId)`.
- `ISettingsService`: `GetAsync()`, `SaveAsync(settings)` (valida atalhos duplicados e tempos entre 0 e 1440).
- `IDashboardService`: `GetLast30DaysAsync()` → horas por dia, horas por tipo, total, média e nº de demandas.
- `BreakMonitor` (Application): `Evaluate(demanda ativa, configurações)` devolve `TakeBreak`/`ReturnToWork`/`None`; a UI apenas dispara a notificação.
- Abstrações de infraestrutura: `IClock`, `IHotkeyRegistrar`, `INotificationSender`, `IAutoStartManager`.

Arrastar o balão **não tem contrato de service**: é comportamento da janela (`BubbleWindow`).

Mensagens de erro e log em pt-BR; nomes de classes, arquivos e métodos em inglês.

## Riscos

| Risco | Mitigação |
|---|---|
| Atalho já em uso por outro programa | `RegisterHotKey` falha → mensagem amigável; validação nas configurações |
| Spotlight não recebe foco em app minimizado | Forçar foco com `Activate` ao abrir; validado manualmente |
| Crash com demanda em curso | D-11 e D-14: estado derivado de eventos persistidos a cada transição |
| Relógio do sistema alterado | Eventos usam UTC; novo evento nunca fica antes do anterior |
| Balão arrastado para fora da tela (ou monitor removido) | D-18: posição limitada à área virtual ao soltar; ao reabrir o programa volta ao canto padrão |
| Arrasto confundido com clique | Limiar de movimento do sistema separa clique (expande) de arrasto; botões nunca arrastam |
| Desempenho de janelas transparentes animadas | Gradientes radiais no lugar de `BlurEffect`; animações a 30 fps; efeitos de desfoque só durante a transição do balão |
| Spec incompleta | Premissas D-12 a D-14 registradas na spec (regras de negócio e bordas) |
| Pacote sem assinatura digital | O aviso do SmartScreen é esperado e documentado na spec (fora de escopo); assinar o executável é evolução futura |
| Falha ou cancelamento no meio da instalação | D-25: extração em pasta temporária, troca por renomeação e restauração da versão anterior; pasta `.setup-old` permanece só se a restauração também falhar |
| Antivírus ou arquivo em uso impede a troca de pasta | A troca falha com mensagem amigável e a versão anterior é restaurada; o instalador encerra antes as instâncias do próprio programa |
| Instalador grande (~123 MB) | Inevitável: leva o app com o runtime .NET para funcionar sem pré-requisitos; compressão do executável único ligada |
| Elevação (UAC) não testável automaticamente | O fluxo `/resume` e a passagem entre as duas instâncias foram testados sem UAC; a gravação real em Arquivos de Programas depende de validação manual |
| Histórico lento com muitos registros | Filtro de datas no banco, limite de 500 e título em memória sobre o resultado; reavaliar se passar de milhares de demandas |
| Dependências novas | Todas listadas em D-21; qualquer outra exige nova decisão neste plano |

## Revisões em relação à primeira versão do plano

Decisões D-04, D-05, D-08, D-09, D-17 e D-20 foram alteradas durante a implementação e D-18, D-19 e D-21 a D-25 são novas. Cada uma traz o motivo na própria linha. Pela governança, alterações no plano devem ir em commit separado.
