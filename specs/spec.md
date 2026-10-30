# Monitor de produtividade

## Problema

Hoje preciso fazer trabalhos para as empresas e muitas vezes esqueço de contabilizar o tempo gasto de desenvolvimento. No fim, acabo mensurando uma média do que lembro de tempo gasto para enviar para a empresa.

## Objetivo

Permitir que, com um simples atalho pressionado do teclado no windows, que o programa seja ativado me perguntando o que vou desenvolver ou fazer e, ao colocar o que vou fazer e confirmar, começa contar um tempo em background até que eu acione outro atalho ou manualmente abra o programa e encerre a contabilização, tendo registro e tempo de todas atividades realizadas.

## Usuários

Usuário que estiver utilizando o OS.

## Histórias

Como desenvolvedor, quero ter relatório da minha produtividade sabendo o que foi feito em cada, além de poder editar com mais detalhess

## Requisitos funcionais
- RF-01: Quando minimizado, programa deve ficar em segundo plano na bandeja da barra de tarefas.
- RF-02: Configuração para iniciar programa junto com o windows
- RF-03: Permitir o usuário configurar o atalho de teclado para iniciar a demanda
- RF-04: Permitir o usuário configurar um atalho de teclado para finalizar a demanda
- RF-05: Permitir o usuário configurar um atalho de teclado para pausar a demanda
- RF-06: Pausas não podem contabilizar na quantidade total de horas trabalhadas da demanda.
- RF-07: Deve ter um relatório de cada demanda, mostrando o fluxo cronológico de início, pausas e conclusão, mostrando a hora/minuto/segundo da pausa e da retomada quando visualizando detalhes de cada.
- RF-08: Programa deve apresentar dashboards na tela inicial mostrando produtividade dos últimos 30 dias
- RF-09: Usuário deve conseguir configurar alertas de pausa, onde atingindo o tempo solicitado, o programa notifica que é hora de iniciar o descanso.
- RF-10: Usuário deve conseguir configurar tempo do descanso, onde atingindo o tempo solicitado, o programa notifica que é hora de voltar.
- RF-11: O programa deve notificar pelas notificações do windows
- RF-12: O aviso/notificação de descanso não deve interromper a demanda automaticamente, mesma coisa para notificação de retomada.
- RF-13: O sistema deve permitir que o usuário alterne entre tema claro/escuro ou automático do sistema
- RF-14: Enquanto a demanda/descanso estiver em andamento, no canto superior direito da tela deve ter um pequeno balão mostrando se estou descansando ou trabalhando e o cronômetro em andamento.
- RF-15: Clicando no balão, ele se transforma em um cartão maior (tamanho fixo definido no layout aprovado, em vez de uma porcentagem da tela) apresentando um cronômetro grande, o tipo da demanda, o que está sendo feito, o total de pausas e botões para pausar/retomar e finalizar. Uma seta no cartão recolhe o balão de volta.
- RF-16: Usuário deve conseguir inserir anotações em cada demanda
- RF-17: Usuário deve conseguir configurar um atalho para escrever uma anotação e, confirmando, automaticamente é inserido nas anotações da demanda
- RF-18: A inclusão de anotações também pode ser incluída no próprio sistema
- RF-19: Ativando atalho de anotações, deve abrir no centro da tela como spotlight do MACos para que somente aparece um campo bem bonito para digitar e enviar
- RF-20: Pressionando ESC, deve cancelar a anotação
- RF-21: O usuário deve conseguir clicar e arrastar o balão para qualquer ponto da tela, tanto recolhido quanto expandido. O arrasto começa ao segurar o botão do mouse sobre a pílula ou sobre o corpo do cartão e mover; os botões do cartão não iniciam arrasto. Um clique simples, sem mover, continua expandindo o balão.
- RF-22: O programa deve ter identidade visual própria no estilo "vidro" (tema "Aurora"): janela principal sem a moldura padrão do Windows, cantos arredondados e controles próprios de minimizar, maximizar e fechar; barra lateral com Painel, Demandas e Configurações; painel inicial com saudação e data, cartão da sessão atual com anel de progresso que completa a cada minuto, indicadores (hoje, total em 30 dias, média por dia, demandas), gráfico de 30 dias e distribuição por tipo de demanda; animações suaves de entrada, de passagem do mouse e de clique. Vale para o tema escuro e para o claro (RF-13).

