# Windows Keep Awake

Aplicativo para Windows 10/11 x64. Solicita ao Windows que mantenha o sistema ativo e, opcionalmente, a tela ligada. A execução pelo código-fonte requer o SDK .NET 8; a instalação distribuída é self-contained e inclui o runtime .NET, sem exigir SDK, runtime separado ou ajuste de `PATH`.

## Bandeja do sistema

Ao minimizar a janela, ela é ocultada da barra de tarefas e o aplicativo continua executando na bandeja do sistema. Fechar pela caixa **X** também oculta a janela; isso não encerra o processo nem libera as solicitações de energia. Clique duas vezes no ícone para reabrir a janela. O menu do ícone oferece **Abrir janela**, iniciar/interromper a manutenção de energia, ativar/desativar o modo de apresentação e **Sair**. Selecione **Sair** para liberar as solicitações e encerrar o aplicativo. Se uma liberação falhar, a janela permanece aberta para que a operação possa ser tentada novamente.

O ícone próprio do aplicativo é incorporado ao executável e ao `NotifyIcon`, sem depender de um arquivo externo depois da compilação. O Windows pode ocultar ícones na área de notificação ou colocá-los no menu de ícones ocultos: no Windows 11, verifique **Configurações > Personalização > Barra de tarefas > Outros ícones da bandeja do sistema**; no Windows 10, verifique **Configurações da barra de tarefas > Área de notificação > Selecionar quais ícones aparecem na barra de tarefas**. A propriedade `NotifyIcon.Visible` ativa o ícone para o Windows, mas não substitui a preferência de exibição definida pelo usuário.

O aplicativo impede uma segunda instância para o mesmo usuário e sessão do Windows; uma nova execução enquanto já estiver aberto é encerrada sem abrir outra janela.

## Ativação, duração e modo de apresentação

Ao abrir, **Manter também a tela ligada** já está marcado e o modo de apresentação é ativado automaticamente. O estado só é mostrado como ativo depois da confirmação das APIs do Windows; falhas são exibidas e registradas. O botão **Ativar modo de apresentação** usa as APIs Win32 documentadas `PowerCreateRequest`, `PowerSetRequest` e `PowerClearRequest` com `PowerRequestSystemRequired` e `PowerRequestDisplayRequired`. A documentação da Microsoft informa que o pedido de tela mantém a tela ligada e impede o início automático do protetor de tela e o bloqueio automático por inatividade enquanto a solicitação estiver ativa. Ele mantém a sessão disponível enquanto ativo; não desbloqueia uma sessão já bloqueada nem impede bloqueio manual.

Escolha **30 minutos**, **1 hora**, **2 horas** ou **Indefinidamente** em **Duração**. A duração começa quando uma solicitação é ativada ou quando a duração é alterada enquanto há uma solicitação ativa. Ao expirar, o aplicativo tenta liberar os dois modos e atualiza janela e bandeja. **Indefinidamente** não agenda expiração. Desativar manualmente impede a reativação automática durante aquela sessão do aplicativo.

Como esse pedido suprime o bloqueio automático por inatividade enquanto ativo, use-o somente em situações e dispositivos em que isso seja autorizado pela organização. Ele não altera a configuração de política; a organização pode ainda aplicar outros mecanismos de bloqueio. Não simula entrada do usuário.

## Inicialização com o Windows

