namespace Simryx.App.Models;

public sealed class DemoDevice
{
    public string Name { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Connection { get; set; } = "USB";
    public string Badge { get; set; } = string.Empty;
    public string Glyph { get; set; } = "\uE975";
}