## Regras de negócio
- RN-01: Só pode haver uma demanda ativa (em andamento ou pausada) por vez. Iniciar outra enquanto há uma ativa finaliza a anterior, e o programa avisa isso antes de confirmar.
- RN-02: O tempo trabalhado de uma demanda é a soma dos trechos entre início/retomada e pausa/conclusão; o tempo de pausa nunca entra nessa soma (RF-06). Finalizar uma demanda pausada não conta o período pausado como trabalho.
- RN-03: Pausar só é possível com a demanda em andamento; retomar só com ela pausada; uma demanda finalizada não muda mais de estado (mas seus detalhes e anotações podem ser editados).
- RN-04: O tipo e o "o que será feito" são obrigatórios para iniciar ou editar uma demanda.
- RN-05: Os alertas de descanso (RF-09) e de retorno (RF-10) só notificam, uma única vez por trecho de trabalho ou de descanso, e nunca pausam, retomam ou finalizam a demanda (RF-12). O valor 0 desativa o alerta.
- RN-06: Anotações exigem texto e uma demanda ativa quando criadas pelo atalho (RF-17); pela tela do programa podem ser adicionadas a qualquer demanda (RF-18).
- RN-07: Cada ação tem um atalho diferente, e todo atalho exige ao menos uma tecla modificadora (Ctrl, Alt, Shift ou Win).
- RN-08: A posição do balão arrastado (RF-21) só vale enquanto o programa está aberto. Ao fechar e abrir de novo, o balão volta ao canto superior direito e passa a ser movido conforme o uso. A posição não é gravada.
- RN-09: Ao reabrir o programa com uma demanda em andamento ou pausada, ela é restaurada e o cronômetro continua correto.

## Casos de borda
- Atalho já usado por outro programa: o app avisa qual atalho falhou e pede outro nas configurações.
- Atalho de anotação ou de pausa sem demanda ativa: o app apenas avisa; nada é criado.
- Relógio do computador alterado para o passado: o evento nunca é registrado antes do anterior, então não há tempo negativo.
- Trabalho que atravessa a meia-noite: as horas são divididas entre os dois dias no gráfico.
- Segunda abertura do programa: não abre outra cópia; apenas traz a janela da primeira.
- Balão arrastado para fora das telas (ou monitor removido): ao soltar, ele é reposicionado para ficar inteiro visível; ao reabrir o programa volta ao canto padrão.
- Balão arrastado perto da borda e depois expandido: o cartão expandido continua inteiro visível.
- Sem demanda ativa: o balão não aparece. Se uma nova demanda começa na mesma sessão, ele reaparece onde foi deixado.
- Fechar ou minimizar a janela principal não encerra o programa: ele continua na bandeja; "Sair" fica no menu da bandeja.

## Fora de escopo
- Gravar a posição do balão entre aberturas do programa.
- Sincronização entre computadores, múltiplos usuários ou envio automático de relatórios para empresas.
- Alertas por e-mail ou celular; somente notificações do Windows.
- Exportar relatórios para arquivo.
- Assinatura digital do instalador e atualização automática do programa.

## Critérios de aceite
- CA-01: Ao pressionar o atalho de iniciar, aparece o campo "o que você vai fazer"; ao confirmar, o balão aparece com o cronômetro correndo.
- CA-02: Pausar e retomar alteram o status do balão (Trabalhando/Descansando) e o tempo trabalhado não cresce durante a pausa.
- CA-03: O detalhe da demanda mostra início, pausas, retomadas e conclusão com data, hora, minuto e segundo.
- CA-04: Clicar na pílula do balão o expande; a seta o recolhe; pausar e finalizar funcionam pelo cartão.
- CA-05: **Arrastar o balão** (RF-21): segurar o mouse sobre a pílula e mover leva o balão até o ponto onde o mouse é solto; o mesmo vale para o cartão expandido; clicar sem mover continua expandindo; arrastar a partir de um botão do cartão não move o balão.
- CA-06: Depois de arrastar, o balão permanece no novo lugar ao pausar, retomar e expandir/recolher; ao fechar o programa pela bandeja e abri-lo novamente, o balão aparece no canto superior direito.
- CA-07: Soltar o balão com parte dele fora da tela o deixa inteiro visível.
- CA-08: O painel mostra os 30 dias de produtividade; dias sem horas aparecem como barras apagadas.
- CA-09: Os alertas de descanso e de retorno geram notificação do Windows sem alterar o status da demanda.
- CA-10: Alternar entre tema claro, escuro e automático muda a aparência do programa sem reiniciar.
- CA-11: Instalar a partir do pacote .zip coloca o programa no Menu Iniciar e na Área de Trabalho, sem pedir administrador.