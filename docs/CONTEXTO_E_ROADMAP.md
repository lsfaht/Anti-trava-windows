# Windows Keep Awake — contexto verificado e roadmap

**Revisão do documento:** 23/10/2026

**Escopo desta revisão:** Fase 1 da Central de Manutenção e redesenho da interface WinForms. Nenhuma operação de limpeza foi executada; a análise de TEMP permanece somente leitura.

## Correção estrutural de layout e preparação do motor de manutenção — 23/10/2026

- **Causa confirmada por teste geométrico:** cards das páginas em coluna mantinham a largura padrão de 200 px porque `CreateSectionCard` não preenchia a célula do `TableLayoutPanel`; controles internos, inclusive o painel de ações Keep Awake, ultrapassavam o corpo do card. O menu também permanecia fixo em 232 px e o início posicionava textos por coordenadas fixas.
- Os cards agora usam `DockStyle.Fill`; dashboard usa cartões em fluxo com quebra e textos de tamanho automático. Navegação lateral se contrai abaixo de 1120 px para ícones com tooltip. Área central usa rolagem vertical; catálogo e manutenção mudam de duas colunas para pilha abaixo de 980 px. Filtros, relatório e tabelas ocupam a área disponível, com rolagem horizontal em tabelas quando necessária.
- Formulário declara DPI `PerMonitorV2`; tamanho mínimo externo 800×480 (nesta sessão, área cliente 784×441). Testes de controle/bounds e irmãos sem sobreposição exercitam todas as seis páginas em 800×480, 900×600, 1366×768 e 1920×1080, além do tamanho mínimo real. Esta sessão reportou `DeviceDpi=96` (100%); não houve alteração global de DPI, portanto 125% e 150% permanecem pendentes de inspeção em máquina configurada nessas escalas.
- A Central reorganiza o catálogo em colunas Categoria, Operação, Descrição, Risco, Estado e Admin; inclui prévias de categoria e de todas as operações, detalhes e impactos, resultados e histórico com coluna de tipo **Análise/Execução**.
- **Nenhum executor real está registrado.** Os botões Executar selecionada, Executar categoria e Executar todas as manutenções ficam desabilitados, com explicação. Não foi implementada exclusão de arquivos. A análise TEMP selecionada segue somente leitura e cancelável.
- Foram introduzidos contratos para executores marcados aprovado/validado, prévia identificada, seleção explícita de operações, confirmação ligada à prévia, risco, privilégio, progresso, cancelamento entre itens, resultados individuais/consolidados e histórico. Um fake executor nos testes verifica que uma confirmação de outra prévia não executa e que a confirmação correspondente aciona apenas o fake. A interface mostra a prévia e mantém execução desabilitada; a seleção interativa em diálogo fica para a fase que registrar executores reais.
- Restore e Build Release foram concluídos; a suíte reportou **64/64 checks**. Dois checks de link simbólico real seguem ignorados por `IOException`, com cobertura fake. `git diff --check` terminou sem erros (apenas avisos de normalização LF/CRLF do Git).
- Artefato isolado previsto em `src/KeepAwake.Windows/artifacts/validation/responsive-maintenance-final-bin/Release/net8.0-windows/KeepAwake.Windows.exe`; nenhuma instalação existente foi substituída e nenhum processo pessoal foi encerrado.
- A prévia da Central foi inspecionada por árvore de acessibilidade; a captura gráfica falhou com `FrameArrived timed out`. Assim, evidência visual raster não está disponível e não se declara homologação visual, apesar dos testes geométricos.

## Redesign da interface e auditoria funcional — 23/10/2026

