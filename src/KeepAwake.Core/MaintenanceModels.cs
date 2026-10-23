namespace KeepAwake.Core;

public enum MaintenanceCategory
{
    BasicCleanup,
    Applications,
    Windows,
    Diagnostics
}

public enum MaintenanceRisk
{
    Low,
    Medium,
    High
}

public enum MaintenanceAvailability
{
    Available,
    Planned,
    Unavailable
}

public sealed record MaintenanceOperationDescriptor(
    string Id,
    MaintenanceCategory Category,
    string Name,
    string Description,
    MaintenanceRisk Risk,
    bool RequiresAdministrator,
    MaintenanceAvailability Availability,
    bool CanAnalyze);

public static class MaintenanceCatalog
{
    public static IReadOnlyList<MaintenanceOperationDescriptor> Operations { get; } = Array.AsReadOnly<MaintenanceOperationDescriptor>(
    [
        new("user-temp", MaintenanceCategory.BasicCleanup, "TEMP do usuário", "Analisa arquivos e subdiretórios elegíveis da pasta TEMP do usuário. Somente leitura.", MaintenanceRisk.Low, false, MaintenanceAvailability.Available, true),
        new("windows-temp", MaintenanceCategory.BasicCleanup, "TEMP do Windows", "Análise e limpeza do diretório temporário do sistema.", MaintenanceRisk.Medium, true, MaintenanceAvailability.Planned, false),
        new("recycle-bin", MaintenanceCategory.BasicCleanup, "Lixeira", "Inspeciona itens da Lixeira antes de qualquer ação.", MaintenanceRisk.Medium, false, MaintenanceAvailability.Planned, false),
        new("thumbnail-cache", MaintenanceCategory.BasicCleanup, "Cache de miniaturas", "Avalia o cache de miniaturas do perfil do usuário.", MaintenanceRisk.Low, false, MaintenanceAvailability.Planned, false),

        new("chrome-basic", MaintenanceCategory.Applications, "Chrome — limpeza básica", "Escopo de cache a definir por mecanismo suportado do Chrome.", MaintenanceRisk.Medium, false, MaintenanceAvailability.Planned, false),
        new("chrome-complete", MaintenanceCategory.Applications, "Chrome — limpeza completa", "Categorias de navegação selecionadas pelo usuário, com aviso de sincronização e perda de sessão.", MaintenanceRisk.High, false, MaintenanceAvailability.Planned, false),
        new("chrome-maximum", MaintenanceCategory.Applications, "Chrome — limpeza máxima", "Ainda requer definição de escopo por perfil e confirmação específica; não manipula arquivos nesta versão.", MaintenanceRisk.High, false, MaintenanceAvailability.Planned, false),
        new("app-caches", MaintenanceCategory.Applications, "Caches de aplicativos", "Catálogo de caches de aplicativos com escopo e consequências a definir individualmente.", MaintenanceRisk.Medium, false, MaintenanceAvailability.Planned, false),

        new("wer-reports", MaintenanceCategory.Windows, "Relatórios de erros", "Relatórios do Windows Error Reporting; podem conter dados úteis para diagnóstico.", MaintenanceRisk.Medium, false, MaintenanceAvailability.Planned, false),
        new("disk-cleanup", MaintenanceCategory.Windows, "Limpeza de Disco", "Encaminhamento ou integração com ferramenta nativa, respeitando as categorias selecionadas.", MaintenanceRisk.Medium, false, MaintenanceAvailability.Planned, false),
        new("delivery-optimization", MaintenanceCategory.Windows, "Delivery Optimization", "Cache gerenciado de otimização de entrega do Windows.", MaintenanceRisk.Medium, true, MaintenanceAvailability.Planned, false),
        new("directx-shaders", MaintenanceCategory.Windows, "Cache de shaders DirectX", "Cache regenerável de shaders; espaço estimado antes de qualquer ação.", MaintenanceRisk.Low, false, MaintenanceAvailability.Planned, false),
        new("windows-update", MaintenanceCategory.Windows, "Windows Update", "Diagnóstico de cache de atualização; não interromper serviço nem apagar arquivos diretamente.", MaintenanceRisk.High, true, MaintenanceAvailability.Planned, false),
        new("dism", MaintenanceCategory.Windows, "DISM", "Análise/manutenção do component store por ferramenta oficial, com impacto e elevação explícitos.", MaintenanceRisk.High, true, MaintenanceAvailability.Planned, false),

        new("dns-cache", MaintenanceCategory.Diagnostics, "Cache DNS", "Ação diagnóstica de rede; não é limpeza de armazenamento.", MaintenanceRisk.Low, false, MaintenanceAvailability.Planned, false),
        new("storage-analyzer", MaintenanceCategory.Diagnostics, "Analisador de armazenamento", "Análise somente leitura de espaço, arquivos grandes e duplicatas candidatas.", MaintenanceRisk.Low, false, MaintenanceAvailability.Planned, false),
        new("large-duplicate-files", MaintenanceCategory.Diagnostics, "Arquivos grandes e duplicados", "Análise por escopo escolhido; igualdade por hash é cara e não implica autorização para excluir.", MaintenanceRisk.Low, false, MaintenanceAvailability.Planned, false)
    ]);
}

