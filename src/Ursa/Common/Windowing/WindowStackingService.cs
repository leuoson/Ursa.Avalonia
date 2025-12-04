using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Platform;

namespace Ursa.Common.Windowing;

/// <summary>
/// Provides platform-specific logic for keeping a window at the lowest z-order.
/// </summary>
public class WindowStackingService : IWindowStackingService
{
    public static IWindowStackingService Instance { get; } = new WindowStackingService();

    public Task<WindowStackingResult> PinBottomAsync(Window window, CancellationToken cancellationToken = default)
    {
        if (window is null)
        {
            throw new ArgumentNullException(nameof(window));
        }
        var handle = window.TryGetPlatformHandle();
        if (handle is null)
        {
            return Task.FromResult(WindowStackingResult.Failure("Window handle is not available."));
        }

        return Task.FromResult(HandlePlatform(Operation.Pin, handle));
    }

    public Task<WindowStackingResult> ReleaseAsync(Window window, CancellationToken cancellationToken = default)
    {
        if (window is null)
        {
            throw new ArgumentNullException(nameof(window));
        }
        var handle = window.TryGetPlatformHandle();
        if (handle is null)
        {
            return Task.FromResult(WindowStackingResult.Failure("Window handle is not available."));
        }

        return Task.FromResult(HandlePlatform(Operation.Release, handle));
    }

    private static WindowStackingResult HandlePlatform(Operation operation, IPlatformHandle handle)
    {
        var descriptor = handle.HandleDescriptor ?? string.Empty;
        if (string.Equals(descriptor, Win32Interop.Descriptor, StringComparison.Ordinal))
        {
            return operation == Operation.Pin
                ? Win32Interop.PinBottom(handle.Handle)
                : Win32Interop.Release(handle.Handle);
        }

        if (string.Equals(descriptor, MacInterop.Descriptor, StringComparison.Ordinal))
        {
            return MacInterop.TryHandle(operation, handle.Handle);
        }

        if (string.Equals(descriptor, X11Interop.Descriptor, StringComparison.Ordinal))
        {
            return X11Interop.TryHandle(operation, handle.Handle);
        }

        return WindowStackingResult.Failure($"Handle descriptor '{descriptor}' is not supported.");
    }

    private enum Operation
    {
        Pin,
        Release
    }

    private static class Win32Interop
    {
        public const string Descriptor = "HWND";

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private static readonly IntPtr HWND_TOP = new(0);
        private static readonly IntPtr HWND_BOTTOM = new(1);
        private static readonly IntPtr HWND_NOTOPMOST = new(-2);

        public static WindowStackingResult PinBottom(IntPtr handle)
        {
            if (!SetWindowPos(handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW))
            {
                return WindowStackingResult.Failure($"Win32 SetWindowPos failed with error {Marshal.GetLastWin32Error()}.");
            }

            return WindowStackingResult.Success();
        }

        public static WindowStackingResult Release(IntPtr handle)
        {
            if (!SetWindowPos(handle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW))
            {
                return WindowStackingResult.Failure($"Win32 SetWindowPos failed with error {Marshal.GetLastWin32Error()}.");
            }

            if (!SetWindowPos(handle, HWND_TOP, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW))
            {
                return WindowStackingResult.Failure($"Win32 SetWindowPos failed with error {Marshal.GetLastWin32Error()}.");
            }

            return WindowStackingResult.Success();
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    }

    private static class MacInterop
    {
        public const string Descriptor = "NSWindow";

        private const string AppKit = "/System/Library/Frameworks/AppKit.framework/AppKit";
        private const string ObjCLib = "/usr/lib/libobjc.dylib";

        private static readonly IntPtr NSWindowLevelSelector = SelRegisterName("setLevel:");
        private static readonly IntPtr NSWindowCollectionBehaviorSelector = SelRegisterName("setCollectionBehavior:");
        private static readonly IntPtr NSWindowMiniaturizeSelector = SelRegisterName("miniaturize:");
        private static readonly IntPtr NSWindowDeminiaturizeSelector = SelRegisterName("deminiaturize:");
        private static readonly IntPtr NSWindowOrderBackSelector = SelRegisterName("orderBack:");
        private static readonly IntPtr NSWindowOrderFrontSelector = SelRegisterName("orderFront:");

        private const int NSWindowLevelNormal = 0;
        private const uint NSWindowCollectionBehaviorCanJoinAllSpaces = 1u << 0;
        private const uint NSWindowCollectionBehaviorStationary = 1u << 4;

        public static WindowStackingResult TryHandle(Operation operation, IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return WindowStackingResult.Failure("NSWindow handle is zero.");
            }

            var result = operation == Operation.Pin
                ? Pin(handle)
                : Release(handle);

            if (result.IsFailure)
            {
                Logger.TryGet(LogEventLevel.Warning, nameof(WindowStackingService))?.Log(nameof(MacInterop), result.Message!);
            }