- A janela usa o título **Windows Keep Awake**, tema escuro grafite/verde e menu lateral com Início, Keep Awake, Janelas e sessão, Central de Manutenção, Diagnóstico e Configurações.
- O dashboard reflete os estados reais dos pedidos de energia, tela, duração e inicialização; não exibe métricas de CPU, memória ou armazenamento que não estejam coletadas.
- Keep Awake separa estado/ações das preferências de duração e tela. Filtros ficam em Janelas e sessão. Diagnóstico preserva as consultas somente leitura e usa área rolável. Configurações mostra apenas a inicialização já suportada.
- A Central de Manutenção foi reorganizada em catálogo com filtro de categoria, painel de detalhes, área de análise com indicadores e histórico em colunas. Os 17 itens continuam no catálogo; somente TEMP do usuário está disponível. Nenhuma ação de exclusão foi adicionada.
- Os controles usam `Dock`, `Anchor`, tabelas percentuais e áreas roláveis. Testes WinForms definem tamanhos de janela de 1366×768 e 1920×1080; o ambiente não permitiu alternar a escala de DPI do Windows.
- A suíte própria STA possui agora 59 checks. A validação Release desta revisão concluiu **59/59**, incluindo navegação, dashboard, filtro de categoria, detalhes, análise, cancelamento, histórico, janela fora da tela e redimensionamento. `git diff --check` não apontou erros.
- O build isolado foi produzido em `src/KeepAwake.Windows/artifacts/validation/ui-redesign-final-bin/Release/net8.0-windows/KeepAwake.Windows.exe`; os arquivos da instalação existente não foram substituídos.
- O formulário foi iniciado pelo modo de preview da suíte usando APIs de energia falsas, sem ativar pedidos reais. O serviço de análise disponibilizado no preview é o analisador de TEMP somente leitura; ele não foi acionado nessa revisão.
- A árvore de acessibilidade confirmou o título, controles e estados do dashboard. A captura de tela pelo auxiliar de interface falhou com `FrameArrived timed out`; por isso, não há capturas geradas e a inspeção visual de todas as telas não foi homologada. A verificação de layout foi automatizada; escalas de DPI e posições visuais ainda dependem de inspeção manual em máquina de teste.
- Auditoria: energia, alternância e liberação têm testes WinForms com APIs falsas e testes reais de ativação/liberação imediata; isso não comprova comportamento temporal de suspensão/bloqueio. Inicialização/fechamento, ocultação/restauração e mutex têm cobertura automatizada; ativação física do ícone não foi refeita. A opção de inicialização foi inspecionada e testada sem alterar a configuração real. Catálogo, seleção, TEMP sintética, erros, cancelamento e armazenamento do histórico foram exercitados; não foi feita varredura da TEMP real do usuário nesta validação.
- A criação de link simbólico real segue indisponível por `IOException`; a regra de ignorar reparse points foi coberta pelo filesystem simulado. Nenhuma limpeza, mudança de política, encerramento de processo do usuário, commit ou push foi feito.

## 1. Estado do repositório

- Workspace: repositório Windows Keep Awake.
- Branch verificada: `codex/windows-keep-awake`.
- `origin`: `https://github.com/lsfaht/Anti-trava-windows.git`.
- A referência de código continua no commit `138544b` (`Add Windows Keep Awake installer and tray improvements`) na branch `codex/windows-keep-awake`. Antes desta Fase 1 já existia `docs/CONTEXTO_E_ROADMAP.md` não rastreado; as mudanças da Fase 1 também permanecem sem commit. Nenhum commit ou push foi feito.
- Solução: `WindowsKeepAwake.sln`; projetos `src/KeepAwake.Core`, `src/KeepAwake.Windows` e `tests/KeepAwake.Tests`.
- A documentação existente é `README.md`. Este arquivo passa a ser o documento de contexto e roadmap. Não havia diretório `docs/` antes desta revisão.

## 2. Implementação confirmada no código

### Aplicativo atual

