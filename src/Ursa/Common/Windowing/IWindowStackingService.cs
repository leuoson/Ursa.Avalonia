using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace Ursa.Common.Windowing;

public interface IWindowStackingService
{
    Task<WindowStackingResult> PinBottomAsync(Window window, CancellationToken cancellationToken = default);

    Task<WindowStackingResult> ReleaseAsync(Window window, CancellationToken cancellationToken = default);
}
