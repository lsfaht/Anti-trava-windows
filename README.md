# Windows Keep Awake

Aplicativo manual para Windows 10/11 com .NET 8. Solicita ao Windows que mantenha o sistema ativo e, opcionalmente, a tela ligada. A opção só permanece enquanto estiver ativada na janela; fechar o aplicativo tenta liberar a solicitação. Não há inicialização automática nem persistência oculta.

## Executar

Requer o SDK do .NET 8 para compilar. No Windows, execute `dotnet run --project src/KeepAwake.Windows`. A interface enumera janelas visíveis no desktop interativo e mostra processo/título apenas em memória. O filtro aceita nomes de processo ou trechos do título separados por ponto e vírgula. O arquivo local de diagnóstico guarda apenas operações e erros da própria aplicação, nunca títulos ou conteúdo de janelas.

## Limite técnico

`SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED)` solicita que o sistema não suspenda; `ES_DISPLAY_REQUIRED` opcionalmente solicita que o monitor não desligue. A API não bloqueia protetor de tela, bloqueio por inatividade, bloqueio manual nem políticas de segurança. `InactivityTimeoutSecs` encontrado no Registro é evidência de uma configuração, mas não prova que ela causou o bloqueio relatado. O relatório consulta somente chaves de Registro legíveis pelo usuário e indica chaves ausentes/inacessíveis. Não lê logs de segurança protegidos, não altera configurações corporativas e recomenda confirmar a política efetiva com o administrador autorizado.

## Testes

`dotnet run --project tests/KeepAwake.Tests` executa verificações Windows em STA, sem pacotes de terceiros. Os testes exercitam o formulário WinForms, os cliques dos botões, filtros contra janelas visíveis reais do processo de teste, erros via API falsa, tentativas de liberação após exceção e a liberação real de `SetThreadExecutionState` ao fechar o formulário. Os testes criam e fecham somente suas próprias janelas; devem ser executados em uma sessão Windows interativa. Não aguardam suspensão, desligamento do monitor ou bloqueio.
