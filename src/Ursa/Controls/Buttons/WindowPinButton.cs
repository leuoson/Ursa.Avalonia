using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Ursa.Common.Windowing;

namespace Ursa.Controls;

/// <summary>
/// IconButton-based pin control that issues pin, release, or toggle commands without maintaining a checked state.
/// </summary>
public class WindowPinButton : IconButton
{
    public static readonly StyledProperty<Window?> TargetWindowProperty =
        AvaloniaProperty.Register<WindowPinButton, Window?>(nameof(TargetWindow));

    public static readonly StyledProperty<IWindowStackingService?> PinningServiceProperty =
        AvaloniaProperty.Register<WindowPinButton, IWindowStackingService?>(nameof(PinningService));

    public static readonly StyledProperty<WindowPinCommandAction> ActionProperty =
        AvaloniaProperty.Register<WindowPinButton, WindowPinCommandAction>(nameof(Action), WindowPinCommandAction.Toggle);

    public static readonly StyledProperty<object?> UnpinnedIconProperty =
        AvaloniaProperty.Register<WindowPinButton, object?>(nameof(UnpinnedIcon));

    public static readonly StyledProperty<object?> PinnedIconProperty =
        AvaloniaProperty.Register<WindowPinButton, object?>(nameof(PinnedIcon));

    public static readonly StyledProperty<bool> SyncIconWithStateProperty =
        AvaloniaProperty.Register<WindowPinButton, bool>(nameof(SyncIconWithState), true);

    private Window? _attachedWindow;
    private UrsaWindow? _attachedUrsaWindow;
    private bool _isExecuting;
    private bool _isPinningSupported;
    private bool _isPinned;

    static WindowPinButton()
    {
        TargetWindowProperty.Changed.AddClassHandler<WindowPinButton>((button, _) => button.AttachToWindow());
        PinnedIconProperty.Changed.AddClassHandler<WindowPinButton>((button, _) => button.UpdateIcon());
        UnpinnedIconProperty.Changed.AddClassHandler<WindowPinButton>((button, _) => button.UpdateIcon());
        SyncIconWithStateProperty.Changed.AddClassHandler<WindowPinButton>((button, _) => button.UpdateIcon());
    }

    public Window? TargetWindow
    {
        get => GetValue(TargetWindowProperty);
        set => SetValue(TargetWindowProperty, value);
    }

    public IWindowStackingService? PinningService
    {
        get => GetValue(PinningServiceProperty);
        set => SetValue(PinningServiceProperty, value);
    }

    public WindowPinCommandAction Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    public object? UnpinnedIcon
    {
        get => GetValue(UnpinnedIconProperty);
        set => SetValue(UnpinnedIconProperty, value);
    }

    public object? PinnedIcon
    {
        get => GetValue(PinnedIconProperty);
        set => SetValue(PinnedIconProperty, value);
    }

    public bool SyncIconWithState
    {
        get => GetValue(SyncIconWithStateProperty);
        set => SetValue(SyncIconWithStateProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachToWindow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachFromWindow();
    }

    protected override void OnClick()
    {
        base.OnClick();
        _ = ApplyActionAsync();
    }

    private async Task ApplyActionAsync()
    {
        if (_isExecuting)
        {
            return;
        }

        if (!EnsureWindowAttachment() || !_isPinningSupported)
        {
            return;
        }

        var window = _attachedUrsaWindow ?? _attachedWindow;
        if (window is null)
        {
            return;
        }

        bool targetState = DetermineTargetState(window);

        _isExecuting = true;
        UpdateEnabledState();

        WindowStackingResult result = await WindowPinController.SetPinStateAsync(window, targetState, PinningService);
        _isExecuting = false;
        UpdateEnabledState();

        if (result.IsFailure)
        {
            WindowPinController.LogFailure(this, result);
            return;
        }

        _isPinned = targetState;
        UpdateIcon();
    }

    private bool DetermineTargetState(Window window)
    {
        return Action switch
        {
            WindowPinCommandAction.Pin => true,
            WindowPinCommandAction.Release => false,
            _ => WindowPinController.TryGetCurrentState(window, out var current) ? !current : !_isPinned
        };
    }

    private bool EnsureWindowAttachment()
    {
        if (_attachedWindow is not null)
        {
            return true;
        }

        AttachToWindow();
        return _attachedWindow is not null;
    }

    private void AttachToWindow()
    {
        DetachFromWindow();

        var window = TargetWindow ?? ResolveWindowFromTree();
        if (window is null)
        {
            UpdatePinningAvailability(false);
            return;
        }

        _attachedWindow = window;
        if (window is UrsaWindow ursaWindow)
        {
            _attachedUrsaWindow = ursaWindow;
            _attachedUrsaWindow.PropertyChanged += OnUrsaWindowPropertyChanged;
            UpdateFromUrsaWindow();
            return;
        }

        UpdateExternalWindowState(window);
    }

    private void DetachFromWindow()
    {
        if (_attachedUrsaWindow is not null)
        {
            _attachedUrsaWindow.PropertyChanged -= OnUrsaWindowPropertyChanged;
        }

        _attachedUrsaWindow = null;
        _attachedWindow = null;
        _isPinned = false;
        UpdatePinningAvailability(false);
    }

    private void OnUrsaWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == UrsaWindow.IsPinnedToDesktopBottomProperty ||
            e.Property == UrsaWindow.CanPinToDesktopBottomProperty)
        {
            UpdateFromUrsaWindow();
        }
    }

    private void UpdateFromUrsaWindow()
    {
        if (_attachedUrsaWindow is null)
        {
            return;
        }

        _isPinned = _attachedUrsaWindow.IsPinnedToDesktopBottom;
        UpdatePinningAvailability(_attachedUrsaWindow.CanPinToDesktopBottom);
        UpdateIcon();
    }

    private void UpdateExternalWindowState(Window window)
    {
        if (!WindowPinController.TryGetCurrentState(window, out var current))
        {
            current = false;
        }

        _isPinned = current;
        UpdatePinningAvailability(WindowPinController.CanPin(window));
        UpdateIcon();
    }

    private void UpdatePinningAvailability(bool canPin)
    {
        _isPinningSupported = canPin;
        UpdateEnabledState();
    }

    private void UpdateEnabledState()
    {
        SetCurrentValue(IsEnabledProperty, _isPinningSupported && !_isExecuting);
    }

    private void UpdateIcon()
    {
        if (!SyncIconWithState)
        {
            return;
        }

        var icon = _isPinned ? PinnedIcon : UnpinnedIcon;
        if (icon is not null)
        {
            SetCurrentValue(IconProperty, icon);
        }
    }

    private Window? ResolveWindowFromTree()
    {
        return this.FindAncestorOfType<UrsaWindow>()
            ?? (this.GetVisualRoot() as Window)
            ?? this.FindAncestorOfType<Window>();
    }
}
