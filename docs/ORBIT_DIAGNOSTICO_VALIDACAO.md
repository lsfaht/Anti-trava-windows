# Orbit 0.1.1 — confiabilidade e diagnóstico

Data: 26/11/2026. Implementação autorizada neste chat. Sem commit/push, alteração de políticas, limpeza de dados do usuário ou término de instâncias do usuário.

## Evidência que motivou a mudança

A investigação anterior encontrou ativações aceitas pela API, mas não havia processo Orbit ao consultar depois. Isso não permite afirmar quando ou por que o processo terminou. Os novos eventos são prospectivos; não reconstruirão incidentes passados.

Havia uma exceção de navegação no Histórico no log legado de 30/10/2026, em `MainWindow.BuildHistory`/`ItemCollection.Add`. O código adicionava itens depois de atribuir `ItemsSource`. Corrigido para montar a lista antes da vinculação, inclusive no caminho de erro. Esse defeito não comprova a causa dos bloqueios mais recentes.

## Implementação

- `src/Orbit.Application/DiagnosticJournal.cs`: eventos JSONL limitados, rotação, marcador persistente, detecção de execução incompleta, tratamento de falhas de gravação e exportação. Apenas a instância primária abre o diário. Exceções são registradas sem mensagens/caminhos arbitrários.
- `src/Orbit.Application/OrbitLifetime.cs`: estados de janela/sessão/energia e liberação ao encerrar; ocultar/restaurar não libera pedidos.
- `src/Orbit.Application/KeepAwakeViewModel.cs`: retorno da API separado de efeito real, estados parciais, falhas de liberação e incerteza após suspensão/retomada. Não reativa automaticamente pedidos.
- `src/KeepAwake.Core/PresentationModeRequest.cs`: expõe separadamente os pedidos de sistema e tela sem alterar o mecanismo nativo.
- `src/Orbit.App/WindowsSessionObserver.cs`: janela nativa oculta recebe notificações WTS somente da sessão atual e notificações de suspensão/retomada. Falhas de registro ficam no diário. Não intercepta entrada nem impede bloqueio.
- `src/Orbit.App/DiagnosticReadOnlyChecks.cs`: lista de consultas numéricas e limitações, sem elevação nem alteração do Registro.
- `src/Orbit.App/App.xaml.cs`: versão/PID, exceções de WinUI/runtime/tarefas e encaminhamento de instância preservado; execução de teste isolada.
- `src/Orbit.App/MainWindow.xaml.cs`: integra observador, sinal de vida, exportação, estados precisos, limpeza independente de recursos e correção do Histórico.
- `src/Orbit.App/MainWindow.Smoke.cs`: teste explícito do host real com energia falsa.
- `tests/Orbit.Tests/DiagnosticTests.cs` e `Program.cs`: regressão de ciclo de vida, falhas, persistência, privacidade e exportação.
- `src/Orbit.App/Orbit.App.csproj`: versão 0.1.1. `README.md`: uso e limites.

Não foram adicionadas dependências NuGet.

## Validação

SDK utilizado: **8.0.426**, em `%LOCALAPPDATA%\Programs\dotnet\dotnet.exe`. Comandos a partir da raiz do projeto:

```powershell
dotnet build WindowsKeepAwake.sln --configuration Release
dotnet run --project tests/Orbit.Tests --configuration Release --no-build
dotnet run --project tests/KeepAwake.Tests --configuration Release --no-build
dotnet publish src/Orbit.App --configuration Release --self-contained true -o artifacts/orbit/0.1.1/win-x64
artifacts\orbit\0.1.1\win-x64\Orbit.exe --diagnostic-smoke
git diff --check
```

