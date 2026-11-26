namespace Orbit.Application;

public enum ObservedSystemEvent { Locked, Unlocked, Disconnected, Connected, Suspend, Resume, PowerChanged, SessionEnding }

public sealed class OrbitLifetime(KeepAwakeViewModel power, DiagnosticJournal journal)
{
    public bool ExitRequested { get; private set; }
    public bool IsClosed { get; private set; }
    public string Visibility { get; private set; } = "visible";
    public string Session { get; private set; } = "unknown (no event received)";
    public string Power { get; private set; } = "unknown (no event received)";
    public RequestSnapshot Snapshot => new(power.IsActive, power.IsPresentationSystemRequested,
        power.IsPresentationDisplayRequested, Session, Power, Visibility, power.VerificationNotice);

    public void Hide() { if (ExitRequested || IsClosed) return; Visibility = "tray"; journal.Record("window_hidden", Snapshot); }
    public void Restore() { if (ExitRequested || IsClosed) return; Visibility = "visible"; journal.Record("window_restored", Snapshot); }
    public void RequestExit() { if (ExitRequested || IsClosed) return; ExitRequested = true; journal.Record("exit_requested", Snapshot); }
    public void Observe(ObservedSystemEvent observed)
    {
        if (IsClosed) return;
        switch (observed)
        {
            case ObservedSystemEvent.Locked: Session = "locked"; break;
            case ObservedSystemEvent.Unlocked: Session = "unlocked"; break;
            case ObservedSystemEvent.Disconnected: Session = "disconnected"; break;
            case ObservedSystemEvent.Connected: Session = "connected (lock state unknown)"; break;
            case ObservedSystemEvent.Suspend: Power = "suspend observed; requests not revalidated"; break;
            case ObservedSystemEvent.Resume: Power = "resume observed; requests not revalidated"; break;
            case ObservedSystemEvent.SessionEnding: ExitRequested = true; break;
        }
        journal.Record("system_" + observed, Snapshot);
        if (observed is ObservedSystemEvent.Suspend or ObservedSystemEvent.Resume)
            power.MarkVerificationUncertain();
    }
    public void Finish(bool otherCleanupSucceeded = true)
    {
        if (IsClosed) return;
        IsClosed = true;
        Visibility = "closed";
        power.Dispose();
        journal.Complete(Snapshot, ExitRequested && otherCleanupSucceeded && power.ReleaseSucceeded);
    }
}