- Aplicativo WinForms em C# para Windows 10/11 x64, direcionado a .NET 8. A UI, integrações Win32 e lógica central estão divididas entre `src/KeepAwake.Windows` e `src/KeepAwake.Core`.
- `PowerRequest` usa `SetThreadExecutionState` com `ES_CONTINUOUS | ES_SYSTEM_REQUIRED` e, conforme a opção, `ES_DISPLAY_REQUIRED`. A API mantém solicitação de energia enquanto ativa; não é um monitor de uso, não coleta eventos históricos do Windows e não equivale a controlar presença do usuário.
- `PresentationModeRequest` separa o modo de apresentação por `PowerCreateRequest`, `PowerSetRequest` e `PowerClearRequest`, com pedidos de tela e sistema. O aplicativo tenta liberar os pedidos ao interromper, expirar ou sair.
- O formulário enumera janelas visíveis do desktop interativo, obtendo somente processo, título e indicação de janela em primeiro plano. Os filtros aceitam nomes/trechos de título separados por `;`. Esses valores são usados em memória e não são gravados no log.
- A bandeja usa `NotifyIcon`, ícone incorporado ao executável, menu de contexto, duplo clique para restaurar e comando explícito **Sair**. Minimizar ou fechar pela caixa X oculta a janela sem terminar o processo.
- Há prevenção de instância duplicada por mutex de usuário/sessão. Existe uma opção de inicialização com o Windows, desmarcada por padrão, que escreve somente na chave `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` quando o próprio usuário a habilita.
- O relatório de diagnóstico consulta algumas chaves legíveis de Registro; não altera políticas e informa limites sobre a identificação de bloqueios corporativos.
- O log local registra operações/erros sem título de janela. O código limita o log ativo a aproximadamente 1 MiB e mantém um arquivo `.1`; portanto, há rotação simples de dois arquivos, não um histórico analítico de eventos.
- Existem instalador Inno Setup e script de build/publicação. O perfil de publicação é self-contained para Windows x64. O instalador padrão é por usuário e não solicita elevação.
- A aba **Central de Manutenção** exibe o catálogo por categoria, descrição, risco, necessidade de administrador e disponibilidade. Somente TEMP do usuário pode ser analisada nesta fase; as outras entradas não executam ações.
- O Core agora contém catálogo/modelos, contratos e `UserTempAnalyzer`; o projeto Windows contém o adaptador de leitura do sistema de arquivos, o serviço assíncrono para TEMP do usuário, o armazenamento JSON de histórico e o controle WinForms da Central.
- O analisador usa `Path.GetTempPath()`, valida diretório/escopo, conta arquivos e bytes sem abrir conteúdo e não percorre reparse points. Falhas resultam em estado parcial ou erro; cancelamento é suportado. O total é potencialmente recuperável, não espaço liberado.
- O histórico é `%LOCALAPPDATA%\WindowsKeepAwake\maintenance-history.json`, sem caminhos de arquivos, com retenção de 30 dias, limite de 500 registros e arquivo de até 1 MiB.

### Testes existentes

`tests/KeepAwake.Tests/Program.cs` é um executável de verificações próprias em STA, sem framework de testes ou pacotes de terceiros. Após a Fase 1, declara 52 checks. Nesta validação, **52/52 passaram**, sem falhas ou checks ignorados formalmente. Foram incluídos:

- flags e idempotência de `SetThreadExecutionState`, falhas e tentativas repetidas de liberação;
- ativação, rollback, liberação e falhas de `PowerSetRequest`;
- estado inicial/alternância WinForms, filtros por processo/título, lista vazia e restauração do filtro;
- ícone e menu da bandeja, ocultar/restaurar, sincronização, expiração e encerramento;
- algumas chamadas reais às APIs de energia para confirmar ativação/liberação imediata;
- catálogo e disponibilidade, contagem/tamanho, pasta vazia, falhas, cancelamento, escopo, root inseguro e reparse points simulados;
- persistência/retencão do histórico, análise de arquivos sintéticos sem alteração, integração WinForms e cancelamento pela interface.

O build Release isolado passou com 0 avisos e 0 erros. A amostra sintética de 1.000 arquivos levou cerca de 65 ms de tempo decorrido, consumiu cerca de 78 ms de CPU do processo e teve variação observada de working set de -0,18 MiB (uma única medição, não benchmark). A criação de link simbólico real foi negada por `IOException`; o caso foi registrado como SKIP e a lógica de reparse point foi validada por filesystem falso. Uma junction real não foi criada nesta execução. Esses checks não constituem observação temporal de suspensão, bloqueio ou política corporativa.

## 3. Recursos que ainda não existem

Continuam sem implementação funcional:

- monitoramento periódico e histórico de CPU/RAM com gráficos, limiares configuráveis ou alertas;
- histórico de alterações de arquivos via `FileSystemWatcher`;
- inventário manual de capturas de tela, relatórios diários ou histórico de início/fim de processos;
- analisador geral de armazenamento e detecção de arquivos grandes/antigos/duplicados;
- limpeza real do Windows ou Chrome.

O catálogo da Central já contém descritores dessas operações, mas os itens além da análise da TEMP do usuário estão marcados como planejados e não executáveis. O analisador geral também continua apenas planejado. A aplicação não deve sugerir que uma janela ou processo foi responsável por uma alteração de arquivo sem uma fonte de auditoria que comprove essa relação.

## 4. Decisões e limites aprovados para o planejamento

