using System.Diagnostics;

namespace KeepAwake.Core;

public sealed class UserTempAnalyzer(IReadOnlyFileTree fileTree)
{
    public const string OperationId = "user-temp";

    public MaintenanceAnalysisResult Analyze(
        string tempDirectory,
        IProgress<MaintenanceAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;
        var stopwatch = Stopwatch.StartNew();
        var files = 0;
        var directories = 0;
        var failures = 0;
        var reparsePointsSkipped = 0;
        long bytes = 0;
        var cancelled = false;

        try
        {
            if (string.IsNullOrWhiteSpace(tempDirectory) || !Path.IsPathFullyQualified(tempDirectory))
                throw new ArgumentException("O caminho TEMP precisa ser absoluto.", nameof(tempDirectory));

            var root = Path.TrimEndingDirectorySeparator(fileTree.GetFullPath(tempDirectory));
            if (!fileTree.DirectoryExists(root)) throw new DirectoryNotFoundException("A pasta TEMP não foi encontrada.");
            var rootAttributes = fileTree.GetAttributes(root);
            if ((rootAttributes & FileAttributes.Directory) == 0)
                throw new IOException("O caminho TEMP não é um diretório.");
            if ((rootAttributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("A pasta TEMP é um link ou reparse point; a análise foi interrompida por segurança.");

            var pending = new Stack<string>();
            pending.Push(root);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var rootPrefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;

            while (pending.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }

                var directory = pending.Pop();
                directories++;
                try
                {
                    foreach (var entry in fileTree.EnumerateEntries(directory))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            cancelled = true;
                            break;
                        }

                        try
                        {
                            var fullEntry = fileTree.GetFullPath(entry);
                            if (!fullEntry.StartsWith(rootPrefix, comparison))
                            {
                                failures++;
                                continue;
                            }

                            var attributes = fileTree.GetAttributes(fullEntry);
                            if ((attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                reparsePointsSkipped++;
                                continue;
                            }

                            if ((attributes & FileAttributes.Directory) != 0)
                            {
                                pending.Push(fullEntry);
                                continue;
                            }

                            var length = fileTree.GetFileLength(fullEntry);
                            if (length < 0) throw new IOException("O tamanho do arquivo é inválido.");
                            files++;
                            bytes = long.MaxValue - bytes < length ? long.MaxValue : bytes + length;
                            progress?.Report(new MaintenanceAnalysisProgress(files, bytes, directories));
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            cancelled = true;
                            break;
                        }
                        catch
                        {
                            failures++;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    cancelled = true;
                    break;
                }
                catch
                {
                    failures++;
                }
            }

            if (cancellationToken.IsCancellationRequested) cancelled = true;
            var status = cancelled ? MaintenanceAnalysisStatus.Cancelled
                : failures > 0 ? MaintenanceAnalysisStatus.Partial
                : files == 0 ? MaintenanceAnalysisStatus.Empty
                : MaintenanceAnalysisStatus.Completed;
            stopwatch.Stop();
            return new MaintenanceAnalysisResult(OperationId, startedAt, stopwatch.Elapsed, status, files, bytes, failures, reparsePointsSkipped);
        }
        catch
        {
            stopwatch.Stop();
            return new MaintenanceAnalysisResult(OperationId, startedAt, stopwatch.Elapsed, MaintenanceAnalysisStatus.Error, files, bytes, Math.Max(1, failures), reparsePointsSkipped);
        }
    }
}
