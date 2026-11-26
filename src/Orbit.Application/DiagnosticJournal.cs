using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Orbit.Application;

public sealed record RequestSnapshot(bool EnergyRequested, bool PresentationSystemRequested,
    bool PresentationDisplayRequested, string Session, string Power, string Window,
    string Verification = "Estado interno baseado no último retorno da API; efeito no Windows não verificado.");

public sealed record DiagnosticEntry(DateTimeOffset Time, string RunId, int ProcessId, string Version,
    string Event, RequestSnapshot? Requests, string? Detail);

public sealed record RunState(string RunId, int ProcessId, string Version, DateTimeOffset StartedAt,
    DateTimeOffset LastSeenAt, string Outcome);

/// <summary>Local, bounded diagnostics. Only the primary instance may own this journal.</summary>
public sealed class DiagnosticJournal
{
    private readonly object _gate = new();
    private readonly string _directory;
    private readonly int _maxBytes;
    private readonly Queue<DiagnosticEntry> _memory = new();
    private RunState _run;
    private bool _ended;
    private bool _faulted;
    public string? LastError { get; private set; }
    public string PreviousRunNotice { get; private set; } = "Primeira execução com o novo diagnóstico; histórico anterior não avaliado.";
    public string LogPath => Path.Combine(_directory, "events.jsonl");
    public string ReportDirectory => Path.Combine(_directory, "reports");
    private string MarkerPath => Path.Combine(_directory, "run-state.json");

    public DiagnosticJournal(string directory, string version, int processId, int maxBytes = 1024 * 1024)
    {
        _directory = directory;
        _maxBytes = maxBytes;
        var now = DateTimeOffset.Now;
        _run = new(Guid.NewGuid().ToString("N"), processId, version, now, now, "running");
        try
        {
            Directory.CreateDirectory(_directory);
            if (File.Exists(MarkerPath))
            {
                if (new FileInfo(MarkerPath).Length > 16384) throw new InvalidDataException();
                var previous = JsonSerializer.Deserialize<RunState>(File.ReadAllText(MarkerPath)) ?? throw new InvalidDataException();
                PreviousRunNotice = previous.Outcome == "normal"
                    ? "Execução anterior encerrada normalmente."
                    : $"Execução anterior sem encerramento normal confirmado ({previous.Outcome}); último sinal de vida persistido: {previous.LastSeenAt:O}. Não comprova crash: pode ser término forçado, desligamento ou falha de gravação.";
                Record("previous_run", detail: PreviousRunNotice);
            }
        }
        catch (Exception ex) { PreviousRunNotice = "Histórico de encerramento anterior indisponível."; SetStorageError(ex); }
        Record("process_started");
        SaveMarker();
    }

    public void Record(string eventName, RequestSnapshot? snapshot = null, string? detail = null)
    {
        lock (_gate)
        {
            var entry = new DiagnosticEntry(DateTimeOffset.Now, _run.RunId, _run.ProcessId, _run.Version, eventName, snapshot, detail);
            _memory.Enqueue(entry);
            while (_memory.Count > 256) _memory.Dequeue();
            try
            {
                Directory.CreateDirectory(_directory);
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length >= _maxBytes)
                    File.Move(LogPath, LogPath + ".previous", overwrite: true);
                File.AppendAllText(LogPath, JsonSerializer.Serialize(entry) + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex) { SetStorageError(ex); }
        }
    }

    // Avoid exception messages, file paths, window titles and document contents in the export.
    public void RecordException(string context, Exception error, RequestSnapshot? snapshot = null, bool fatal = false)
    {
        lock (_gate)
        {
            _faulted |= fatal;
            var errors = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions : new[] { error }.AsEnumerable();
            var summary = string.Join("; ", errors.Take(8).Select(e =>
                $"{e.GetType().FullName}; HRESULT=0x{e.HResult:X8}; " +
                (e is System.ComponentModel.Win32Exception win32 ? $"Win32={win32.NativeErrorCode}; " : "") +
                string.Join(" > ", (new StackTrace(e, false).GetFrames() ?? []).Take(8).Select(f => f.GetMethod()?.Name))));
            Record(context, snapshot, summary);
            if (fatal) { _run = _run with { Outcome = "faulted", LastSeenAt = DateTimeOffset.Now }; SaveMarker(); }
        }
    }

