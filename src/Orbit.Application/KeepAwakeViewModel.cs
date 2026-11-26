using System.ComponentModel;
using System.Runtime.CompilerServices;
using KeepAwake.Core;

namespace Orbit.Application;

public sealed class KeepAwakeViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly PowerRequest _powerRequest;
    private readonly Func<PresentationModeRequest> _presentationFactory;
    private PresentationModeRequest? _presentationRequest;
    private bool _keepDisplayOn = true;
    private bool _isBusy;
    private string _status = "Inativa";
    private string _presentationStatus = "Inativo";
    private string _message = "Pronto para manter o sistema acordado.";
    private bool _disposed;

    public KeepAwakeViewModel(IExecutionStateApi powerApi, Func<PresentationModeRequest> presentationFactory)
    {
        _powerRequest = new PowerRequest(powerApi ?? throw new ArgumentNullException(nameof(powerApi)));
        _presentationFactory = presentationFactory ?? throw new ArgumentNullException(nameof(presentationFactory));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string, Exception?>? DiagnosticEvent;
    public bool ReleaseSucceeded { get; private set; } = true;
    private bool _powerUncertain, _presentationUncertain;
    public string VerificationNotice => _powerUncertain || _presentationUncertain
        ? "Suspensão ou retomada observada: há pedidos não revalidados. Interrompa e ative novamente se necessário."
        : "Estado baseado no retorno da API. O efeito sobre bloqueios não foi verificado.";
    public bool IsPresentationSystemRequested => _presentationRequest?.IsSystemRequested == true;
    public bool IsPresentationDisplayRequested => _presentationRequest?.IsDisplayRequested == true;
    public bool KeepDisplayOn { get => _keepDisplayOn; set { if (Set(ref _keepDisplayOn, value)) OnPropertyChanged(nameof(DisplayDescription)); } }
    public string DisplayDescription => KeepDisplayOn ? "Sistema e tela" : "Somente sistema";
    public bool IsActive => _powerRequest.IsEnabled;
    public bool IsPresentationActive => _presentationRequest?.IsEnabled == true;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }
    public string Status { get => _status; private set => Set(ref _status, value); }
    public string PresentationStatus { get => _presentationStatus; private set => Set(ref _presentationStatus, value); }
    public string Message { get => _message; private set => Set(ref _message, value); }
    public string ToggleLabel => IsActive ? "Interromper manutenção" : "Iniciar manutenção";
    public string PresentationLabel => IsPresentationActive ? "Interromper modo de apresentação" : "Ativar modo de apresentação";

    public bool TogglePower()
    {
        if (_disposed) return false;
        IsBusy = true;
        try
        {
            if (_powerRequest.IsEnabled) { _powerRequest.Disable(); Status = "Inativa"; Message = "Solicitação de energia liberada."; }
            else { _powerRequest.Enable(KeepDisplayOn); Status = "Solicitação aceita pela API"; Message = "Pedido de energia aceito pela API. Isso não comprova prevenção de bloqueio."; }
            _powerUncertain = false; OnPropertyChanged(nameof(VerificationNotice));
            DiagnosticEvent?.Invoke(IsActive ? "energy_accepted" : "energy_released", null);
            OnPropertyChanged(nameof(IsActive)); OnPropertyChanged(nameof(ToggleLabel));
            return true;
        }
        catch (Exception ex) { Status = _powerRequest.IsEnabled ? "Pedido pendente — erro" : "Erro — inativa"; Message = ex.Message; DiagnosticEvent?.Invoke("energy_error", ex); return false; }
        finally { OnPropertyChanged(nameof(IsActive)); OnPropertyChanged(nameof(ToggleLabel)); IsBusy = false; }
    }

    public bool TogglePresentation()
    {
        if (_disposed) return false;
        IsBusy = true;
        try
        {
            if (IsPresentationActive) { _presentationRequest!.Disable(); PresentationStatus = "Inativo"; Message = "Modo de apresentação desativado."; }
            else { _presentationRequest ??= _presentationFactory(); _presentationRequest.Enable(); PresentationStatus = "Solicitações aceitas pela API"; Message = "Pedidos de sistema e tela aceitos pela API. A prevenção de bloqueio não foi verificada."; }
            _presentationUncertain = false; OnPropertyChanged(nameof(VerificationNotice));
            DiagnosticEvent?.Invoke(IsPresentationActive ? "presentation_accepted" : "presentation_released", null);
            OnPropertyChanged(nameof(IsPresentationActive)); OnPropertyChanged(nameof(PresentationLabel));
            return true;
        }
        catch (Exception ex) { PresentationStatus = IsPresentationActive ? "Pedidos pendentes ou parciais — erro" : "Erro — inativo"; Message = ex.Message; DiagnosticEvent?.Invoke("presentation_error", ex); return false; }
        finally { OnPropertyChanged(nameof(IsPresentationActive)); OnPropertyChanged(nameof(PresentationLabel)); IsBusy = false; }
    }

    public void MarkVerificationUncertain()
    {
        _powerUncertain = IsActive; _presentationUncertain = IsPresentationActive;
        if (IsActive) Status = "Pedido anterior — não revalidado";
        if (IsPresentationActive) PresentationStatus = "Pedidos anteriores — não revalidados";
        OnPropertyChanged(nameof(VerificationNotice));
    }

    public void Dispose()
    {
        if (_disposed) return;
        List<Exception>? errors = null;
        try { _powerRequest.Dispose(); } catch (Exception ex) { (errors ??= []).Add(ex); }
        if (_presentationRequest is not null)
            try { _presentationRequest.Dispose(); } catch (Exception ex) { (errors ??= []).Add(ex); }
        _disposed = true;
        ReleaseSucceeded = errors is null;
        if (errors is not null) { Message = "Falha ao liberar uma solicitação: " + string.Join("; ", errors.Select(e => e.Message)); DiagnosticEvent?.Invoke("requests_cleanup_error", new AggregateException(errors)); }
        else DiagnosticEvent?.Invoke("requests_released_on_exit", null);
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(name); return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
