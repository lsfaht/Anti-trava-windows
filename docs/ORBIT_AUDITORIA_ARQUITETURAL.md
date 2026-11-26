# Orbit — auditoria arquitetural inicial

Data da auditoria: 2026-10-28
Branch observada: `codex/windows-keep-awake`
Commit observado: `dc807353c6b2c3422946f9a484c4a2b2cc2a4b3c`

## Escopo e evidências

Foram examinados a solução e os projetos, `README.md`, `docs/CONTEXTO_E_ROADMAP.md`, os serviços de energia e análise, a inicialização, bandeja, código de layout, catálogo e histórico da Central, runner de testes, script de build do instalador e definição Inno Setup. A árvore Git estava limpa no início. O processo Release já em execução foi preservado.

A inspeção visual foi tentada na janela real por ferramenta de captura Windows, mas a captura expirou duas vezes por timeout. Portanto, os defeitos visuais detalhados no pedido são relatos anteriores do usuário, não achados que pude reproduzir nesta sessão. Não havia captura histórica no repositório. Não declaro homologação visual a partir de testes geométricos ou build.

## Estado atual

- Solução com `KeepAwake.Core` (`net8.0`), `KeepAwake.Windows` (WinForms `net8.0-windows`) e `KeepAwake.Tests` (runner próprio WinForms/STA).
- `PowerRequest` e `PresentationModeRequest` encapsulam ciclo de vida e interfaces de API. A infraestrutura Windows invoca `SetThreadExecutionState` e `PowerCreateRequest`/`PowerSetRequest`/`PowerClearRequest`.
- Enumeração de janelas visíveis, registro opcional de inicialização por usuário, mutex/sinal de ativação, ícone e menu da bandeja estão no host Windows. A janela oculta ao minimizar/fechar e libera solicitações ao sair.
- `MainForm` e `MainForm.Layout` concentram navegação, estado, integração e construção de layout. A Central é um grande `UserControl` WinForms que combina apresentação, filtragem, prévias, execução condicionada e integração com análise/histórico.
- `UserTempAnalyzer`, modelos de manutenção e armazenamento JSON de histórico são reutilizáveis; análise percorre somente a TEMP do usuário, sem abrir conteúdo de arquivo e sem atravessar reparse points. Executores reais de limpeza não estão registrados; botões de execução ficam desativados.
- Preferências e histórico existentes ficam em `%LOCALAPPDATA%\WindowsKeepAwake`; logs não registram conteúdo de janelas. O instalador Inno usa AppId e diretório estáveis, publicação self-contained .NET x64 e registra atalhos/inicialização opcional.
- README documenta build/testes, Keep Awake, modo de apresentação, bandeja, análise TEMP e limites. Test runner próprio reportou 69 checks na última validação documentada; não é xUnit e integra controles WinForms em STA.

## Acoplamentos, riscos e limitações

1. A UI e seu estado estão acoplados: eventos, labels, temporizadores e sincronização da bandeja são privados da `MainForm`; não existe ViewModel nem composição por DI.
2. A Central combina cerca de 700 linhas de UI com chamadas assíncronas e estado de operação. Modelos e serviços são reaproveitáveis, mas o controle não é.
3. Layout WinForms é construído por código e parcialmente adaptativo. Há breakpoints e testes de dimensões em DPI de 100%; isso não comprova renderização sem cortes, contraste ou comportamento em 125/150%.
4. Mutex e ativação dependem de nomes/contratos existentes. A primeira versão Orbit não deve compartilhar o mutex antigo cegamente nem criar duas instâncias por coexistência; deve definir uma transição compatível.
5. Preferências e histórico não têm esquema versionado explícito no contrato geral. O novo host deve manter o caminho e o formato JSON atuais; mudanças futuras precisam ser versionadas.
6. `StartupRegistration` grava um valor Run específico do produto. Renomear o valor ou caminho do executável pode quebrar a inicialização instalada; atualização e desinstalação precisam reconhecer ambos os hosts.
7. A aplicação atual e o instalador são WinForms/.NET self-contained. WinUI 3 unpackaged requer Windows App SDK Runtime instalado ou publicação self-contained de Windows App SDK, que aumenta consideravelmente os artefatos. Instalador atual não o inclui.
8. Não há workloads/templates WinUI nem runtime Windows App SDK no ambiente inicialmente auditado. A compilação pode restaurar pacotes de projeto, mas instalação global de SDK/runtime não é necessária para desenvolver e não será feita.
9. Serviços de energia são dependentes do Windows e não devem ser testados provocando suspensão, bloqueio ou desligamento do monitor. Mocks podem atestar chamadas e sequência, não o comportamento de energia real.
10. A captura real da janela WinForms falhou por timeout; estado visual atual segue não homologado.
11. Futuras automações de nuvem/pipeline exigem escopo, autenticação corporativa, segregação de segredos e aprovação operacional. Nenhuma conexão, chamada real, credencial ou identificador corporativo será incluído/ativado nesta entrega.
12. Gerenciamento de arquivos/backups e limpeza destrutiva não estão implementados e têm risco de perda de dados; permanecem fora da primeira migração.