            return result;
        }

        private static WindowStackingResult Pin(IntPtr nsWindow)
        {
            // Keep the window in the normal layer so it stays interactive, but push it
            // behind other app windows via orderBack so it behaves like "always on bottom".
            objc_msgSend(nsWindow, NSWindowLevelSelector, NSWindowLevelNormal);
            var behavior = NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorStationary;
            objc_msgSend(nsWindow, NSWindowCollectionBehaviorSelector, new IntPtr((long)behavior));
            objc_msgSend(nsWindow, NSWindowOrderBackSelector, nsWindow);
            objc_msgSend(nsWindow, NSWindowDeminiaturizeSelector, nsWindow);
            return WindowStackingResult.Success();
        }

        private static WindowStackingResult Release(IntPtr nsWindow)
        {
            objc_msgSend(nsWindow, NSWindowLevelSelector, NSWindowLevelNormal);
            objc_msgSend(nsWindow, NSWindowCollectionBehaviorSelector, IntPtr.Zero);
            objc_msgSend(nsWindow, NSWindowOrderFrontSelector, nsWindow);
            return WindowStackingResult.Success();
        }

        [DllImport(ObjCLib)]
        private static extern IntPtr sel_registerName(string selectorName);

        [DllImport(AppKit)]
        private static extern void objc_msgSend(IntPtr receiver, IntPtr selector, int arg1);

        [DllImport(AppKit)]
        private static extern void objc_msgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

        private static IntPtr SelRegisterName(string name) => sel_registerName(name);
    }

    private static class X11Interop
    {
        public const string Descriptor = "XID";

        public static WindowStackingResult TryHandle(Operation operation, IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return WindowStackingResult.Failure("X11 window handle is zero.");
            }

            return operation == Operation.Pin
                ? ChangeBelowState(handle, enable: true)
                : ChangeBelowState(handle, enable: false);
        }

        private static WindowStackingResult ChangeBelowState(IntPtr window, bool enable)
        {
            var display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero)
            {
                return WindowStackingResult.Failure("Failed to open X11 display.");
            }

            try
            {
                var root = XDefaultRootWindow(display);
                if (root == IntPtr.Zero)
                {
                    return WindowStackingResult.Failure("Failed to resolve X11 root window.");
                }

                var stateAtom = XInternAtom(display, "_NET_WM_STATE", false);
                var belowAtom = XInternAtom(display, "_NET_WM_STATE_BELOW", false);
                if (stateAtom == IntPtr.Zero || belowAtom == IntPtr.Zero)
                {
                    return WindowStackingResult.Failure("Required X11 atoms are unavailable.");
                }

                var clientEvent = new XClientMessageEvent
                {
                    type = ClientMessage,
                    send_event = 1,
                    display = display,
                    window = window,
                    message_type = stateAtom,
                    format = 32,
                    ptr1 = new IntPtr(enable ? _NET_WM_STATE_ADD : _NET_WM_STATE_REMOVE),
                    ptr2 = belowAtom
                };

                var mask = new IntPtr(SubstructureRedirectMask | SubstructureNotifyMask);
                int status = XSendEvent(display, root, false, mask, ref clientEvent);
                if (status == 0)
                {
                    return WindowStackingResult.Failure("XSendEvent rejected _NET_WM_STATE request.");
                }

                XFlush(display);
                return WindowStackingResult.Success();
            }
            finally
            {
                XCloseDisplay(display);
            }
        }

        private const string LibX11 = "libX11.so.6";
        private const int ClientMessage = 33;
        private const long SubstructureRedirectMask = 0x00004000;
        private const long SubstructureNotifyMask = 0x00002000;
        private const int _NET_WM_STATE_ADD = 1;
        private const int _NET_WM_STATE_REMOVE = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct XClientMessageEvent
        {
            public int type;
            public IntPtr serial;
            public int send_event;
            public IntPtr display;
            public IntPtr window;
            public IntPtr message_type;
            public int format;
            public IntPtr ptr1;
            public IntPtr ptr2;
            public IntPtr ptr3;
            public IntPtr ptr4;
            public IntPtr ptr5;
        }

        [DllImport(LibX11)]
        private static extern IntPtr XOpenDisplay(IntPtr display);

        [DllImport(LibX11)]
        private static extern int XCloseDisplay(IntPtr display);

        [DllImport(LibX11)]
        private static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport(LibX11)]
        private static extern IntPtr XInternAtom(IntPtr display, string atom_name, bool only_if_exists);

        [DllImport(LibX11)]
        private static extern int XSendEvent(IntPtr display, IntPtr w, bool propagate, IntPtr event_mask, ref XClientMessageEvent event_send);

        [DllImport(LibX11)]
        private static extern int XFlush(IntPtr display);
    }
}
