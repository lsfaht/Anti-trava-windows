namespace KeepAwake.Core;

public enum PresentationPowerRequestType
{
    DisplayRequired = 0,
    SystemRequired = 1
}

public interface IPresentationPowerApi : IDisposable
{
    void Set(PresentationPowerRequestType requestType);
    void Clear(PresentationPowerRequestType requestType);
}

public sealed class PresentationModeRequest(IPresentationPowerApi api) : IDisposable
{
    private bool _systemRequired;
    private bool _displayRequired;
    private bool _disposed;

    public bool IsEnabled => _systemRequired || _displayRequired;

    public void Enable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_systemRequired)
        {
            api.Set(PresentationPowerRequestType.SystemRequired);
            _systemRequired = true;
        }

        if (_displayRequired) return;
        try
        {
            api.Set(PresentationPowerRequestType.DisplayRequired);
            _displayRequired = true;
        }
        catch (Exception activationError)
        {
            if (!_systemRequired) throw;
            try
            {
                api.Clear(PresentationPowerRequestType.SystemRequired);
                _systemRequired = false;
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("A ativação do modo de apresentação falhou e a limpeza parcial também falhou.", activationError, cleanupError);
            }
            throw;
        }
    }

    public void Disable()
    {
        List<Exception>? errors = null;
        if (_displayRequired)
        {
            try { api.Clear(PresentationPowerRequestType.DisplayRequired); _displayRequired = false; }
            catch (Exception ex) { (errors ??= []).Add(ex); }
        }
        if (_systemRequired)
        {
            try { api.Clear(PresentationPowerRequestType.SystemRequired); _systemRequired = false; }
            catch (Exception ex) { (errors ??= []).Add(ex); }
        }
        if (errors is not null) throw new AggregateException("Não foi possível liberar todas as solicitações do modo de apresentação.", errors);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Exception? releaseError = null;
        Exception? disposeError = null;
        try { Disable(); }
        catch (Exception ex) { releaseError = ex; }
        try { api.Dispose(); }
        catch (Exception ex) { disposeError = ex; }

        if (disposeError is null)
        {
            _systemRequired = false;
            _displayRequired = false;
            _disposed = true;
        }

        if (releaseError is not null && disposeError is not null) throw new AggregateException(releaseError, disposeError);
        if (releaseError is not null) throw releaseError;
        if (disposeError is not null) throw disposeError;
    }
}