    public void Heartbeat(RequestSnapshot snapshot)
    {
        lock (_gate)
        {
            if (_ended) return;
            Record("heartbeat", snapshot);
            _run = _run with { LastSeenAt = DateTimeOffset.Now };
            SaveMarker();
        }
    }

    public void Complete(RequestSnapshot snapshot, bool cleanupSucceeded)
    {
        lock (_gate)
        {
            if (_ended) return;
            _ended = true;
            _run = _run with { LastSeenAt = DateTimeOffset.Now, Outcome = _faulted ? "faulted" : cleanupSucceeded ? "normal" : "cleanup_error" };
            Record("process_ended", snapshot, _run.Outcome);
            SaveMarker();
        }
    }

    public string Export(string directory, RequestSnapshot snapshot, IEnumerable<string> checks)
    {
        lock (_gate)
        {
            var report = new StringBuilder("ORBIT — DIAGNÓSTICO LOCAL\n");
            report.AppendLine($"Exportado em: {DateTimeOffset.Now:O}\nVersão: {_run.Version}\nPID: {_run.ProcessId}\nExecução: {_run.RunId}");
            report.AppendLine(PreviousRunNotice);
            report.AppendLine("Este relatório não identifica a causa de bloqueio nem comprova que pedidos foram honrados pelo Windows.");
            report.AppendLine("Horários incluem fuso. Estado da sessão é o último evento recebido, não uma consulta contínua. Não inclui o log legado.");
            report.AppendLine("Verificação: " + snapshot.Verification);
            report.AppendLine(JsonSerializer.Serialize(snapshot));
            report.AppendLine("CONSULTAS E LIMITAÇÕES");
            foreach (var check in checks) report.AppendLine(check);
            report.AppendLine("Falha de persistência observada: " + (LastError ?? "nenhuma"));
            report.AppendLine("EVENTOS LOCAIS (retenção limitada)");
            foreach (var path in new[] { LogPath + ".previous", LogPath })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    if (new FileInfo(path).Length > _maxBytes + 65536) { report.AppendLine("Arquivo de eventos excedeu o limite; omitido."); continue; }
                    foreach (var line in File.ReadLines(path))
                    {
                        // Re-serialize the known schema; malformed or foreign records are not exported verbatim.
                        try { var entry = JsonSerializer.Deserialize<DiagnosticEntry>(line); if (entry is not null) report.AppendLine(JsonSerializer.Serialize(entry)); }
                        catch (JsonException) { report.AppendLine("Registro incompleto ou inválido omitido."); }
                    }
                }
                catch (Exception ex) { report.AppendLine("Leitura de eventos indisponível: " + ex.GetType().Name); }
            }
            report.AppendLine("EVENTOS EM MEMÓRIA DA EXECUÇÃO ATUAL (podem repetir os registros persistidos)");
            foreach (var entry in _memory) report.AppendLine(JsonSerializer.Serialize(entry));
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, $"Orbit-diagnostico-{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.txt");
            File.WriteAllText(destination, report.ToString(), Encoding.UTF8);
            return destination;
        }
    }

    private void SaveMarker()
    {
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(MarkerPath + ".tmp", JsonSerializer.Serialize(_run));
            File.Move(MarkerPath + ".tmp", MarkerPath, overwrite: true);
        }
        catch (Exception ex) { SetStorageError(ex); }
    }
    private void SetStorageError(Exception ex) => LastError = $"{ex.GetType().Name}; HRESULT=0x{ex.HResult:X8}";
}
