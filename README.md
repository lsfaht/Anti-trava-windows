# Windows Keep Awake

## Interface atual

A janela principal usa tema grafite com destaque verde e seis seções. O menu lateral mostra os nomes completos em janelas largas e muda para ícones com tooltips abaixo de 1120 px. Os cards do dashboard se reorganizam em até três colunas. No rodapé lateral, alimentação/bateria e Keep Awake aparecem como estados separados; o primeiro vem de `GetSystemPowerStatus`, atualiza ao receber `WM_POWERBROADCAST` e tem polling de 15 segundos como fallback. Valores desconhecidos não são inventados e o modo compacto mantém os detalhes em tooltips. As páginas extensas rolam verticalmente e a janela pode ser reduzida até 800×480 unidades lógicas. O aplicativo declara suporte a DPI por monitor (`PerMonitorV2`) e ajusta a navegação e o catálogo quando o espaço muda.

O título da janela é **Windows Keep Awake**. A prevenção de instância duplicada usa o mutex existente; uma nova execução pede que a instância atual restaure a janela. Instâncias de versões anteriores que ainda estejam em execução não conhecem o sinal novo, então a nova execução informa que a versão antiga precisa ser encerrada pelo menu da bandeja antes de iniciar a atualizada.

Aplicativo para Windows 10/11 x64. Solicita ao Windows que mantenha o sistema ativo e, opcionalmente, a tela ligada. A execução pelo código-fonte requer o SDK .NET 8; a instalação distribuída é self-contained e inclui o runtime .NET, sem exigir SDK, runtime separado ou ajuste de `PATH`.

## Bandeja do sistema

Ao minimizar a janela, ela é ocultada da barra de tarefas e o aplicativo continua executando na bandeja do sistema. Fechar pela caixa **X** também oculta a janela; isso não encerra o processo nem libera as solicitações de energia. Clique duas vezes no ícone para reabrir a janela. O menu do ícone oferece **Abrir janela**, iniciar/interromper a manutenção de energia, ativar/desativar o modo de apresentação e **Sair**. Selecione **Sair** para liberar as solicitações e encerrar o aplicativo. Se uma liberação falhar, a janela permanece aberta para que a operação possa ser tentada novamente.

O ícone próprio do aplicativo é incorporado ao executável e ao `NotifyIcon`, sem depender de um arquivo externo depois da compilação. O Windows pode ocultar ícones na área de notificação ou colocá-los no menu de ícones ocultos: no Windows 11, verifique **Configurações > Personalização > Barra de tarefas > Outros ícones da bandeja do sistema**; no Windows 10, verifique **Configurações da barra de tarefas > Área de notificação > Selecionar quais ícones aparecem na barra de tarefas**. A propriedade `NotifyIcon.Visible` ativa o ícone para o Windows, mas não substitui a preferência de exibição definida pelo usuário.

O aplicativo impede uma segunda instância para o mesmo usuário e sessão do Windows; uma nova execução solicita que a janela da instância existente seja restaurada.

## Ativação, duração e modo de apresentação

Ao abrir, **Manter também a tela ligada** já está marcado e o modo de apresentação é ativado automaticamente. O estado só é mostrado como ativo depois da confirmação das APIs do Windows; falhas são exibidas e registradas. O botão **Ativar modo de apresentação** usa as APIs Win32 documentadas `PowerCreateRequest`, `PowerSetRequest` e `PowerClearRequest` com `PowerRequestSystemRequired` e `PowerRequestDisplayRequired`. A documentação da Microsoft informa que o pedido de tela mantém a tela ligada e impede o início automático do protetor de tela e o bloqueio automático por inatividade enquanto a solicitação estiver ativa. Ele mantém a sessão disponível enquanto ativo; não desbloqueia uma sessão já bloqueada nem impede bloqueio manual.

Escolha **30 minutos**, **1 hora**, **2 horas** ou **Indefinidamente** em **Duração**. A duração começa quando uma solicitação é ativada ou quando a duração é alterada enquanto há uma solicitação ativa. Ao expirar, o aplicativo tenta liberar os dois modos e atualiza janela e bandeja. **Indefinidamente** não agenda expiração. Desativar manualmente impede a reativação automática durante aquela sessão do aplicativo.

Como esse pedido suprime o bloqueio automático por inatividade enquanto ativo, use-o somente em situações e dispositivos em que isso seja autorizado pela organização. Ele não altera a configuração de política; a organização pode ainda aplicar outros mecanismos de bloqueio. Não simula entrada do usuário.

## Inicialização com o Windows