## Compatibilidade WinUI 3

O host em execução é Windows 10 22H2, build 19045, acima do piso documentado de Windows 10 build 17763 para Windows App SDK; WinUI 3 aceita .NET 8+. O SDK .NET 8 já está instalado. O Windows App SDK não estava no cache NuGet local e nenhum workload WinUI estava instalado.

Distribuição unpackaged encaixa melhor no modelo de instalador tradicional existente, mas requer Windows App SDK Runtime no destino ou conteúdo self-contained. A escolha inicial será self-contained para manter instalação sem elevação/sem pré-requisito global, aceitando aumento de tamanho. Isso precisa ser medido no publish e homologado no instalador antes de substituir distribuição.

Fontes Microsoft consultadas:

- [Documentação de desenvolvimento Windows](https://learn.microsoft.com/en-us/windows/apps/) — WinUI 3/Windows App SDK e .NET 8+.
- [Windows App SDK 1.8](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-1-8) — canal estável, releases e compatibilidade do pacote.
- [Distribuir aplicativo WinUI 3 unpackaged](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app) — runtime dependente ou publicação self-contained.
- [Visão geral de deployment](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview) e [self-contained](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps) — efeitos sobre distribuição.

## Decisões e recomendações

- Preservar `KeepAwake.Core`, contratos de energia, análise de TEMP e formato de histórico.
- Adicionar host `Orbit.App` WinUI 3 lado a lado; não remover nem renomear ainda WinForms, seus namespaces ou identificadores persistentes. Isso permite rollback e comparação visual.
- Separar UI e estado com ViewModels e serviços de aplicação no host novo. Reutilizar modelos de domínio e interfaces existentes; adaptar interfaces do Windows em infraestrutura.
- Implementar somente páginas com conteúdo funcional: Dashboard, Keep Awake, Manutenção, Diagnóstico e Histórico. Outras áreas ficam fora da navegação até existir implementação revisada.
- Migrar bandeja/startup com contratos explícitos e garantir que apenas um host possua as solicitações de energia por sessão. Durante homologação, o host Orbit terá identidade/mutex próprios e não assumirá a solicitação da instância legada.
- Manter automações de nuvem em simulação sem endpoints/IDs internos ou credenciais até pedido autorizado e revisão de segurança.
- Nenhum commit, push, instalação global ou alteração de política nesta entrega.

## Critérios de aceitação da migração inicial

- WinUI 3 compila em Windows 10/11 com .NET 8 e restaura somente dependências de projeto.
- O host exibe estado real da solicitação após êxito da API, mantém ativar/interromper e liberação no fechamento.
- Diagnóstico enumera janelas sem capturar tela nem conteúdo; análise TEMP continua somente leitura; histórico mantém formato e local atuais.
- UI usa layout responsivo/declarativo, tema legível, navegação funcional e estados vazios/erro.
- Testes de ViewModel/serviços verificam transições e falhas; regressão legada passa.
- Publish isolado produz aplicação executável sem substituir binários da instância em execução.
- Capturas visuais da aplicação real em resoluções/DPI e revisão das páginas principais; enquanto captura do ambiente estiver indisponível, homologação visual fica explicitamente pendente.
- Instalador, bandeja e startup migrados só após compatibilidade e coexistência serem comprovadas; até lá, WinForms permanece fallback.