public enum MaintenanceAnalysisStatus
{
    Completed,
    Empty,
    Partial,
    Cancelled,
    Error
}

public enum MaintenanceActionType
{
    Analysis,
    Execution
}

public enum MaintenanceOperationState
{
    Planned,
    AnalysisAvailable,
    ExecutionAvailable,
    Running,
    Completed,
    PartiallyCompleted,
    Error,
    Cancelled
}

public sealed record MaintenanceExecutionPreviewItem(
    string OperationId,
    bool Included,
    bool ExecutorAvailable,
    bool RequiresAdministrator,
    MaintenanceRisk Risk,
    string Reason);

public sealed record MaintenanceExecutionPreview(Guid Id, IReadOnlyList<MaintenanceExecutionPreviewItem> Items)
{
    public IReadOnlyList<MaintenanceExecutionPreviewItem> Included => Items.Where(item => item.Included).ToArray();
    public IReadOnlyList<MaintenanceExecutionPreviewItem> Unavailable => Items.Where(item => !item.ExecutorAvailable).ToArray();

    /// <summary>Creates a new reviewable preview from the user's explicit operation selection.</summary>
    public MaintenanceExecutionPreview WithSelectedOperations(IEnumerable<string> operationIds)
    {
        var selected = operationIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return this with
        {
            Id = Guid.NewGuid(),
            Items = Items.Select(item => item with { Included = item.ExecutorAvailable && selected.Contains(item.OperationId) }).ToArray()
        };
    }
}

public sealed record MaintenanceExecutionProgress(string OperationId, int CompletedOperations, int TotalOperations, string Message);

public sealed record MaintenanceExecutionApproval(Guid PreviewId, DateTimeOffset ConfirmedAt);

public enum MaintenanceBatchStatus
{
    Completed,
    PartiallyCompleted,
    Cancelled,
    Error
}

public sealed record MaintenanceOperationExecutionResult(
    string OperationId,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    MaintenanceOperationState State,
    int ItemsAffected,
    long BytesRecovered,
    IReadOnlyList<string> Errors);

public sealed record MaintenanceBatchExecutionResult(
    Guid PreviewId,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    MaintenanceBatchStatus Status,
    IReadOnlyList<MaintenanceOperationExecutionResult> Operations);

/// <summary>Execution is opt-in per operation. The current application registers no executors.</summary>
public interface IMaintenanceOperationExecutor
{
    string OperationId { get; }
    bool RequiresAdministrator { get; }
    MaintenanceRisk Risk { get; }
    bool IsApproved { get; }
    bool IsValidated { get; }
    Task<MaintenanceOperationExecutionResult> ExecuteAsync(IProgress<MaintenanceExecutionProgress>? progress, CancellationToken cancellationToken);
}

public sealed class MaintenanceExecutionRegistry
{
    private readonly IReadOnlyDictionary<string, IMaintenanceOperationExecutor> _executors;

    public MaintenanceExecutionRegistry(IEnumerable<IMaintenanceOperationExecutor>? executors = null)
    {
        _executors = (executors ?? []).Where(executor => executor.IsApproved && executor.IsValidated)
            .ToDictionary(executor => executor.OperationId, StringComparer.OrdinalIgnoreCase);
    }

    public bool CanExecute(string operationId) => _executors.ContainsKey(operationId);

    public MaintenanceExecutionPreview Preview(IEnumerable<MaintenanceOperationDescriptor> operations, bool includeHighRisk = false)
    {
        var items = operations.Select(operation =>
        {
            var executorAvailable = _executors.TryGetValue(operation.Id, out var executor);
            var allowedByRisk = includeHighRisk || operation.Risk != MaintenanceRisk.High;
            var included = executorAvailable && allowedByRisk;
            var reason = !executorAvailable
                ? "Nenhum executor aprovado e validado está registrado."
                : !allowedByRisk
                    ? "Operação de risco alto excluída da prévia em lote."
                    : "Executor disponível; confirmação explícita ainda será necessária.";
            return new MaintenanceExecutionPreviewItem(operation.Id, included, executorAvailable,
                executor?.RequiresAdministrator ?? operation.RequiresAdministrator,
                executor?.Risk ?? operation.Risk, reason);
        }).ToArray();
        return new MaintenanceExecutionPreview(Guid.NewGuid(), items);
    }

