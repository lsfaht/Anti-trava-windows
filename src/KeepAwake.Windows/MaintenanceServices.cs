using System.Text.Json;
using KeepAwake.Core;

namespace KeepAwake.Windows;

internal sealed class WindowsReadOnlyFileTree : IReadOnlyFileTree
{
    public string GetFullPath(string path) => Path.GetFullPath(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public IEnumerable<string> EnumerateEntries(string directoryPath) => Directory.EnumerateFileSystemEntries(directoryPath);
    public FileAttributes GetAttributes(string path) => File.GetAttributes(path);
    public long GetFileLength(string path) => new FileInfo(path).Length;
}

internal sealed class WindowsUserTempAnalysisService : IUserTempAnalysisService
{
    private readonly UserTempAnalyzer _analyzer = new(new WindowsReadOnlyFileTree());
    public string TargetDirectory { get; } = Path.GetFullPath(Path.GetTempPath());

    public Task<MaintenanceAnalysisResult> AnalyzeAsync(IProgress<MaintenanceAnalysisProgress>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => _analyzer.Analyze(TargetDirectory, progress, cancellationToken), CancellationToken.None);
}

internal sealed class JsonMaintenanceHistoryStore : IMaintenanceHistoryStore
{
    internal static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    internal const int MaximumRecords = 500;
    internal const int MaximumFileBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();

    internal JsonMaintenanceHistoryStore(string filePath, TimeProvider? timeProvider = null)
    {
        _filePath = Path.GetFullPath(filePath);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<MaintenanceHistoryEntry> Load()
    {
        lock (_gate)
        {
            var records = ReadRecords();
            var retained = ApplyRetention(records);
            if (retained.Count != records.Count) WriteRecords(retained);
            return retained.OrderByDescending(record => record.StartedAt).ToArray();
        }
    }

    public void Add(MaintenanceHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            var records = ApplyRetention(ReadRecords());
            records.Add(entry);
            if (records.Count > MaximumRecords)
                records = records.OrderByDescending(record => record.StartedAt).Take(MaximumRecords).ToList();

            while (records.Count > 0)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(records, JsonOptions);
                if (bytes.Length <= MaximumFileBytes)
                {
                    WriteBytes(bytes);
                    return;
                }
                records.Remove(records.OrderBy(record => record.StartedAt).First());
            }

            WriteRecords(records);
        }
    }

    private List<MaintenanceHistoryEntry> ReadRecords()
    {
        if (!File.Exists(_filePath)) return [];
        var bytes = File.ReadAllBytes(_filePath);
        if (bytes.Length > MaximumFileBytes) throw new InvalidDataException("O histórico de manutenção excede o limite de leitura.");
        return JsonSerializer.Deserialize<List<MaintenanceHistoryEntry>>(bytes, JsonOptions) ?? [];
    }

    private List<MaintenanceHistoryEntry> ApplyRetention(List<MaintenanceHistoryEntry> records)
    {
        var threshold = _timeProvider.GetUtcNow() - Retention;
        return records.Where(record => record.StartedAt >= threshold)
            .OrderByDescending(record => record.StartedAt)
            .Take(MaximumRecords)
            .ToList();
    }

    private void WriteRecords(List<MaintenanceHistoryEntry> records) =>
        WriteBytes(JsonSerializer.SerializeToUtf8Bytes(records, JsonOptions));

    private void WriteBytes(byte[] bytes)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("O caminho do histórico não contém uma pasta.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _filePath + ".tmp";
        File.WriteAllBytes(temporaryPath, bytes);
        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}
