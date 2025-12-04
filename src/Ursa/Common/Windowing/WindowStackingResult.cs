namespace Ursa.Common.Windowing;

/// <summary>
/// Represents the outcome of attempting to change the z-order of a window.
/// </summary>
public sealed class WindowStackingResult
{
    private WindowStackingResult(bool isSuccess, string? message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public string? Message { get; }

    public static WindowStackingResult Success() => new(true, null);

    public static WindowStackingResult Failure(string message) => new(false, message);

    public override string ToString() => IsSuccess ? "Success" : $"Failure: {Message}";
}