    public async Task<MaintenanceBatchExecutionResult> ExecuteSequentiallyAsync(
        MaintenanceExecutionPreview preview,
        MaintenanceExecutionApproval approval,
        IProgress<MaintenanceExecutionProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(approval);
        if (approval.PreviewId != preview.Id || approval.ConfirmedAt == default)
            throw new InvalidOperationException("A execução requer confirmação explícita correspondente à prévia atual.");

        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var results = new List<MaintenanceOperationExecutionResult>();
        var included = preview.Included;
        var cancelled = false;
        for (var index = 0; index < included.Count; index++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }
            var item = included[index];
            if (!_executors.TryGetValue(item.OperationId, out var executor)) continue;
            progress?.Report(new MaintenanceExecutionProgress(item.OperationId, index, included.Count, "Executando operação confirmada."));
            try
            {
                results.Add(await executor.ExecuteAsync(progress, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }
            catch (Exception ex)
            {
                results.Add(new MaintenanceOperationExecutionResult(item.OperationId, DateTimeOffset.UtcNow, TimeSpan.Zero,
                    MaintenanceOperationState.Error, 0, 0, [ex.Message]));
            }
            progress?.Report(new MaintenanceExecutionProgress(item.OperationId, index + 1, included.Count, "Operação encerrada."));
        }
        stopwatch.Stop();
        var status = cancelled ? MaintenanceBatchStatus.Cancelled
            : results.Any(result => result.State is MaintenanceOperationState.Error or MaintenanceOperationState.PartiallyCompleted)
                ? results.Any(result => result.State == MaintenanceOperationState.Completed) ? MaintenanceBatchStatus.PartiallyCompleted : MaintenanceBatchStatus.Error
                : MaintenanceBatchStatus.Completed;
        return new MaintenanceBatchExecutionResult(preview.Id, startedAt, stopwatch.Elapsed, status, results);
    }
}

public sealed record MaintenanceAnalysisProgress(int FilesIdentified, long PotentialBytes, int DirectoriesVisited);

public sealed record MaintenanceAnalysisResult(
    string OperationId,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    MaintenanceAnalysisStatus Status,
    int FilesIdentified,
    long PotentialBytes,
    int Failures,
    int ReparsePointsSkipped);

public sealed record MaintenanceHistoryEntry(
    Guid Id,
    string OperationId,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    MaintenanceAnalysisStatus Status,
    int FilesIdentified,
    long PotentialBytes,
    int Failures,
    bool IncompleteOrCancelled)
{
    public MaintenanceActionType Action { get; init; } = MaintenanceActionType.Analysis;
    public int? ItemsAffected { get; init; }
    public long? BytesRecovered { get; init; }
}

public static class MaintenanceHistoryEntryFactory
{
    public static MaintenanceHistoryEntry FromResult(MaintenanceAnalysisResult result) => new(
        Guid.NewGuid(), result.OperationId, result.StartedAt, result.Duration, result.Status,
        result.FilesIdentified, result.PotentialBytes, result.Failures,
        result.Status is MaintenanceAnalysisStatus.Partial or MaintenanceAnalysisStatus.Cancelled or MaintenanceAnalysisStatus.Error);

    public static MaintenanceHistoryEntry FromResult(MaintenanceOperationExecutionResult result) => new(
        Guid.NewGuid(), result.OperationId, result.StartedAt, result.Duration,
        result.State switch
        {
            MaintenanceOperationState.Completed => MaintenanceAnalysisStatus.Completed,
            MaintenanceOperationState.PartiallyCompleted => MaintenanceAnalysisStatus.Partial,
            MaintenanceOperationState.Cancelled => MaintenanceAnalysisStatus.Cancelled,
            _ => MaintenanceAnalysisStatus.Error
        }, 0, 0, result.Errors.Count, result.State is MaintenanceOperationState.PartiallyCompleted or MaintenanceOperationState.Cancelled or MaintenanceOperationState.Error)
    {
        Action = MaintenanceActionType.Execution,
        ItemsAffected = result.ItemsAffected,
        BytesRecovered = result.BytesRecovered
    };
}

public interface IReadOnlyFileTree
{
    string GetFullPath(string path);
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateEntries(string directoryPath);
    FileAttributes GetAttributes(string path);
    long GetFileLength(string path);
}

public interface IUserTempAnalysisService
{
    string TargetDirectory { get; }
    Task<MaintenanceAnalysisResult> AnalyzeAsync(IProgress<MaintenanceAnalysisProgress>? progress, CancellationToken cancellationToken);
}

public interface IMaintenanceHistoryStore
{
    IReadOnlyList<MaintenanceHistoryEntry> Load();
    void Add(MaintenanceHistoryEntry entry);
}