| Cenário | Resultado e evidência | Tipo |
|---|---|---|
| Build Release | Aprovado, zero avisos/erros | Compilação |
| Camada Orbit | 22/22 verificações | Testes com APIs falsas e arquivos temporários isolados |
| Regressão legada | 69/69 checks no runner; subcenário de link simbólico real ignorado por `IOException` de criação, teste de filesystem falso executado | WinForms, núcleo e chamadas nativas breves |
| Inicialização, ícone válido/Visible, registros WTS e energia | Aprovados no host real | APIs nativas; não captura visual |
| Ativar/interromper, sincronização janela/bandeja | Aprovados via handlers/peers dos próprios controles | WinUI real, energia falsa |
| Minimizar, restaurar, fechar pelo comando do X | Aprovados; visibilidade consultada com `IsWindowVisible`/`IsIconic`, pedidos falsos preservados | Janela real |
| Histórico | Aprovado ao construir a página | WinUI real |
| Exportar | Aprovado pelo botão; relatório gerado | Host real, eventos de energia falsos |
| Sair | Aprovado; janela encerra, ícone descartado, pedidos falsos liberados | Host real |
| Abertura normal e segunda instância | Aprovado: PID primário 27372, janela Orbit com HWND não nulo e respondendo; PID secundário 11316 encerrou com código 0; permaneceu uma instância e foi registrado `window_restored` | Executável 0.1.1 normal; nenhuma solicitação ativada pelo teste |
| Bloqueio/desbloqueio/suspensão/retomada | Estados e snapshots cobertos por eventos simulados no núcleo; registros nativos de observadores aceitos | Recepção de eventos reais permanece pendente |
| Permanecer desbloqueado durante ausência | Não executado e não garantido | Requer observação real autorizada |
| Ícone fisicamente junto ao relógio/duplo clique físico | Não verificados visualmente | Pendente |

O smoke final tem 13/13 cenários, zero falhas e código de saída zero. A primeira tentativa do harness usou `Window.Close()`, que encerra programaticamente antes dos cenários restantes. Foi corrigida para o comando de sistema do botão X na própria janela de teste e adicionada a exigência de contagem completa; nenhum resultado parcial deve ser aceito como homologação.

Evidências locais (raiz `C:\Users\lucas.faht\Documents\ChatGPT\Windowa`):

- `artifacts\validation\orbit-diagnostics-20261126\build.txt`
- `artifacts\validation\orbit-diagnostics-20261126\orbit-tests.txt`
- `artifacts\validation\orbit-diagnostics-20261126\legacy-tests.txt`
- `artifacts\validation\orbit-diagnostics-20261126\publish.txt`
- `artifacts\validation\orbit-diagnostics-20261126\smoke-result.json` (13/13, PID 25468, código de saída do aplicativo 0)
- `artifacts\validation\orbit-diagnostics-20261126\startup-single-instance.json`
- `artifacts\validation\orbit-diagnostics-20261126\diff-check.txt` (`git diff --check` aprovado; avisos de conversão LF/CRLF)
- `artifacts\orbit\0.1.1\win-x64\smoke-results\result.json`, `events.jsonl`, `run-state.json` e `reports\Orbit-diagnostico-*.txt`

Os eventos no diretório smoke são evidência de teste, não de um bloqueio real. O diário normal usa `%LOCALAPPDATA%\Orbit`. Horários de eventos têm offset local. A retenção limitada e o sinal de vida na thread da interface não constituem uma auditoria completa; pausas entre eventos não identificam sozinhas um travamento.

## Uso após a atualização

Iniciar `C:\Users\lucas.faht\Documents\ChatGPT\Windowa\artifacts\orbit\0.1.1\win-x64\Orbit.exe`. Se uma versão antiga estiver aberta, encerrá-la voluntariamente por **Sair** antes de iniciar esta. O Orbit inicia sem pedidos de energia; ativação continua explícita.

Após uma ocorrência natural, desbloquear normalmente e exportar por **Diagnóstico → Exportar diagnóstico**. Se o processo tiver terminado, reabrir a versão nova para registrar a execução anterior incompleta e exportar. Comparar o horário do bloqueio relatado com os eventos e snapshots. Uma linha `system_Locked` mostra um evento recebido, não qual política/agente o causou. Sem esse evento, não concluir que não houve bloqueio: o observador pode estar indisponível, o processo ausente ou o log rotacionado.

Somente um teste autorizado em Windows real poderá demonstrar o comportamento durante ausência. A investigação da origem e precedência das políticas continua dependendo de consultas permitidas e do administrador. Nenhuma política foi modificada.

## Referências oficiais

- https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsregistersessionnotification
- https://learn.microsoft.com/en-us/windows/win32/power/wm-powerbroadcast
- https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-powersetrequest
- https://learn.microsoft.com/en-us/windows/security/threat-protection/security-policy-settings/interactive-logon-machine-inactivity-limit
