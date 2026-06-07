using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Simryx.App.Services;
using Windows.Storage;
using Windows.System;

namespace Simryx.App.Views;

public sealed partial class DashboardPage : Page
{
    private readonly IThemeSelectorService _theme;
    private readonly ILocalizationService _localization;
    private readonly ProfileService _profiles = new();
    private bool _loaded;

    public DashboardPage()
    {
        InitializeComponent();
        _theme = App.Services.GetRequiredService<IThemeSelectorService>();
        _localization = App.Services.GetRequiredService<ILocalizationService>();
        Loading += OnLoading;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoading(FrameworkElement sender, object args)
    {
        // Прячем карточки ДО первого кадра — чтобы не было видно «прогрузки» фонов.
        if (MotionService.Reduced) return;
        EntranceAnimations.HideAll(Tile1, Tile2, Tile3, Tile4, ActionsPanel);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loaded = true;

        // Идемпотентная подписка.
        ProfileService.ActiveChanged -= OnActiveProfileChanged;
        ProfileService.ActiveChanged += OnActiveProfileChanged;

        PopulateStatus();

        if (MotionService.Reduced) return;

        // Премиальное каскадное появление.
        EntranceAnimations.PlayStaggered(40, 100, Tile1, Tile2, Tile3, Tile4, ActionsPanel);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        ProfileService.ActiveChanged -= OnActiveProfileChanged;
    }

    private void OnActiveProfileChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_loaded) return;
            try { PopulateStatus(); }
            catch { /* страница уже выгружена */ }
        });
    }

    private void PopulateStatus()
    {
        bool en = (_localization.CurrentLanguage ?? string.Empty)
            .StartsWith("en", StringComparison.OrdinalIgnoreCase);

        StatusDevicesValue.Text = en ? "5 (demo)" : "5 (демо)";
        StatusGameValue.Text = en ? "Not running" : "Не запущена";

        var active = _profiles.GetActive();
        StatusProfileValue.Text = active is not null
            ? active.Name
            : (en ? "Not selected" : "Не выбран");

        StatusUpdatesValue.Text = en ? "Up to date (demo)" : "Актуально (демо)";
    }

    private void Profiles_Click(object sender, RoutedEventArgs e) => NavigateTo("Profiles");
    private void Updates_Click(object sender, RoutedEventArgs e) => UpdateInfoBar.IsOpen = true;

    private async void Logs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Simryx", "Simryx Hub", "logs");
            Directory.CreateDirectory(dir);
            var folder = await StorageFolder.GetFolderFromPathAsync(dir);
            await Launcher.LaunchFolderAsync(folder);
        }
        catch
        {
            // Папка логов появится после первого запуска с логированием
        }
    }

    private void NavigateTo(string tag)
    {
        var nav = FindAncestor<NavigationView>(this);
        if (nav is null) return;
        var item = nav.MenuItems.Concat(nav.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(i => (i.Tag as string) == tag);
        if (item != null) nav.SelectedItem = item;
    }

    private static T? FindAncestor<T>(DependencyObject start) where T : DependencyObject
    {
        var current = start;
        while (current != null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}