A caixa **Iniciar com o Windows minimizado na bandeja** fica desmarcada por padrão. Quando habilitada, grava uma entrada reversível somente no `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, sem privilégios administrativos. Desmarque a opção para removê-la. A inicialização respeita as políticas do computador; não contorna controles corporativos.

## Executar

Requer o SDK do .NET 8 para compilar. No Windows, execute `dotnet run --project src/KeepAwake.Windows`. Minimize a janela para mantê-la na bandeja; para encerrar, use **Sair** no menu do ícone. A interface enumera janelas visíveis no desktop interativo e mostra processo/título apenas em memória. O filtro aceita nomes de processo ou trechos do título separados por ponto e vírgula. O arquivo local de diagnóstico guarda apenas operações e erros da própria aplicação, nunca títulos ou conteúdo de janelas.

Depois de compilar em Release, também é possível iniciar sem abrir um terminal pelo arquivo `Iniciar-WindowsKeepAwake.bat` na raiz ou pelo atalho **Windows Keep Awake** na área de trabalho. O script usa o runtime Desktop do .NET 8 instalado pelo usuário ou pela máquina, verifica se o executável Release existe e se os fontes são mais antigos que ele, e nunca recompila silenciosamente. Se o executável estiver ausente ou desatualizado, o script informa o comando de build necessário.

## Instalador e distribuição

O instalador x64 instala por padrão em `%LOCALAPPDATA%\Programs\WindowsKeepAwake`, sem elevação administrativa. Ele cria um atalho no menu Iniciar e oferece, desmarcado por padrão, um atalho na área de trabalho e a opção de iniciar o aplicativo ao concluir. O instalador verifica o mutex da aplicação e pede que ela seja fechada pelo menu **Sair** antes de atualizar ou desinstalar; não força o encerramento. Logs e configurações ficam em `%LOCALAPPDATA%\WindowsKeepAwake` e são preservados ao atualizar ou desinstalar.

Para gerar a publicação e o instalador, instale o SDK .NET 8 e o Inno Setup 6 no computador de compilação e execute `powershell -ExecutionPolicy Bypass -File .\Build-Installer.ps1`. O perfil `src/KeepAwake.Windows/Properties/PublishProfiles/win-x64.pubxml` publica em Release, self-contained, como arquivo único para Windows x64, incluindo os binários nativos do runtime para extração segura pelo .NET. O Inno Setup empacota o executável e esta documentação em `artifacts/installer/WindowsKeepAwake-Setup-1.0.0-win-x64.exe`; os artefatos são ignorados pelo Git. A compilação do instalador exige conexão para baixar o runtime pack .NET se ainda não estiver no cache local.

Instale executando o `.exe` gerado. Para atualizar, feche a aplicação pelo menu da bandeja e execute o novo instalador; ele atualiza os arquivos na mesma pasta e mantém atalhos e dados do usuário. Para desinstalar, use **Aplicativos instalados** do Windows ou o atalho de desinstalação no menu Iniciar. A desinstalação remove a aplicação e seus atalhos, mas mantém logs e dados em `%LOCALAPPDATA%\WindowsKeepAwake`.

## Limite técnico

`SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)` solicita que o sistema não suspenda; `ES_DISPLAY_REQUIRED` opcionalmente solicita que o monitor não desligue. Essa API, sozinha, não impede protetor de tela, bloqueio por inatividade, bloqueio manual nem políticas de segurança. O modo de apresentação opcional usa a API separada `PowerSetRequest` e a documentação do Windows especifica que `PowerRequestDisplayRequired` também impede o bloqueio automático por inatividade enquanto ativo. `InactivityTimeoutSecs` encontrado no Registro é evidência de uma configuração, mas não prova que ela causou um bloqueio específico. O relatório consulta somente chaves de Registro legíveis pelo usuário e indica chaves ausentes/inacessíveis. Não lê logs de segurança protegidos, não altera configurações corporativas e recomenda confirmar com o administrador se o modo de apresentação é autorizado.

## Testes

`dotnet run --project tests/KeepAwake.Tests` executa verificações Windows em STA, sem pacotes de terceiros. Os testes exercitam o formulário WinForms, a criação/visibilidade/ícone do `NotifyIcon`, os controles da bandeja, ocultar/restaurar, sincronização dos comandos, encerramento e liberação, filtros contra janelas visíveis reais do processo de teste, erros via APIs falsas, tentativas de liberação após exceção, a liberação real de `SetThreadExecutionState` ao encerrar e uma ativação/liberação imediata de `PowerSetRequest`. O teste confirma que o objeto NotifyIcon está configurado como visível e carrega o recurso de ícone incorporado; a preferência visual e a posição do ícone na bandeja dependem do Windows. Os testes não simulam a espera de 30 minutos, 1 hora ou 2 horas, nem validam o comportamento temporal de protetor de tela ou bloqueio. Os testes criam e fecham somente suas próprias janelas; devem ser executados em uma sessão Windows interativa.

Não há neste repositório um ambiente Windows limpo/VM automatizado. A compilação e os testes do código não substituem um teste de instalação em máquina sem .NET, e a distribuição final deve ser testada nesse cenário antes de uma adoção ampla.
