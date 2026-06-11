using LiteTubeDock.Constants;

namespace LiteTubeDock.Models;

public sealed class WindowSettings
{
    public double Left { get; set; } = AppConstants.DefaultWindowLeft;

    public double Top { get; set; } = AppConstants.DefaultWindowTop;

    public double Width { get; set; } = AppConstants.DefaultWindowWidth;

    public double Height { get; set; } = AppConstants.DefaultWindowHeight;
}
