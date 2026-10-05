using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DatabaseLoadBalancing.Client.ViewModels;

public sealed class ConnectionStatusViewModel : INotifyPropertyChanged
{
    private string _status = "未连接";
    private string _message = "请连接服务端。";

    public string Status { get => _status; private set => Set(ref _status, value); }

    public string Message { get => _message; private set => Set(ref _message, value); }

    public bool IsAvailable => Status == "Ready";

    public void SetReady() { Status = "Ready"; Message = "服务端已连接。"; OnPropertyChanged(nameof(IsAvailable)); }

    public void SetUnavailable(string message) { Status = "Unavailable"; Message = message; OnPropertyChanged(nameof(IsAvailable)); }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