A caixa **Iniciar com o Windows minimizado na bandeja** fica desmarcada por padrão. Quando habilitada, grava uma entrada reversível somente no `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, sem privilégios administrativos. Desmarque a opção para removê-la. A inicialização respeita as políticas do computador; não contorna controles corporativos.

## Executar

Requer o SDK do .NET 8 para compilar. No Windows, execute `dotnet run --project src/KeepAwake.Windows`. Minimize a janela para mantê-la na bandeja; para encerrar, use **Sair** no menu do ícone. A interface enumera janelas visíveis no desktop interativo e mostra processo/título apenas em memória. O filtro aceita nomes de processo ou trechos do título separados por ponto e vírgula. O arquivo local de diagnóstico guarda apenas operações e erros da própria aplicação, nunca títulos ou conteúdo de janelas.

Depois de compilar em Release, também é possível iniciar sem abrir um terminal pelo arquivo `Iniciar-WindowsKeepAwake.bat` na raiz ou pelo atalho **Windows Keep Awake** na área de trabalho. O script usa o runtime Desktop do .NET 8 instalado pelo usuário ou pela máquina, verifica se o executável Release existe e se os fontes são mais antigos que ele, e nunca recompila silenciosamente. Se o executável estiver ausente ou desatualizado, o script informa o comando de build necessário.

## Central de Manutenção

A aba **Central de Manutenção** tem cabeçalho com contagens, barra de ações, catálogo em colunas, detalhes e prévia, resultados e histórico. Abaixo de 980 px os painéis se empilham e a área rola; em telas largas, catálogo/detalhes e resultados/histórico aparecem em duas colunas. O catálogo mostra categoria, operação, descrição, risco, estado e necessidade de administrador; use rolagem horizontal se a largura não comportar todas as colunas.

Na Fase 1, somente **TEMP do usuário** está disponível para análise. **Analisar selecionada** continua somente leitura. O catálogo tem filtro de categoria e busca por nome, categoria ou descrição; limpar a busca restaura os resultados. As colunas secundárias são recolhidas em janelas estreitas, enquanto detalhes completos ficam no painel da operação. A Central também oferece prévias para a categoria e para o catálogo inteiro. Os botões **Executar selecionada**, **Executar categoria** e **Executar todas as manutenções** permanecem desabilitados porque não existe executor real aprovado e validado. Eles não iniciam comandos nem simulam limpeza. A categoria e o lote excluem ações de risco alto por padrão. Nenhuma operação de exclusão de arquivos foi implementada.

O botão **Analisar** percorre, em segundo plano e somente para leitura, o caminho retornado pelo Windows para TEMP do usuário. A análise conta arquivos e tamanhos, indica falhas de acesso, progresso e links/junctions ignorados. Ela verifica o escopo do caminho e não atravessa reparse points. O tamanho é **potencialmente recuperável**; nenhum arquivo é aberto para leitura de conteúdo, alterado, movido ou excluído. A análise pode ser cancelada. Os estados possíveis distinguem conclusão, diretório vazio, conclusão parcial, cancelamento e erro.

O histórico é gravado em `%LOCALAPPDATA%\WindowsKeepAwake\maintenance-history.json`. Diferencia **Análise** de **Execução**. Guarda identificador, data/hora, duração, estado, itens analisados ou afetados, bytes potenciais ou recuperados e falhas; não registra caminhos de arquivos individuais. Registros com mais de 30 dias são removidos do histórico da aplicação. O armazenamento é limitado a 500 registros e 1 MiB. Essa retenção afeta somente o arquivo de histórico da própria aplicação.

As categorias incluem TEMP do usuário e do Windows, Lixeira, miniaturas, caches de aplicativos e Chrome, operações de Windows e diagnósticos. Nesta fase, todas as operações fora da TEMP do usuário aparecem como planejadas; não há limpeza real, inclusive para Chrome, Windows Update, Lixeira, DISM ou caches do sistema. Prefetch e exclusão indiscriminada de logs não estão no catálogo.

O modelo de execução separa prévia, disponibilidade de executor, aprovação/validação, confirmação da prévia, progresso, cancelamento entre operações, resultados individuais/consolidados e histórico. O contrato permite criar uma nova prévia a partir de uma seleção explícita de operações; risco alto precisa ser incluído explicitamente. A interface atual apresenta a prévia, mas os botões de execução continuam desabilitados e não oferece seleção de itens no diálogo. O registro de produção está vazio. Quando uma próxima fase fornecer um executor, a aplicação deverá mostrá-lo como executável apenas se estiver marcado como aprovado e validado; o fluxo de execução exige confirmação vinculada à prévia atual. Operações de risco alto não entram automaticamente em lotes.

O teste de integração exercita os controles da aba em uma sessão WinForms. A análise real de validação usou arquivos sintéticos em uma pasta temporária isolada, não a TEMP real do usuário. A criação de links simbólicos não foi permitida nesta máquina; o comportamento de pular `ReparsePoint` foi validado pelo filesystem falso. Uma futura validação em outra máquina pode confirmar esse caso com um link ou junction real.

## Instalador e distribuição

O instalador x64 instala por padrão em `%LOCALAPPDATA%\Programs\WindowsKeepAwake`, sem elevação administrativa. Ele cria um atalho no menu Iniciar e oferece, desmarcado por padrão, um atalho na área de trabalho e a opção de iniciar o aplicativo ao concluir. O instalador verifica o mutex da aplicação e pede que ela seja fechada pelo menu **Sair** antes de atualizar ou desinstalar; não força o encerramento. Logs e configurações ficam em `%LOCALAPPDATA%\WindowsKeepAwake` e são preservados ao atualizar ou desinstalar.

Para gerar a publicação e o instalador, instale o SDK .NET 8 e o Inno Setup 6 no computador de compilação e execute `powershell -ExecutionPolicy Bypass -File .\Build-Installer.ps1`. O perfil `src/KeepAwake.Windows/Properties/PublishProfiles/win-x64.pubxml` publica em Release, self-contained, como arquivo único para Windows x64, incluindo os binários nativos do runtime para extração segura pelo .NET. O Inno Setup empacota o executável e esta documentação em `artifacts/installer/WindowsKeepAwake-Setup-1.0.0-win-x64.exe`; os artefatos são ignorados pelo Git. A compilação do instalador exige conexão para baixar o runtime pack .NET se ainda não estiver no cache local.

Instale executando o `.exe` gerado. Para atualizar, feche a aplicação pelo menu da bandeja e execute o novo instalador; ele atualiza os arquivos na mesma pasta e mantém atalhos e dados do usuário. Para desinstalar, use **Aplicativos instalados** do Windows ou o atalho de desinstalação no menu Iniciar. A desinstalação remove a aplicação e seus atalhos, mas mantém logs e dados em `%LOCALAPPDATA%\WindowsKeepAwake`.

## Limite técnico

`SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)` solicita que o sistema não suspenda; `ES_DISPLAY_REQUIRED` opcionalmente solicita que o monitor não desligue. Essa API, sozinha, não impede protetor de tela, bloqueio por inatividade, bloqueio manual nem políticas de segurança. O modo de apresentação opcional usa a API separada `PowerSetRequest` e a documentação do Windows especifica que `PowerRequestDisplayRequired` também impede o bloqueio automático por inatividade enquanto ativo. `InactivityTimeoutSecs` encontrado no Registro é evidência de uma configuração, mas não prova que ela causou um bloqueio específico. O relatório consulta somente chaves de Registro legíveis pelo usuário e indica chaves ausentes/inacessíveis. Não lê logs de segurança protegidos, não altera configurações corporativas e recomenda confirmar com o administrador se o modo de apresentação é autorizado.

## Testes

`dotnet run --project tests/KeepAwake.Tests` executa verificações Windows em STA, sem pacotes de terceiros. A suíte cobre APIs de energia, ciclo de vida, filtros, bandeja, navegação, dashboard, estados reais e simulados de alimentação/bateria, busca no catálogo, limites geométricos, breakpoints, colunas adaptativas, prévias, botões de execução desabilitados, aprovação vinculada, análise, cancelamento e histórico. A validação desta revisão passou **69/69 checks** e consultou `GetSystemPowerStatus` na máquina. A matriz geométrica verificou 800×480, 900×600, 1366×768 e 1920×1080 a `DeviceDpi=96` (100%); 125% e 150% não estavam disponíveis e seguem pendentes. O argumento `--ui-preview` abre o formulário com APIs de energia falsas e não ativa solicitações do Windows. A captura do auxiliar gráfico expirou duas vezes com timeout; nenhuma captura visual foi produzida e não há homologação visual. Os testes criam e fecham somente suas próprias janelas; devem ser executados em uma sessão Windows interativa.

Não há neste repositório um ambiente Windows limpo/VM automatizado. A compilação e os testes do código não substituem um teste de instalação em máquina sem .NET, e a distribuição final deve ser testada nesse cenário antes de uma adoção ampla.
