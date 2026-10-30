# Tarefas

Status: **Concluída** = implementada e verificada como descrito em "Concluída quando"; **Concluída (manual)** = verificada executando o programa, sem teste automatizado (UI/Windows); **Parcial** = falta parte do critério.

| ID | Tarefa | Origem | Depende de | Concluída quando | Status |
|---|---|---|---|---|---|
| T-01 | Criar solução, projetos (Domain, Application, Infrastructure, App, Tests) e estrutura de pastas | Constituição, D-01 | | Solução compila sem warnings | Concluída |
| T-02 | Configurar logging em arquivo (Serilog) e tratador central de exceções com mensagem amigável pt-BR | Constituição (Qualidade), D-06 | T-01 | Teste verifica que o log é gravado sem texto de demanda/anotação | Parcial: log e tratador funcionam e foram vistos em execução; não há teste automatizado do conteúdo do log |
| T-03 | Criar `DomainException`, `IClock` e relógio falso de teste | Constituição, D-11 | T-01 | Teste unitário usa relógio falso | Concluída |
| T-04 | Modelar `Demand` e `DemandEvent` com transições Start/Pause/Resume/Finish | RF-06, RF-07, D-11 | T-03 | Testes cobrem transições válidas e inválidas | Concluída |
| T-05 | Calcular tempo trabalhado a partir dos eventos, excluindo pausas | RF-06, RN-02, D-11 | T-04 | Teste cobre várias pausas e demanda em andamento | Concluída |
| T-06 | Modelar `Note`, `AppSetting` e `Hotkey` com validações | RF-16, RF-03 a RF-05, RN-04, RN-07 | T-03 | Testes cobrem texto vazio, tempo inválido e atalhos duplicados | Concluída |
| T-07 | Criar `AppDbContext`, mapeamentos e criação do banco SQLite | D-04 | T-04, T-06 | Banco criado em instalação nova | Concluída |
| T-08 | Implementar `DemandEventService` | RF-07 | T-07 | Teste lista eventos em ordem cronológica | Concluída |
| T-09 | Implementar `DemandService` | RF-04, RF-05, RF-06, RN-01, RN-03, D-12 | T-07, T-08 | Testes cobrem uma demanda ativa, restauração e eventos | Concluída |
| T-10 | Implementar `NoteService` | RF-16, RF-17, RF-18, RN-06 | T-09 | Teste cobre sem demanda ativa e texto vazio | Concluída |
| T-11 | Implementar `SettingsService` com valores padrão | RF-03 a RF-05, RF-09, RF-10, RF-13, D-15 | T-07 | Teste cobre padrões e persistência | Concluída |
| T-12 | Implementar `DashboardService` (30 dias) | RF-08 | T-09 | Teste cobre horas por dia, por tipo, meia-noite e janela de 30 dias | Concluída |
| T-13 | Implementar `BreakMonitor` (alerta de descanso e de retorno sem interromper) | RF-09, RF-10, RF-12, RN-05, D-13 | T-09, T-11 | Teste confirma alerta único e demanda inalterada | Concluída |
| T-14 | Implementar atalhos globais com tratamento de conflito | RF-03, RF-04, RF-05, RF-17, D-03 | T-11 | Atalho dispara a ação | Concluída (manual) |
| T-15 | Implementar notificações do Windows | RF-11, D-05 | T-01 | Notificação aparece no Windows | Concluída (manual) |
| T-16 | Implementar início com o Windows | RF-02, D-07 | T-11 | Chave criada e removida conforme a configuração | Concluída (manual) |
| T-17 | Implementar tema claro/escuro/automático | RF-13, D-08 | T-11 | Alternar tema muda a janela | Concluída (manual) |
| T-18 | Criar janela principal e ícone de bandeja | RF-01, D-02 | T-01 | Minimizar/fechar mantém o ícone na bandeja | Concluída (manual) |
| T-19 | Criar diálogo de iniciar demanda acionado pelo atalho | RF-03, RF-04 | T-09, T-14, T-18 | Atalho abre o diálogo e confirma iniciando o cronômetro | Concluída (manual) |
| T-20 | Ligar atalhos de pausar/retomar e finalizar às services | RF-04, RF-05 | T-09, T-14 | Atalhos alteram o status da demanda | Concluída (manual) |
| T-21 | Criar balão no canto superior direito com status e cronômetro | RF-14, D-17 | T-05, T-18 | Balão mostra Trabalhando/Descansando com tempo correndo | Concluída (manual) |
| T-22 | Expandir o balão em cartão com cronômetro grande, tipo e descrição | RF-15, D-17 | T-21 | Clique alterna entre pílula e cartão | Concluída (manual) |
| T-23 | Criar spotlight de anotação (Enter confirma, Esc cancela) | RF-17, RF-19, RF-20, D-16 | T-10, T-14 | Anotação salva na demanda ativa; Esc não grava | Concluída (manual) |
| T-24 | Criar lista de demandas e detalhe com fluxo cronológico (h/min/s) | RF-07 | T-08, T-18 | Detalhe mostra início, pausas, retomadas e fim com segundos | Concluída (manual) |
| T-25 | Permitir editar detalhes da demanda e incluir anotações pela tela | RF-16, RF-18 | T-10, T-24 | Edição e nova anotação persistem | Concluída (manual) |
| T-26 | Criar painel inicial com produtividade dos últimos 30 dias | RF-08, D-09 | T-12, T-18 | Gráficos exibem dados das demandas existentes | Concluída (manual) |
| T-27 | Criar tela de configurações (atalhos, alertas, descanso, início com Windows, tema) | RF-02 a RF-05, RF-09, RF-10, RF-13 | T-11, T-16, T-17 | Alterações persistem e são aplicadas sem reiniciar | Concluída (manual) |
| T-28 | Restaurar demanda ativa/pausada ao abrir o programa | RN-09, D-14 | T-09, T-21 | Reabrir com demanda em curso retoma o cronômetro correto | Concluída (manual) |
| T-29 | Revisar tratamento de erros e mensagens pt-BR em todas as telas | Constituição (Qualidade) | T-19 a T-27 | Falha mostra aviso amigável e gera log | Concluída (manual) |
| T-30 | Rodar build e testes finais | Desafio (definição de pronto) | T-29 | Sem erros ou warnings, testes passam, app roda | Concluída |
| T-31 | Criar testes de arquitetura da constituição | Constituição (Arquitetura) | T-09 | Teste falha se UI/Infraestrutura acessar o contexto ou faltar service de um model | Concluída |
| T-32 | Criar base visual "Aurora": paletas escura e clara, ícones de linha, estilos de cartão, botões, navegação e barra | RF-22, D-19 | T-18 | Telas usam os estilos; tema claro e escuro trocam a paleta | Concluída (manual) |
| T-33 | Redesenhar janela principal: sem moldura, cantos de 28 px, controles próprios, barra lateral com atalhos | RF-22, D-19 | T-32 | Janela arrasta, redimensiona, maximiza e minimiza para a bandeja | Concluída (manual) |
| T-34 | Redesenhar painel: saudação, cartão da sessão com anel por minuto, indicadores, gráfico de 30 dias e rosca por tipo | RF-22, RF-08, D-19 | T-26, T-33 | Dias sem horas viram barras apagadas; barras com valor em degradê | Concluída (manual) |
| T-35 | Animações: entrada escalonada, elevação no hover, escala ao clicar, barras crescendo, ponto pulsando só com timer rodando | RF-22, D-19 | T-34 | Animações visíveis e ponto parado em pausa | Concluída (manual) |
| T-36 | Redesenhar balão como pílula que vira cartão (largura, altura e raio animados) | RF-14, RF-15, RF-22, D-17 | T-21, T-22, T-32 | Transição de 0,55 s sem cortar conteúdo; botões inteiros no cartão | Concluída (manual) |
| T-37 | **Arrastar o balão** pela pílula e pelo corpo do cartão, sem quebrar o clique que expande | RF-21, RN-08, D-18 | T-36 | Arrasto move a janela; clique sem mover expande; botões não arrastam | Concluída (manual) |
| T-38 | Manter a posição arrastada só durante a sessão e limitar à área visível das telas | RF-21, RN-08, CA-06, CA-07, D-18 | T-37 | Reabrir o programa volta ao canto; soltar fora da tela deixa o cartão inteiro visível | Concluída (manual) |
| T-39 | Gerar pacote .zip com `Instalar.cmd`/`Desinstalar.cmd`/`LEIA-ME.txt` e instalação por usuário | CA-11, D-20 | T-30 | Extrair e executar o instalador coloca o programa no Menu Iniciar | Concluída (manual: arquivos e instalação em destino de teste; `Instalar.cmd` não foi executado de ponta a ponta) |

## Verificação manual de T-37 e T-38

Executado com o programa em funcionamento (simulando o mouse):
- Arrastar a pílula: janela se moveu na mesma distância do gesto.
- Clique simples na pílula (sem mover): cartão expandiu.
- Arrastar o cartão expandido para além do canto inferior esquerdo: parou onde o cartão fica inteiro visível.

Não exercitado em execução (decorre do desenho, a posição existe só em memória e nada é gravado): voltar ao canto ao reabrir o programa, e o balão reaparecer onde foi deixado quando uma nova demanda começa na mesma sessão.

Não há teste automatizado do arrasto: é comportamento de janela, sem regra de negócio, e o projeto de testes não referencia a camada WPF.
