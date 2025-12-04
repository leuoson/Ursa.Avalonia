using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Ursa.Common.Windowing;

namespace Ursa.Controls;

public enum WindowPinCommandAction
{
    Toggle,
    Pin,
    Release
}

/// <summary>
/// ICommand implementation that can pin, unpin, or toggle any Avalonia window, allowing reuse with arbitrary buttons.
/// </summary>
public class WindowPinCommand : AvaloniaObject, ICommand
{
    public static readonly StyledProperty<Window?> TargetWindowProperty =
        AvaloniaProperty.Register<WindowPinCommand, Window?>(nameof(TargetWindow));

    public static readonly StyledProperty<WindowPinCommandAction> ActionProperty =
        AvaloniaProperty.Register<WindowPinCommand, WindowPinCommandAction>(nameof(Action), WindowPinCommandAction.Toggle);

    public static readonly StyledProperty<IWindowStackingService?> PinningServiceProperty =
        AvaloniaProperty.Register<WindowPinCommand, IWindowStackingService?>(nameof(PinningService));

    private bool _isExecuting;

    public Window? TargetWindow
    {
        get => GetValue(TargetWindowProperty);
        set => SetValue(TargetWindowProperty, value);
    }

    public WindowPinCommandAction Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    public IWindowStackingService? PinningService
    {
        get => GetValue(PinningServiceProperty);
        set => SetValue(PinningServiceProperty, value);
    }

    public event EventHandler? CanExecuteChanged;

    static WindowPinCommand()
    {
        TargetWindowProperty.Changed.AddClassHandler<WindowPinCommand>((cmd, _) => cmd.RaiseCanExecuteChanged());
        ActionProperty.Changed.AddClassHandler<WindowPinCommand>((cmd, _) => cmd.RaiseCanExecuteChanged());
    }

    public bool CanExecute(object? parameter)
    {
        if (_isExecuting)
        {
            return false;
        }

        return ResolveWindow(parameter) is not null;
    }

    public async void Execute(object? parameter)
    {
        var window = ResolveWindow(parameter);
        if (window is null || _isExecuting)
        {
            return;
        }

        bool targetState = DetermineTargetState(window);

        try
        {
            _isExecuting = true;
            RaiseCanExecuteChanged();

            WindowStackingResult result = await WindowPinController.SetPinStateAsync(window, targetState, PinningService);
            if (result.IsFailure)
            {
                WindowPinController.LogFailure(this, result);
            }
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    private bool DetermineTargetState(Window window)
    {
        return Action switch
        {
            WindowPinCommandAction.Pin => true,
            WindowPinCommandAction.Release => false,
            _ => WindowPinController.TryGetCurrentState(window, out var current) ? !current : true
        };
    }

    private Window? ResolveWindow(object? parameter)
    {
        if (parameter is Window window)
        {
            return window;
        }

        if (parameter is Visual visual)
        {
            if (visual is Window visualWindow)
            {
                return visualWindow;
            }

            return visual.FindAncestorOfType<Window>() ?? visual.GetVisualRoot() as Window;
        }

        return TargetWindow;
    }

    private void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, System.EventArgs.Empty);
}
