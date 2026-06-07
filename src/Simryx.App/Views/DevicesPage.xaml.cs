using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Simryx.App.Models;

namespace Simryx.App.Views;

public sealed partial class DevicesPage : Page
{
    private readonly ResourceLoader _res = new();

    public ObservableCollection<DemoDevice> Devices { get; } = new();

    public DevicesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Devices.Count > 0) return;

        var demo = _res.GetString("DemoBadgeText");

        Devices.Add(new DemoDevice
        {
            Name = "Simryx Force DD",
            TypeLabel = _res.GetString("DeviceTypeBase"),
            Glyph = "\uE975",
            Badge = demo,
        });
        Devices.Add(new DemoDevice
        {
            Name = "Simryx GT Wheel",
            TypeLabel = _res.GetString("DeviceTypeWheel"),
            Glyph = "\uE7FC",
            Badge = demo,
        });
        Devices.Add(new DemoDevice
        {
            Name = "Simryx Pro Pedals",
            TypeLabel = _res.GetString("DeviceTypePedals"),
            Glyph = "\uE9D9",
            Badge = demo,
        });
        Devices.Add(new DemoDevice
        {
            Name = "Simryx Shifter",
            TypeLabel = _res.GetString("DeviceTypeShifter"),
            Glyph = "\uE713",
            Badge = demo,
        });
        Devices.Add(new DemoDevice
        {
            Name = "Simryx Handbrake",
            TypeLabel = _res.GetString("DeviceTypeHandbrake"),
            Glyph = "\uE77B",
            Badge = demo,
        });
    }
}