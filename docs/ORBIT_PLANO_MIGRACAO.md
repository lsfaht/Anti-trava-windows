# Orbit — plano de migração e entregas

## Objetivo da primeira entrega

Introduzir um aplicativo WinUI 3 chamado Orbit em paralelo ao host WinForms. A primeira entrega serve para homologar arquitetura, identidade e os fluxos essenciais de Keep Awake, diagnóstico e análise segura; o host legado fica disponível para rollback. Não alterar nem apagar os dados existentes, e não transferir solicitações de energia entre processos.

### Implementado nesta rodada

- Auditoria e plano de migração.
- Projetos paralelos `Orbit.Application`, `Orbit.App` WinUI 3 e `Orbit.Tests`.
- Shell navegável com tema claro/escuro por recursos, páginas de início, Keep Awake, Manutenção, Diagnóstico e Histórico.
- ViewModel testável com ativação/interrupção, falha real comunicada, modo de apresentação explícito e liberação ao descartar.
- NotifyIcon WinForms hospedado no processo WinUI, menu de bandeja, restauração/ocultação e mutex/evento para instância Orbit única.
- Análise TEMP somente leitura, cancelamento, filtragem do catálogo/janelas e compatibilidade com o histórico local.
- Build Release x64 self-contained e nove testes de aplicação (ver relatório da entrega).

### Pendente para concluir a migração

- Ícone Orbit próprio (o host ainda reutiliza temporariamente o .ico legado) e inicialização opcional com o Windows.
- Migração do instalador, ícone/atalhos e ciclo de atualização/rollback.
- Capturas e revisão visual real; a captura automática do ambiente falhou duas vezes por timeout nesta execução.
- Validação do app Orbit executando em tela real e homologação em DPI/resoluções diferentes.

## Arquitetura alvo incremental

```text
Orbit.App (WinUI 3, views, navegação, tema)
  ├── Orbit.Application (ViewModels e orquestração)
  ├── KeepAwake.Core (solicitações de energia, filtros, modelos e análise TEMP)
  └── Orbit.Windows (interoperabilidade Windows, janelas, sessão, logs, armazenamento)
KeepAwake.Windows (WinForms legado, fallback durante homologação)
KeepAwake.Tests (regressão existente) + testes Orbit
```

No primeiro passo, evitar dividir os serviços em novos assemblies sem necessidade: adicionar contratos/ViewModels em `Orbit.Application` e infraestrutura WinUI/Windows em `Orbit.App` ou biblioteca próxima. Extrair módulos em assemblies somente quando mais de um host realmente os consumir. A integração por interfaces permite trocar o host sem copiar regras de negócio.

## Sequência

### Entrega 1 — identidade e migração da experiência base

1. Registrar auditoria e decisões técnicas.
2. Criar `Orbit.App` WinUI 3 unpackaged, mantendo os projetos atuais.
3. Criar shell com tema Orbit, navegação adaptável e páginas implementadas: Dashboard, Keep Awake, Manutenção, Diagnóstico e Histórico.
4. Conectar Keep Awake/Presentation aos contratos Core; falhas devem preservar estado real, comunicar e registrar.
5. Conectar análise TEMP de somente leitura, cancelamento e histórico JSON atual.
6. Reimplementar enumeração de janelas e diagnósticos sem leitura de conteúdo/captura.
7. Adicionar testes de ViewModels e erros, executar os 69 checks legados e build dos hosts.
8. Publicar em `artifacts/orbit/` separado; jamais sobrescrever executável em uso.
9. Gerar capturas reais da UI em resoluções/DPI declarados, revisar visualmente e registrar limitações. Captura que falhar deixa homologação visual pendente.
10. Manter tray/startup/instalador WinForms como caminho de produção até haver plano e validação de coexistência. No host novo, somente habilitar funções equivalentes quando a transição estiver implementada.

### Entrega 2 — arquitetura de módulos e Central de Manutenção

Separar catálogo, análise, histórico, diagnósticos e execução em módulos com contratos de risco/permissão. Continuar em modo somente leitura por padrão. Solicitar o arquivo de referência de comandos quando essa fase for autorizada; não inventar comandos ausentes.

### Entrega 3 — arquivos e backups

Projetar prévia, simulação, cancelamento e confirmação vinculada para cópia/backup. Espelhamento/destruição precisa de prévia de exclusões, validação de caminhos e aprovação explícita. Nenhum caminho pessoal de exemplo será codificado como padrão.

### Entrega 4 — automações externas

Somente conectores mock/simulação, validação e histórico. Não incorporar endpoints, IDs ou URLs corporativos no repositório público. Execução real de nuvem/pipeline requer credenciais aprovadas, menor privilégio e autorização explícita posterior.

### Entrega 5 — assistente e fluxo conversacional

Propor operações estruturadas por catálogo, risco e confirmação. Não permitir comandos arbitrários. Implementar apenas depois de contratos de módulo e política de aprovação.

### Entrega 6 — distribuição e transição

Medir self-contained vs runtime compartilhado; avaliar instalador estável, atalhos, startup, mutex, ícone e atualização dos dados existentes. A transição deve suportar rollback e nunca instalar em cima do processo legado em execução.

## Riscos e mitigação

| Risco | Mitigação |
|---|---|
| Runtime WinUI ausente no Windows de destino | Publish self-contained ou instalador do Runtime; medir tamanho e testar em máquina limpa antes da troca |
| Duas instâncias solicitarem energia | Identidades distintas durante homologação; não migrar estado implícito; demonstrar propriedade e limpeza |
| Compatibilidade de configurações/histórico | Reutilizar caminho e esquema, sem gravar nova preferência não versionada |
| APIs Win32 fora da interface WinUI | Isolar P/Invoke/Win32 em infraestrutura testável |
| Captura gráfica não disponível | Preservar evidência do erro e marcar homologação visual como pendente |
| Operação de manutenção destrutiva | Nenhuma execução sem executor revisado, preview e confirmação explícita |
| Credenciais/dados internos em Git | Nenhum segredo ou configuração corporativa em arquivos rastreados; mocks locais |
| Substituir instalador prematuramente | Manter instalador legado até validar pacote Orbit, atualização e rollback |

## Fora da primeira entrega

Não incluir manutenção destrutiva, gerenciamento de arquivos, automações reais de nuvem/GitLab, assistente com execução, novas dependências globais, elevação, bypass de políticas, commit ou push. Navegação não deve mostrar módulos vazios como se funcionassem.

## Homologação e aprovação

Relatar separadamente build/testes unitários, testes da aplicação real e inspeção visual. Publicar capturas e caminho do executável isolado. Não considerar a migração nem instalador finalizados por build. Parar após a entrega para revisão do usuário; commit/push dependem de aprovação explícita.
