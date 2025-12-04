using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Logging;
using System.Windows.Input;
using Ursa.Common.Windowing;

namespace Ursa.Controls;

/// <summary>
///     Ursa Window is an advanced Window control that provides a lot of features and customization options.
/// </summary>
public class UrsaWindow : Window
{
    /// <summary>
    /// The name of the dialog host part in the control template.
    /// </summary>
    public const string PART_DialogHost = "PART_DialogHost";

    /// <summary>
    /// Defines the visibility of the full-screen button.
    /// </summary>
    public static readonly StyledProperty<bool> IsFullScreenButtonVisibleProperty =
        AvaloniaProperty.Register<UrsaWindow, bool>(
            nameof(IsFullScreenButtonVisible));

    public static readonly StyledProperty<bool> IsPinButtonVisibleProperty =
        AvaloniaProperty.Register<UrsaWindow, bool>(
            nameof(IsPinButtonVisible), true);

    public static readonly StyledProperty<bool> IsPinnedToDesktopBottomProperty =
        AvaloniaProperty.Register<UrsaWindow, bool>(
            nameof(IsPinnedToDesktopBottom));

    public static readonly DirectProperty<UrsaWindow, bool> CanPinToDesktopBottomProperty =
        AvaloniaProperty.RegisterDirect<UrsaWindow, bool>(
            nameof(CanPinToDesktopBottom), o => o._canPinToDesktopBottom);

    /// <summary>
    /// Defines the visibility of the minimize button.
    /// </summary>
    [Obsolete("Will be removed in Ursa 2.0. Use Window.CanMinimize property instead.")]
    public static readonly StyledProperty<bool> IsMinimizeButtonVisibleProperty =
        AvaloniaProperty.Register<UrsaWindow, bool>(
            nameof(IsMinimizeButtonVisible), true);

    /// <summary>
    /// Defines the visibility of the restore button.
    /// </summary>
    [Obsolete("Will be removed in Ursa 2.0. Use Window.CanMaximize property instead.")]
    public static readonly StyledProperty<bool> IsRestoreButtonVisibleProperty =
        AvaloniaProperty.Register<UrsaWindow, bool>(
            nameof(IsRestoreButtonVisible), true);

    /// <summary>
    /// Defines the visibility of the close button.
    /// </summary>
    public static readonly StyledProperty<bool> IsCloseButtonVisibleProperty =
        AvaloniaProperty.Register<UrsaWindow, bool>(
            nameof(IsCloseButtonVisible), true);

    /// <summary>
    /// Defines the visibility of the title bar.
    /// </summary>
    public static readonly StyledProperty<bool> IsTitleBarVisibleProperty = AvaloniaProperty.Register<UrsaWindow, bool>(
        nameof(IsTitleBarVisible), true);

    /// <summary>
    /// Defines the visibility of the managed resizer.
    /// </summary>
    public static readonly StyledProperty<bool> IsManagedResizerVisibleProperty =
        AvaloniaProperty.Register<UrsaWindow, bool>(
            nameof(IsManagedResizerVisible));

    /// <summary>
    /// Defines the content of the title bar.
    /// </summary>
    public static readonly StyledProperty<object?> TitleBarContentProperty =
        AvaloniaProperty.Register<UrsaWindow, object?>(
            nameof(TitleBarContent));

    /// <summary>
    /// Defines the content on the left side of the window.
    /// </summary>
    public static readonly StyledProperty<object?> LeftContentProperty = AvaloniaProperty.Register<UrsaWindow, object?>(
        nameof(LeftContent));

    /// <summary>
    /// Defines the content on the right side of the window.
    /// </summary>
    public static readonly StyledProperty<object?> RightContentProperty =
        AvaloniaProperty.Register<UrsaWindow, object?>(
            nameof(RightContent));

    /// <summary>
    /// Defines the margin of the title bar.
    /// </summary>
    public static readonly StyledProperty<Thickness> TitleBarMarginProperty =
        AvaloniaProperty.Register<UrsaWindow, Thickness>(
            nameof(TitleBarMargin));

    private bool _canClose;
    private bool _canPinToDesktopBottom;
    private IWindowStackingService _pinningService = WindowStackingService.Instance;
    private readonly UrsaWindowTogglePinCommand _togglePinCommand;
    
    /// <summary>
    /// Gets the style key override for the control.
    /// </summary>
    protected override Type StyleKeyOverride => typeof(UrsaWindow);

    public UrsaWindow()
    {
        _togglePinCommand = new UrsaWindowTogglePinCommand(this);
        Opened += OnOpenedHandler;
    }

internal static class WindowStackingServiceDescriptor
{
    public const string Windows = "HWND";
    public const string MacOs = "NSWindow";
    public const string X11 = "XID";
}

internal sealed class UrsaWindowTogglePinCommand : ICommand
{
    private readonly UrsaWindow _window;

    public UrsaWindowTogglePinCommand(UrsaWindow window)
    {
        _window = window;
    }

    public bool CanExecute(object? parameter) => _window.CanPinToDesktopBottom;

    public async void Execute(object? parameter)
    {
        await _window.TogglePinAsync();
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, System.EventArgs.Empty);
}

