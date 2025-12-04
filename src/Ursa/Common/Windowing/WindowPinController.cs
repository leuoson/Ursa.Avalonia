using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Logging;
using Ursa.Controls;

namespace Ursa.Common.Windowing;

/// <summary>
/// Shared helpers for pinning windows across Ursa components.
/// </summary>
public static class WindowPinController
{
    internal const string DescriptorWindows = "HWND";
    internal const string DescriptorMacOs = "NSWindow";
    internal const string DescriptorX11 = "XID";

    public static bool CanPin(Window? window)
    {
        if (window is null)
        {
            return false;
        }

        if (window is UrsaWindow ursaWindow)
        {
            return ursaWindow.CanPinToDesktopBottom;
        }

        var descriptor = window.TryGetPlatformHandle()?.HandleDescriptor;
        return string.Equals(descriptor, DescriptorWindows, StringComparison.Ordinal) ||
               string.Equals(descriptor, DescriptorMacOs, StringComparison.Ordinal) ||
               string.Equals(descriptor, DescriptorX11, StringComparison.Ordinal);
    }

    public static bool TryGetCurrentState(Window window, out bool isPinned)
    {
        if (window is UrsaWindow ursaWindow)
        {
            isPinned = ursaWindow.IsPinnedToDesktopBottom;
            return true;
        }

        isPinned = false;
        return false;
    }

    public static async Task<WindowStackingResult> SetPinStateAsync(Window window, bool targetState, IWindowStackingService? service = null)
    {
        if (window is UrsaWindow ursaWindow)
        {
            if (!ursaWindow.CanPinToDesktopBottom)
            {
                return WindowStackingResult.Failure("Pinning is not supported for this window.");
            }

            var resolvedService = ursaWindow.PinningService ?? service ?? WindowStackingService.Instance;
            var result = targetState
                ? await resolvedService.PinBottomAsync(ursaWindow)
                : await resolvedService.ReleaseAsync(ursaWindow);

            if (result.IsSuccess)
            {
                ursaWindow.SetCurrentValue(UrsaWindow.IsPinnedToDesktopBottomProperty, targetState);
            }

            return result;
        }

        var fallbackService = service ?? WindowStackingService.Instance;
        return targetState
            ? await fallbackService.PinBottomAsync(window)
            : await fallbackService.ReleaseAsync(window);
    }

    internal static void LogFailure(object source, WindowStackingResult result)
    {
        Logger.TryGet(LogEventLevel.Warning, nameof(WindowPinController))?
            .Log(source, result.Message ?? "Failed to change pin state.");
    }
}
