using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Ursa.Common.Windowing;

namespace Ursa.Controls;

/// <summary>
/// A self-contained icon toggle button that tracks the pin-to-bottom state of its host window.
/// </summary>
public class WindowPinToggleButton : IconToggleButton
{
    public static readonly StyledProperty<Window?> TargetWindowProperty =
        AvaloniaProperty.Register<WindowPinToggleButton, Window?>(nameof(TargetWindow));

    public static readonly StyledProperty<IWindowStackingService?> PinningServiceProperty =
        AvaloniaProperty.Register<WindowPinToggleButton, IWindowStackingService?>(nameof(PinningService));

    private Window? _attachedWindow;
    private UrsaWindow? _attachedUrsaWindow;
    private bool _isInternalUpdate;
    private bool _isToggleInFlight;
    private bool _lastKnownPinned;
    private bool _isPinningSupported;

    static WindowPinToggleButton()
    {
        TargetWindowProperty.Changed.AddClassHandler<WindowPinToggleButton, Window?>((button, args) =>
            button.OnTargetWindowChanged(args));
        IsCheckedProperty.Changed.AddClassHandler<WindowPinToggleButton, bool?>((button, args) =>
            button.OnIsCheckedPropertyChanged(args));
    }

    /// <summary>
    /// Gets or sets the window that should respond to the pin command. When unset, the control will attempt
    /// to locate the nearest <see cref="UrsaWindow"/> in the visual tree, falling back to the visual root window.
    /// </summary>
    public Window? TargetWindow
    {
        get => GetValue(TargetWindowProperty);
        set => SetValue(TargetWindowProperty, value);
    }

    /// <summary>
    /// Gets or sets the pinning service used when the target window is not an <see cref="UrsaWindow"/>.
    /// </summary>
    public IWindowStackingService? PinningService
    {
        get => GetValue(PinningServiceProperty);
        set => SetValue(PinningServiceProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TargetWindow is null)
        {
            AttachToWindow(ResolveWindowFromTree());
        }
        else
        {
            AttachToWindow(TargetWindow);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachFromWindow();
    }

    private void OnTargetWindowChanged(AvaloniaPropertyChangedEventArgs<Window?> args)
    {
        var window = args.NewValue.GetValueOrDefault();
        if (VisualRoot is null)
        {
            _attachedWindow = window;
            return;
        }

        if (window is null)
        {
            AttachToWindow(ResolveWindowFromTree());
        }
        else
        {
            AttachToWindow(window);
        }
    }

    private void OnIsCheckedPropertyChanged(AvaloniaPropertyChangedEventArgs<bool?> args)
    {
        if (!args.IsEffectiveValueChange())
        {
            return;
        }

        bool targetState = args.NewValue.GetValueOrDefault() ?? false;
        if (_isInternalUpdate)
        {
            _lastKnownPinned = targetState;
            return;
        }

        _ = ApplyPinStateAsync(targetState);
    }

    private async Task ApplyPinStateAsync(bool targetState)
    {
        if (!EnsureWindowAttachment())
        {
            RevertToTrackedState();
            return;
        }

        if (!_isPinningSupported)
        {
            RevertToTrackedState();
            return;
        }

        _isToggleInFlight = true;
        UpdateEnabledState();

        var window = _attachedUrsaWindow ?? _attachedWindow;
        if (window is null)
        {
            _isToggleInFlight = false;
            UpdateEnabledState();
            RevertToTrackedState();
            return;
        }

        var result = await WindowPinController.SetPinStateAsync(window, targetState, PinningService);

        _isToggleInFlight = false;
        UpdateEnabledState();

        if (result.IsSuccess)
        {
            _lastKnownPinned = targetState;
        }
        else
        {
            WindowPinController.LogFailure(this, result);
            RevertToTrackedState();
        }
    }

    private bool EnsureWindowAttachment()
    {
        if (_attachedWindow is not null)
        {
            return true;
        }

        var fallback = TargetWindow ?? ResolveWindowFromTree();
        if (fallback is null)
        {
            UpdatePinningAvailability(false);
            return false;
        }

        AttachToWindow(fallback);
        return _attachedWindow is not null;
    }

    private void AttachToWindow(Window? window)
    {
        if (ReferenceEquals(_attachedWindow, window))
        {
            return;
        }

        DetachFromWindow();

        _attachedWindow = window;
        if (window is null)
        {
            UpdatePinningAvailability(false);
            return;
        }

        if (window is UrsaWindow ursaWindow)
        {
            _attachedUrsaWindow = ursaWindow;
            _attachedUrsaWindow.PropertyChanged += OnUrsaWindowPropertyChanged;
            UpdateFromUrsaWindow();
            return;
        }

        if (!WindowPinController.TryGetCurrentState(window, out var currentState))
        {
            currentState = false;
        }

        _lastKnownPinned = currentState;
        _isInternalUpdate = true;
        SetCurrentValue(IsCheckedProperty, currentState);
        _isInternalUpdate = false;
        UpdatePinningAvailability(WindowPinController.CanPin(window));
    }

    private void DetachFromWindow()
    {
        if (_attachedUrsaWindow is not null)
        {
            _attachedUrsaWindow.PropertyChanged -= OnUrsaWindowPropertyChanged;
        }

        _attachedUrsaWindow = null;
        _attachedWindow = null;
        _isPinningSupported = false;
        UpdateEnabledState();
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

        _isInternalUpdate = true;
        SetCurrentValue(IsCheckedProperty, _attachedUrsaWindow.IsPinnedToDesktopBottom);
        _isInternalUpdate = false;
        _lastKnownPinned = _attachedUrsaWindow.IsPinnedToDesktopBottom;
        UpdatePinningAvailability(_attachedUrsaWindow.CanPinToDesktopBottom);
    }

    private void UpdatePinningAvailability(bool isSupported)
    {
        _isPinningSupported = isSupported;
        UpdateEnabledState();
    }

    private void UpdateEnabledState()
    {
        SetCurrentValue(IsEnabledProperty, _isPinningSupported && !_isToggleInFlight);
    }

    private void RevertToTrackedState()
    {
        _isInternalUpdate = true;
        SetCurrentValue(IsCheckedProperty, _lastKnownPinned);
        _isInternalUpdate = false;
    }

    private Window? ResolveWindowFromTree()
    {
        return this.FindAncestorOfType<UrsaWindow>()
            ?? (this.GetVisualRoot() as Window)
            ?? this.FindAncestorOfType<Window>();
    }
}

internal static class WindowPinToggleButtonExtensions
{
    public static bool IsEffectiveValueChange<T>(this AvaloniaPropertyChangedEventArgs<T> args)
    {
        return !EqualityComparer<T>.Default.Equals(args.NewValue.Value, args.OldValue.Value);
    }
}