    /// <summary>
    /// Gets or sets a value indicating whether the full-screen button is visible.
    /// </summary>
    public bool IsFullScreenButtonVisible
    {
        get => GetValue(IsFullScreenButtonVisibleProperty);
        set => SetValue(IsFullScreenButtonVisibleProperty, value);
    }

    public bool IsPinButtonVisible
    {
        get => GetValue(IsPinButtonVisibleProperty);
        set => SetValue(IsPinButtonVisibleProperty, value);
    }

    public bool IsPinnedToDesktopBottom
    {
        get => GetValue(IsPinnedToDesktopBottomProperty);
        private set => SetValue(IsPinnedToDesktopBottomProperty, value);
    }

    public bool CanPinToDesktopBottom => _canPinToDesktopBottom;

    public IWindowStackingService PinningService
    {
        get => _pinningService;
        set
        {
            var service = value ?? WindowStackingService.Instance;
            if (!ReferenceEquals(_pinningService, service))
            {
                _pinningService = service;
                _togglePinCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand TogglePinCommand => _togglePinCommand;

    /// <summary>
    /// Gets or sets a value indicating whether the minimize button is visible.
    /// </summary>
    [Obsolete("Will be removed in Ursa 2.0. Use Window.CanMinimize property instead.")]
    public bool IsMinimizeButtonVisible
    {
        get => GetValue(IsMinimizeButtonVisibleProperty);
        set => SetValue(IsMinimizeButtonVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the restore button is visible.
    /// </summary>
    [Obsolete("Will be removed in Ursa 2.0. Use Window.CanMaximize property instead.")]
    public bool IsRestoreButtonVisible
    {
        get => GetValue(IsRestoreButtonVisibleProperty);
        set => SetValue(IsRestoreButtonVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the close button is visible.
    /// </summary>
    public bool IsCloseButtonVisible
    {
        get => GetValue(IsCloseButtonVisibleProperty);
        set => SetValue(IsCloseButtonVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the title bar is visible.
    /// </summary>
    public bool IsTitleBarVisible
    {
        get => GetValue(IsTitleBarVisibleProperty);
        set => SetValue(IsTitleBarVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the managed resizer is visible.
    /// </summary>
    public bool IsManagedResizerVisible
    {
        get => GetValue(IsManagedResizerVisibleProperty);
        set => SetValue(IsManagedResizerVisibleProperty, value);
    }

    /// <summary>
    /// Gets or sets the content of the title bar.
    /// </summary>
    public object? TitleBarContent
    {
        get => GetValue(TitleBarContentProperty);
        set => SetValue(TitleBarContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the content on the left side of the window.
    /// </summary>
    public object? LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the content on the right side of the window.
    /// </summary>
    public object? RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }

    /// <summary>
    /// Gets or sets the margin of the title bar.
    /// </summary>
    public Thickness TitleBarMargin
    {
        get => GetValue(TitleBarMarginProperty);
        set => SetValue(TitleBarMarginProperty, value);
    }
    
    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        var host = e.NameScope.Find<OverlayDialogHost>(PART_DialogHost);
        if (host is not null) LogicalChildren.Add(host);
    }

    private void OnOpenedHandler(object? sender, System.EventArgs e)
    {
        UpdatePinCapability();
    }

    protected virtual void UpdatePinCapability()
    {
        var descriptor = TryGetPlatformHandle()?.HandleDescriptor;
        bool canPin = DeterminePinCapability(descriptor);
        if (SetAndRaise(CanPinToDesktopBottomProperty, ref _canPinToDesktopBottom, canPin))
        {
            _togglePinCommand.RaiseCanExecuteChanged();
        }
    }

    protected virtual bool DeterminePinCapability(string? handleDescriptor)
    {
        return handleDescriptor is WindowStackingServiceDescriptor.Windows
            or WindowStackingServiceDescriptor.MacOs
            or WindowStackingServiceDescriptor.X11;
    }

    private async Task TogglePinAsync()
    {
        if (!CanPinToDesktopBottom)
        {
            return;
        }

        var targetState = !IsPinnedToDesktopBottom;
        var service = PinningService ?? WindowStackingService.Instance;
        WindowStackingResult result = targetState
            ? await service.PinBottomAsync(this)
            : await service.ReleaseAsync(this);

        if (result.IsSuccess)
        {
            SetCurrentValue(IsPinnedToDesktopBottomProperty, targetState);
        }
        else
        {
            Logger.TryGet(LogEventLevel.Warning, nameof(UrsaWindow))?
                .Log(this, result.Message ?? "Failed to toggle pin state.");
        }
    }

    /// <summary>
    /// Determines whether the window can close.
    /// </summary>
    /// <returns>A task that resolves to true if the window can close; otherwise, false.</returns>
    protected virtual async Task<bool> CanClose()
    {
        return await Task.FromResult(true);
    }

    /// <summary>
    /// Handles the window closing event and determines whether the window should close.
    /// </summary>
    /// <param name="e">The event arguments for the closing event.</param>
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        VerifyAccess();
        if (!_canClose)
        {
            e.Cancel = true;
            _canClose = await CanClose();
            if (_canClose)
            {
                Close();
                return;
            }
        }
        base.OnClosing(e);
    }
}