- Uso pessoal, com recursos de monitoramento corporativo desativados por padrão. Não alterar políticas corporativas, permissões ou agentes de segurança.
- Não registrar conteúdo de tela, páginas, documentos, senhas, formulários, teclas ou movimentos; não simular atividade de entrada; não encerrar processos automaticamente.
- Logs e históricos locais devem minimizar dados pessoais, ser visíveis/configuráveis e ter retenção explícita.
- Monitoramento de arquivos começa, se aprovado, em uma pasta de teste selecionada pelo usuário. A seleção de outros diretórios é explícita; não varrer unidades inteiras continuamente.
- Capturas de tela são manuais e iniciadas pelo usuário, com data/hora, escolha de monitor quando aplicável e retenção configurável.
- Nenhum comando de limpeza foi executado. A Fase 1 de análise somente leitura foi explicitamente autorizada e implementada; as fases posteriores continuam dependendo de aprovação. Não foram adicionadas dependências externas nem alteradas políticas.

## 5. Proposta de evolução da aplicação

### Painel e métricas do sistema

- Adicionar serviço independente de coleta de CPU e memória, inicialmente a cada 5–10 segundos, com intervalo configurável e armazenamento local limitado.
- Permitir limiares e duração mínima antes de alertas; exibir valores e histórico em gráficos simples.
- Não matar, suspender nem reconfigurar processos. Mostrar o valor observado e o horário, evitando atribuir causalidade sem evidência.
- Critérios de aceite: pausa/desativação clara; intervalo e retenção respeitados; falhas de coleta visíveis; baixa carga medida; nenhuma coleta em segundo plano oculta.

### Histórico de arquivos

- Prototipar em diretório de teste escolhido pelo usuário e exibir caminho, tipo de evento e horário. Suportar criação, alteração, exclusão e renomeação; tratar eventos duplicados, movimentações, estouro do buffer e diretórios inacessíveis.
- `FileSystemWatcher` entrega notificações, não um registro transacional garantido. Perda de eventos ou reinício do processo deve ser exposto como lacuna; não fazer varredura integral recorrente para reconstruir uma história supostamente completa.
- Atribuição de processo/usuário que originou uma mudança exige auditoria do sistema configurada e autorizada. Não inferir autoria pelo simples fato de um processo estar aberto.
- Critérios de aceite: escopo explícito e persistente; pause/retomar; deduplicação documentada; indicação de perda de eventos; exclusão/retencão do histórico pelo usuário.

### Capturas e relatórios

- Captura somente por ação manual, com pasta e retenção configuráveis, suporte a múltiplos monitores e indicação de falha. Nenhuma captura de tela automática ou OCR.
- Relatório diário opcional deve distinguir “nenhum evento” de “não coletado/sem dados”, incluir lacunas e período da coleta e omitir títulos/conteúdo pessoal por padrão.

### Histórico de aplicações

- Opcional e opt-in. Registrar apenas processo, horário de início/fim e origem do dado, sem argumentos de linha de comando, títulos de documentos, conteúdo ou atividade de teclado/mouse.
- Verificar limites de permissão, processos protegidos e efeitos de reinicialização/encerramento abrupto antes de prometer completude.

## 6. Central de manutenção do Windows — análise das propostas

### Catálogo disponível na Fase 1

O catálogo contém 17 operações. Somente **TEMP do usuário** é analisável nesta versão. As demais linhas são rótulos de roadmap; não executam comandos nem apresentam resultados simulados.

| Categoria | Operação | Estado | Risco | Administrador |
|---|---|---|---|---|
| Limpeza básica | TEMP do usuário | Disponível — análise somente leitura | Baixo | Não |
| Limpeza básica | TEMP do Windows | Planejada | Médio | Sim |
| Limpeza básica | Lixeira | Planejada | Médio | Não |
| Limpeza básica | Cache de miniaturas | Planejada | Baixo | Não |
| Aplicativos | Chrome — limpeza básica | Planejada | Médio | Não |
| Aplicativos | Chrome — limpeza completa | Planejada | Alto | Não |
| Aplicativos | Chrome — limpeza máxima | Planejada | Alto | Não |
| Aplicativos | Caches de aplicativos | Planejada | Médio | Não |
| Windows | Relatórios de erros | Planejada | Médio | Não |
| Windows | Limpeza de Disco | Planejada | Médio | Não |
| Windows | Delivery Optimization | Planejada | Médio | Sim |
| Windows | Cache de shaders DirectX | Planejada | Baixo | Não |
| Windows | Windows Update | Planejada | Alto | Sim |
| Windows | DISM | Planejada | Alto | Sim |
| Diagnóstico | Cache DNS | Planejada | Baixo | Não |
| Diagnóstico | Analisador de armazenamento | Planejada | Baixo | Não |
| Diagnóstico | Arquivos grandes e duplicados | Planejada | Baixo | Não |

