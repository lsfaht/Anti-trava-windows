namespace KeepAwake.Windows;

internal sealed class InstanceActivationSignal : IDisposable
{
    private readonly EventWaitHandle _activationEvent;
    private readonly RegisteredWaitHandle _waitRegistration;

    internal InstanceActivationSignal(string name, Action activate)
    {
        ArgumentNullException.ThrowIfNull(activate);
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, name);
        _waitRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, timedOut) =>
            {
                if (!timedOut) activate();
            },
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    internal static bool TrySignalExisting(string name)
    {
        // A primary process creates the event immediately after acquiring its mutex.
        // Retry briefly to cover the small interval between those two operations.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var activationEvent = EventWaitHandle.OpenExisting(name);
                return activationEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        return false;
    }

    public void Dispose()
    {
        _waitRegistration.Unregister(null);
        _activationEvent.Dispose();
    }
}
