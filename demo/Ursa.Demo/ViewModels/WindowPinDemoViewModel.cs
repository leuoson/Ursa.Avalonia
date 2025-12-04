using System;
using System.Threading.Tasks;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ursa.Common.Windowing;
using Ursa.Controls;

namespace Ursa.Demo.ViewModels;

public partial class WindowPinDemoViewModel : ObservableObject
{
    private readonly AsyncRelayCommand _togglePinCommand;
    private readonly RelayCommand _refreshStateCommand;
    private UrsaWindow? _window;
    private bool _suppressPinSync;

    public WindowPinDemoViewModel()
    {
        _togglePinCommand = new AsyncRelayCommand(TogglePinAsync, () => CanTogglePin);
        _refreshStateCommand = new RelayCommand(UpdateState);
        StatusMessage = "Waiting for UrsaWindow context...";
    }

    public IAsyncRelayCommand TogglePinCommand => _togglePinCommand;

    public IRelayCommand RefreshStateCommand => _refreshStateCommand;

    [ObservableProperty]
    private bool _isPinSupported;

    [ObservableProperty]
    private bool _isPinned;

    partial void OnIsPinnedChanged(bool value)
    {
        if (_suppressPinSync)
        {
            return;
        }

        _ = SetPinStateAsync(value);
    }

    [ObservableProperty]
    private bool _canTogglePin;

    [ObservableProperty]
    private string? _statusMessage;

    public void AttachWindow(UrsaWindow window)
    {
        if (_window == window)
        {
            UpdateState();
            return;
        }

        DetachWindow();
        _window = window;
        _window.PropertyChanged += OnWindowPropertyChanged;
        UpdateState();
    }

    public void DetachWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.PropertyChanged -= OnWindowPropertyChanged;
        _window = null;
        UpdateState();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == UrsaWindow.IsPinnedToDesktopBottomProperty ||
            e.Property == UrsaWindow.CanPinToDesktopBottomProperty)
        {
            UpdateState();
        }
    }

    private void UpdateState()
    {
        if (_window is null)
        {
            IsPinSupported = false;
            _suppressPinSync = true;
            IsPinned = false;
            _suppressPinSync = false;
            CanTogglePin = false;
            StatusMessage = "Attach this demo to a UrsaWindow to control pinning.";
            _togglePinCommand.NotifyCanExecuteChanged();
            return;
        }

        IsPinSupported = _window.CanPinToDesktopBottom;
        _suppressPinSync = true;
        IsPinned = _window.IsPinnedToDesktopBottom;
        _suppressPinSync = false;
        CanTogglePin = _window.CanPinToDesktopBottom;
        _togglePinCommand.NotifyCanExecuteChanged();

        StatusMessage = IsPinSupported
            ? (IsPinned
                ? "Window is pinned below other windows. Use the controls above to release it."
                : "Window can be pinned. Use the title bar pin button or the action below.")
            : "Pinning is not supported on this platform for the current window.";
    }

    private async Task TogglePinAsync()
    {
        await SetPinStateAsync(!IsPinned);
    }

    private async Task SetPinStateAsync(bool targetState)
    {
        if (!CanTogglePin || _window is null)
        {
            StatusMessage = "Pin toggle is not available right now.";
            _suppressPinSync = true;
            IsPinned = false;
            _suppressPinSync = false;
            return;
        }

        if (_window.IsPinnedToDesktopBottom == targetState)
        {
            return;
        }

        var service = _window.PinningService ?? WindowStackingService.Instance;
        var result = targetState
            ? await service.PinBottomAsync(_window)
            : await service.ReleaseAsync(_window);

        if (result.IsSuccess)
        {
            _window.SetCurrentValue(UrsaWindow.IsPinnedToDesktopBottomProperty, targetState);
            UpdateState();
        }
        else
        {
            _suppressPinSync = true;
            IsPinned = _window.IsPinnedToDesktopBottom;
            _suppressPinSync = false;
            StatusMessage = result.Message ?? "Failed to toggle pin state.";
        }
    }
}