As classificações administrativas são indicativas e precisam ser verificadas para a operação e o Windows efetivos antes de qualquer implementação. A coluna não implica que o aplicativo solicite elevação.

Os comandos abaixo foram fornecidos para avaliação. **Não foram executados.** Não devem ser agrupados em um botão de “limpeza máxima” sem escopo, prévia e consentimento por operação.

| Proposta recebida | Avaliação e risco | Direção recomendada para o plano |
|---|---|---|
| `del /q /f /s %TEMP%*` | Apaga recursivamente dados do usuário; arquivos em uso podem falhar, e aplicações podem depender de temporários. `/f` força arquivos somente leitura. | Começar por enumerar tamanho/idade e permitir seleção. Excluir apenas diretórios e arquivos temporários elegíveis, sem força, tratando arquivos em uso e registrando totais/erros. |
| `del /q /f /s C:\Windows\Temp*` | Escopo de sistema, pode exigir elevação; arquivos podem estar em uso por serviços/instaladores. | Não oferecer exclusão direta por padrão. Preferir as ferramentas de armazenamento do Windows ou ação administrativa gerenciada. |
| `del /q /f /s C:\Windows\Prefetch*` | Remove dados de otimização de inicialização/aplicativos; ganho incerto e regeneração posterior pode piorar desempenho temporariamente. | Excluir da limpeza rotineira e da predefinição “máxima”. |
| `ipconfig /flushdns` | Limpa cache DNS local; é diagnóstico de rede e pode aumentar novas consultas, não limpeza de armazenamento geral. | Categoria de rede separada, explicação de efeito e confirmação individual; não incluí-lo em execução em lote de limpeza. |
| `del /q /f /s C:\Windows\Logs*` | Caminho amplo; logs podem ser necessários para diagnóstico, suporte, auditoria e manutenção. Permissões/arquivos em uso também variam. | Não oferecer remoção indiscriminada. Logs do Windows não são “lixo” genérico. |
| `cleanmgr /sageset:1` / `/sagerun:1` | Ferramenta nativa de limpeza, mas seleção e perfil persistem; opções dependem da versão do Windows. | Encaminhar o usuário à interface oficial ou explicar claramente a configuração e a seleção antes da execução. |
| `net stop wuauserv`, apagar `SoftwareDistribution\Download`, `net start wuauserv` | Interrompe serviço de atualização e altera cache gerenciado do Windows Update; pode exigir elevação, falhar se o serviço for gerenciado e afetar atualização em andamento. | Não executar nem apagar diretório diretamente. Diagnosticar atualização e usar solução de problemas/controles administrativos oficiais. |
| `rundll32.exe advapi32.dll,ProcessIdleTasks` | Solicita tarefas ociosas do sistema, pode iniciar manutenção sem estimativa simples ou escopo restrito. | Não tratar como limpador nem incluí-lo em limpeza máxima. |

Para ações de manutenção futuras, cada item deve declarar finalidade, itens incluídos/excluídos, necessidade de elevação, impacto provável, espaço estimado, opção de prévia/dry-run, confirmação individual, erros parciais e histórico. Um modo em lote só deve ser considerado depois que cada operação independente estiver implementada, testada e revisada.

## 13. Revisão de responsividade e indicador de alimentação — 2026-10-23

### Causas verificadas no código

- A Central de Manutenção e a página hospedeira tinham `AutoScroll` ativos simultaneamente, criando rolagem vertical aninhada.
- As colunas do catálogo e do histórico mantinham larguras mínimas fixas mesmo em áreas estreitas, o que produzia rolagem horizontal evitável.
- A barra lateral apresentava apenas um texto genérico de “Energia”, sem separar alimentação elétrica/bateria do estado Keep Awake.
- O aviso de uso responsável da tela tinha texto menor que a redação completa solicitada e estava dentro de um card de altura fixa.

