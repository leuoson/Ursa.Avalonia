using System;
using Avalonia;
using Avalonia.Controls;
using Ursa.Controls;
using Ursa.Demo.ViewModels;

namespace Ursa.Demo.Pages;

public partial class WindowPinDemo : UserControl
{
    public WindowPinDemo()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachToWindow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (DataContext is WindowPinDemoViewModel vm)
        {
            vm.DetachWindow();
        }
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        AttachToWindow();
    }

    private void AttachToWindow()
    {
        if (DataContext is not WindowPinDemoViewModel vm)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is UrsaWindow window)
        {
            vm.AttachWindow(window);
        }
        else
        {
            vm.DetachWindow();
        }
    }
}