### Alterações aplicadas

- A Central de Manutenção agora é a única área com rolagem vertical nessa página. Sua busca combina com o filtro de categoria e volta a mostrar o catálogo completo ao limpar o texto.
- Em larguras reduzidas, as colunas secundárias do catálogo e histórico são recolhidas; os detalhes da operação continuam disponíveis no painel dedicado. As larguras são recalculadas ao redimensionar.
- A barra lateral ganhou um indicador separado de alimentação/bateria e Keep Awake. O estado vem de `GetSystemPowerStatus`; `WM_POWERBROADCAST` atualiza a leitura quando o Windows anuncia mudança e um polling de 15 segundos cobre atualizações sem notificação.
- Bateria, percentual, carregamento, tomada, ausência de bateria e estado desconhecido são tratados sem estimar valores não fornecidos pelo Windows. No modo compacto, ícones acessíveis mantêm descrições completas em tooltips.
- A paleta foi centralizada em `AppTheme`; a navegação usa cores nomeadas do tema. O aviso de uso responsável do modo de apresentação agora preserva a redação completa e tem mais altura mínima.
- O cabeçalho do formulário se ajusta ao conteúdo, a largura do stack desconta a barra de rolagem somente quando ela está visível e a barra lateral expandida passou para 252 px dentro do intervalo de 230–260 px solicitado.

### Validação desta revisão

- `dotnet restore WindowsKeepAwake.sln`: sucesso com SDK 8.0.426 no perfil do usuário (`%LOCALAPPDATA%\Programs\dotnet`); o `dotnet` global do processo ainda não localizou o SDK.
- `dotnet build WindowsKeepAwake.sln --configuration Release`: sucesso, 0 avisos e 0 erros.
- `dotnet tests/KeepAwake.Tests/bin/Release/net8.0-windows/KeepAwake.Tests.dll`: 69/69 checks passaram, incluindo estados simulados, leitura da API real, busca, larguras de colunas e geometria WinForms.
- A chamada real nesta máquina retornou alimentação externa, bateria detectada, 99% e carregando no momento da consulta. Esse valor é apenas uma observação pontual.
- A matriz de janelas verificou 800×480, 900×600, 1366×768 e 1920×1080; a sessão reportou `DeviceDpi=96` (100%). As escalas 125% e 150% não estavam disponíveis para teste.
- O auxiliar de captura gráfica do Windows falhou duas vezes com timeout. Portanto, a responsividade tem evidência automatizada de geometria, mas a validação visual renderizada permanece pendente. Não há homologação visual nesta revisão.
- Nenhuma operação de limpeza, configuração global do Windows ou política corporativa foi alterada.

### Referências da API de alimentação

- Microsoft Learn, [GetSystemPowerStatus](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getsystempowerstatus) e estrutura [SYSTEM_POWER_STATUS](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-system_power_status).
- Microsoft Learn, [eventos de gerenciamento de energia](https://learn.microsoft.com/en-us/windows/win32/power/system-power-management-events) e mensagem [WM_POWERBROADCAST](https://learn.microsoft.com/en-us/windows/win32/power/wm-powerbroadcast-messages).

## 7. Central de manutenção do Chrome — análise técnica e segurança

O aplicativo atual não inspeciona nem limpa perfis do Chrome. Chrome possui vários perfis, caminhos variáveis por instalação/canal e dados sincronizados. Remover arquivos diretamente de um perfil ativo pode encontrar bancos bloqueados ou deixar estado inconsistente; não editar SQLite/LevelDB, não forçar fechamento do navegador e não registrar URLs, cookies ou conteúdo.

### Categorias por tipo de solução

1. **Interface oficial do Chrome:** o usuário escolhe período e tipos em “Excluir dados de navegação”. É a opção suportada mais simples para a primeira fase. Categorias e efeitos variam com versões e contam com histórico, cookies/dados de site, cache, downloads (lista, não os arquivos baixados), autofill, configurações e dados hospedados. Excluir cookies enquanto conectado ao Chrome pode afetar a sessão; com sincronização, exclusões podem se propagar à Conta Google e outros dispositivos. Senhas/passkeys e extensão têm gestão separada.
2. **API oficial de extensão `chrome.browsingData`:** permite a uma extensão Chrome remover certos tipos de dados locais mediante permissão declarada `browsingData`; categorias suportadas mudam ao longo das versões. É uma API suportada do ecossistema Chrome, mas não uma API WinForms que um programa desktop possa chamar sem instalar/implantar uma extensão separada. Ela requer uma decisão de arquitetura, revisão de permissões, distribuição e políticas da organização. Não adicionar extensão nem permissões nesta fase.
3. **CDP / interface de depuração:** Chrome DevTools Protocol expõe comandos de depuração para cache/armazenamento, mas é um protocolo de desenvolvimento e requer uma superfície de depuração. Não habilitar depuração remota nem usar CDP como mecanismo de limpeza de um produto desktop gerenciado.
4. **Manipulação de arquivos locais do perfil:** alguns caches têm arquivos locais, mas os detalhes de diretórios/locks são internos e variam por perfil e versão. É uma estratégia frágil, principalmente com Chrome aberto; não recomendada como limpeza suportada.
5. **Reset ou remoção de perfil:** alto impacto e potencial perda de sessão, dados locais ou configuração. Pode ser considerada somente no modo máximo futuro, com perfil selecionado, explicação das consequências de sincronização/perda e confirmação específica. Preferir controles oficiais do Chrome; não apagar diretamente os arquivos internos do perfil.

### Modos propostos (nomenclatura do produto ainda a aprovar)

- **Básico:** somente cache de imagens/arquivos, período escolhido, depois de detectar que o Chrome foi encerrado normalmente. Prévia de perfil, período e tamanho estimado; se o navegador estiver aberto, cancelar e explicar como fechar manualmente. Nenhum arquivo do perfil será alterado diretamente até uma API suportada estar disponível e aprovada.
- **Completo:** permitir categorias explicitamente selecionadas na interface oficial (por exemplo, histórico/cookies/cache/autofill), com explicação por categoria, período e aviso de sincronização antes de encaminhar à tela oficial do Chrome. Não marcar categorias de forma implícita.
- **Máximo:** ainda não implementado. Pode futuramente incluir redefinição/remoção de perfis locais selecionados, conforme pedido, mas somente com explicação do impacto, seleção inequívoca de cada perfil e confirmação específica. Não manipular diretamente arquivos internos do perfil. Downloads, senhas/passkeys, extensões e dados hospedados exigem avaliação e consentimento separados; o nome não pode ocultar o impacto real.

### Analisador de armazenamento

- Separar análise de remoção: listar arquivos grandes/antigos dentro de escopo escolhido, espaço livre por unidade e possíveis duplicatas por hash, indicando incerteza e custo da varredura.
- Não tratar mesmo nome/tamanho como duplicata comprovada. Hash lê o conteúdo e pode ser caro; oferecer escopo, cancelamento e progresso.
- Não excluir automaticamente, não atravessar perfis ou pastas protegidas por padrão, e não revelar caminhos ou nomes pessoais em relatórios compartilhados sem consentimento.
- Registrar a diferença de espaço antes/depois quando a operação puder ser medida; relatar “não medido” se o valor não for confiável.

## 8. Ordem sugerida do roadmap

1. **Fundação e privacidade:** definir armazenamento local, limites, retenção, configuração, logs de dados e estados “sem evento”/“sem coleta”; preservar o comportamento atual de Keep Awake.
2. **Painel de métricas:** CPU/RAM com coleta limitada, pausa, retenção e validação de impacto.
3. **Análise de armazenamento somente leitura:** seleção de escopo, tamanhos, idade e duplicatas candidatas; nenhuma exclusão.
4. **Histórico de arquivos experimental:** pasta de teste escolhida, lacunas/duplicatas tratadas e nenhuma atribuição sem auditoria.
5. **Captura manual e relatórios locais:** retenção e exclusão controladas pelo usuário.
6. **Ações seguras de manutenção:** prévia e operações pequenas em dados temporários do usuário; estudar primeiro integração com Storage Sense/Recomendações de limpeza.
7. **Chrome básico pela via oficial:** definir UX para perfil/período e decidir se usar encaminhamento à interface oficial ou componente de extensão suportado. Gate de segurança/privacidade antes de qualquer alteração.
8. **Chrome completo e outras ações de Windows:** análise específica e consentimento por categoria; sem limpeza ampla de Logs, Prefetch ou cache do Windows Update.
9. **Modo máximo / reset de perfil:** avaliar separadamente perda de dados, recuperação, experiência por perfil, controles oficiais disponíveis e política corporativa antes de qualquer implementação.

## 9. Questões em aberto antes de implementação

- Frequência e retenção pretendidas para métricas; armazenamento máximo em disco; alertas visuais ou notificações.
- Quais diretórios iniciais serão usados para o protótipo de eventos de arquivo, e por quanto tempo guardar histórico.
- Onde salvar capturas e se devem ser excluídas de backups/sincronização do usuário.
- Se o histórico opcional de processos deve existir e quais fontes locais autorizadas são aceitáveis.
- Para Chrome, se uma extensão separada é aceitável no dispositivo corporativo e se pode ser implantada por política; qual perfil/período/categorias entram no modo básico.
- Quais ações do Windows são autorizadas pela organização e quais exigiriam elevação. A aplicação não deve pedir elevação de forma genérica.
- Escopo e redação de confirmação para “completo” e se “máximo” deve continuar disponível como nome.

## 10. Fontes oficiais consultadas para o plano do Chrome e armazenamento

- Google Chrome Help, [Excluir dados de navegação no Chrome](https://support.google.com/chrome/answer/2392709/clear-browsing-data-computer?hl=en-GB): categorias, escopo de dados e efeitos de sincronização.
- Chrome for Developers, [API `chrome.browsingData`](https://developer.chrome.com/docs/extensions/reference/api/browsingData): tipos de dados, permissão obrigatória e limitações por versão.
- Microsoft Support, [Gerenciar espaço em disco com Storage Sense](https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/manage-drive-space-with-storage-sense): escopo e opções de limpeza do Windows 10/11.
- Microsoft Support, [Configurações de armazenamento no Windows](https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/storage-settings-in-windows): categorias e Recomendações de limpeza.
- Microsoft Learn, [Opções de manutenção de pacote DISM](https://learn.microsoft.com/en-us/windows-hardware/manufacture/desktop/dism-operating-system-package-servicing-command-line-options?view=windows-10): significado de `/AnalyzeComponentStore`, `/StartComponentCleanup` e efeitos de `/ResetBase`.

## 11. Validação da Fase 1

- `dotnet restore WindowsKeepAwake.sln`: sucesso, sem instalação de dependências externas.
- `dotnet build WindowsKeepAwake.sln --configuration Release` com `BaseOutputPath` isolado em `artifacts\validation`: sucesso, 0 avisos e 0 erros. O primeiro build padrão falhou porque a DLL de saída estava bloqueada pelo aplicativo já aberto; nenhum processo foi encerrado.
- Suíte própria em STA: `dotnet` executou diretamente o assembly de teste Release isolado; 52/52 checks passaram. Não existe descoberta de testes pelo VSTest nem contagem de testes ignorados nesse runner.
- A análise de validação usou arquivos sintéticos em pasta isolada: 2 arquivos, 7 bytes, conteúdo/horários inalterados. A amostra de 1.000 arquivos: 65 ms de parede, 78 ms de CPU e variação de working set de -0,18 MiB em uma observação.
- A interface foi exercitada por testes WinForms na sessão interativa: presença da aba, seleção, ação de análise, resultado, histórico e cancelamento. A aplicação principal atualizada não foi aberta como uma segunda instância porque já havia uma instância em execução e o mutex impede duplicação; a instância existente não foi encerrada.
- O filesystem falso validou reparse point e contenção. O sistema recusou a criação de link simbólico real (`IOException`); junction real também não foi validada. O check registrou o caso como `SKIP` dentro da saída de teste, embora o runner conte checks como PASS/FAIL.
- `git diff --check`: passou para as alterações rastreadas. O roadmap, ainda não rastreado, também foi verificado separadamente e não contém espaços finais; Git avisou apenas sobre conversão LF para CRLF em futuras gravações no Windows.

## 12. Gate de aprovação

A Fase 1 está implementada: Central de Manutenção, catálogo, análise somente leitura da TEMP do usuário, cancelamento, histórico com limites e testes. Nenhuma operação de limpeza real foi adicionada ou executada. A próxima etapa proposta é a Fase 2 definida pelo usuário, após revisão desta entrega. Até aprovação explícita, não implementar outras funcionalidades, instalar dependências, excluir arquivos reais, modificar dados do Chrome ou mudar políticas do Windows.
