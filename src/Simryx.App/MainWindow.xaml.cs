using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Simryx.App.Services;
using Simryx.App.Views;
using Windows.Graphics;

namespace Simryx.App;

public sealed partial class MainWindow : Window
{
    private bool _splashStarted;

    private static readonly Vector3[] PartOffsets =
    {
        new Vector3(-46f, -26f, 0f),
        new Vector3(0f, 48f, 0f),
        new Vector3(46f, -26f, 0f),
    };

    public MainWindow()
    {
        InitializeComponent();
        SystemBackdrop = new MicaBackdrop();
        Title = "Simryx Hub";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SetWindowIcon();
        AppWindow.Resize(new SizeInt32(1100, 720));
        PrepareSplashInitialState();
        SplashContent.Loaded += OnSplashContentLoaded;
    }

    // Иконка окна (панель задач + превью миниатюры). В отличие от иконки exe
    // (<ApplicationIcon>), она задаётся в рантайме и читается с диска, поэтому
    // Simryx.ico ОБЯЗАТЕЛЬНО должен быть добавлен в .csproj как Content
    // (чтобы попадал в папку вывода рядом с exe). Используем абсолютный путь —
    // надёжнее при запуске из автозагрузки/обновлятора.
    private void SetWindowIcon()
    {
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Simryx.ico");
        if (System.IO.File.Exists(iconPath))
            AppWindow.SetIcon(iconPath);
    }

    // Показываем окно приветствия один раз, ПОСЛЕ сплэша — когда XamlRoot уже доступен
    private async Task ShowOnboardingIfNeededAsync()
    {
        if (OnboardingService.IsDone) return;

        var welcome = new WelcomeDialog { XamlRoot = Content.XamlRoot };
        await welcome.ShowAsync();

        // Если язык был изменён — перезапускаем, чтобы он применился ко всему интерфейсу (включая левое меню)
        if (welcome.RestartForLanguage)
            Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
    }

    private void OnSplashContentLoaded(object sender, RoutedEventArgs e)
    {
        if (_splashStarted) return;
        _splashStarted = true;
        _ = RunSplashAsync();
    }

    private void PrepareSplashInitialState()
    {
        var parts = new[] { LogoPart1, LogoPart2, LogoPart3 };
        for (var i = 0; i < parts.Length; i++)
        {
            var v = ElementCompositionPreview.GetElementVisual(parts[i]);
            v.CenterPoint = new Vector3(52f, 52f, 0f);
            v.Opacity = 0f;
            v.Offset = PartOffsets[i];
            v.Scale = new Vector3(0.85f, 0.85f, 1f);
        }

        var container = ElementCompositionPreview.GetElementVisual(LogoContainer);
        container.Offset = new Vector3(0f, 22f, 0f);

        var ring = ElementCompositionPreview.GetElementVisual(SplashRing);
        ring.Opacity = 0f;
    }

    private async Task RunSplashAsync()
    {
        // Режим уменьшенных анимаций: без splash-анимаций, сразу показываем интерфейс
        if (MotionService.Reduced)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
            await App.InitializeAsync();
            SplashOverlay.Visibility = Visibility.Collapsed;
            await ShowOnboardingIfNeededAsync();
            return;
        }

        await Task.WhenAll(
            AnimateLogoAsync(assemble: true),
            FadeAsync(SplashRing, 0f, 1f, 1190));

        await Task.WhenAll(App.InitializeAsync(), Task.Delay(1700));

        await Task.WhenAll(
            FadeAsync(SplashRing, 1f, 0f, 600),
            FadeAsync(LogoContainer, 1f, 0f, 600));

        NavView.SelectedItem = NavView.MenuItems[0];

        await Task.WhenAll(
            FadeAsync(SplashOverlay, 1f, 0f, 650),
            RevealAsync(AppTitleBar, delayMs: 0,   durationMs: 520, fromOffset: new Vector3(0f, -6f, 0f), fromScale: 1f),
            RevealAsync(NavView,     delayMs: 140, durationMs: 660, fromOffset: new Vector3(0f, 20f, 0f), fromScale: 0.985f));

        SplashOverlay.Visibility = Visibility.Collapsed;
        await ShowOnboardingIfNeededAsync();
    }

    private static Task RevealAsync(UIElement element, double delayMs, double durationMs,
        Vector3 fromOffset, float fromScale)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);

        // Уменьшенные анимации: сразу финальное состояние
        if (MotionService.Reduced)
        {
            visual.Opacity = 1f;
            visual.Offset = Vector3.Zero;
            visual.Scale = Vector3.One;
            return Task.CompletedTask;
        }

        var compositor = visual.Compositor;
        if (element is FrameworkElement fe)
            visual.CenterPoint = new Vector3((float)fe.ActualWidth / 2f, (float)fe.ActualHeight / 2f, 0f);

        visual.Opacity = 0f;
        visual.Offset = fromOffset;
        visual.Scale = new Vector3(fromScale, fromScale, 1f);

        var ease = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));
        var delay = TimeSpan.FromMilliseconds(delayMs);
        var duration = TimeSpan.FromMilliseconds(durationMs);

        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(1f, 1f, ease);
        opacity.Duration = duration;
        opacity.DelayTime = delay;

        var offset = compositor.CreateVector3KeyFrameAnimation();
        offset.InsertKeyFrame(1f, Vector3.Zero, ease);
        offset.Duration = duration;
        offset.DelayTime = delay;

        var scale = compositor.CreateVector3KeyFrameAnimation();
        scale.InsertKeyFrame(1f, Vector3.One, ease);
        scale.Duration = duration;
        scale.DelayTime = delay;

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("Opacity", opacity);
        visual.StartAnimation("Offset", offset);
        visual.StartAnimation("Scale", scale);
        batch.End();

        var tcs = new TaskCompletionSource();
        batch.Completed += (_, _) => tcs.SetResult();
        return tcs.Task;
    }

    private Task AnimateLogoAsync(bool assemble)
    {
        var parts = new[] { LogoPart1, LogoPart2, LogoPart3 };
        var compositor = ElementCompositionPreview.GetElementVisual(parts[0]).Compositor;
        var ease = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.33f, 1f), new Vector2(0.68f, 1f));
        var duration = TimeSpan.FromMilliseconds(850);
        const int stagger = 170;

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

        for (var i = 0; i < parts.Length; i++)
        {
            var index = assemble ? i : parts.Length - 1 - i;
            var visual = ElementCompositionPreview.GetElementVisual(parts[index]);
            visual.CenterPoint = new Vector3(52f, 52f, 0f);

            var delay = TimeSpan.FromMilliseconds(i * stagger);
            var targetOpacity = assemble ? 1f : 0f;
            var targetOffset = assemble ? Vector3.Zero : PartOffsets[index];
            var targetScale = assemble ? Vector3.One : new Vector3(0.85f, 0.85f, 1f);

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(1f, targetOpacity, ease);
            opacity.Duration = duration;
            opacity.DelayTime = delay;

            var offset = compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(1f, targetOffset, ease);
            offset.Duration = duration;
            offset.DelayTime = delay;

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(1f, targetScale, ease);
            scale.Duration = duration;
            scale.DelayTime = delay;

            visual.StartAnimation("Opacity", opacity);
            visual.StartAnimation("Offset", offset);
            visual.StartAnimation("Scale", scale);
        }

        var totalMs = 850 + (parts.Length - 1) * stagger;
        var container = ElementCompositionPreview.GetElementVisual(LogoContainer);
        var drift = compositor.CreateVector3KeyFrameAnimation();
        drift.InsertKeyFrame(1f, assemble ? Vector3.Zero : new Vector3(0f, 22f, 0f), ease);
        drift.Duration = TimeSpan.FromMilliseconds(totalMs);
        container.StartAnimation("Offset", drift);

        batch.End();

        var tcs = new TaskCompletionSource();
        batch.Completed += (_, _) => tcs.SetResult();
        return tcs.Task;
    }

    private static Task FadeAsync(UIElement element, float from, float to, double durationMs)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;
        visual.Opacity = from;

        var animation = compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1f, to);
        animation.Duration = TimeSpan.FromMilliseconds(durationMs);

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("Opacity", animation);
        batch.End();

        var tcs = new TaskCompletionSource();
        batch.Completed += (_, _) => tcs.SetResult();
        return tcs.Task;
    }

    private void NavView_PaneOpening(NavigationView sender, object args) => AnimateMenuItems();

    private void NavView_PaneClosing(NavigationView sender,
        NavigationViewPaneClosingEventArgs args) => AnimateMenuItems();

    private void AnimateMenuItems()
    {
        if (MotionService.Reduced) return;

        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
        {
            var visual = ElementCompositionPreview.GetElementVisual(item);
            var compositor = visual.Compositor;
            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0f, 0.2f);
            fade.InsertKeyFrame(1f, 1f);
            fade.Duration = TimeSpan.FromMilliseconds(280);
            visual.StartAnimation("Opacity", fade);
        }
    }

    private void NavView_SelectionChanged(NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        NavigationTransitionInfo transition = MotionService.Reduced
            ? new SuppressNavigationTransitionInfo()
            : new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight };

        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage), null, transition);
            return;
        }

        if (args.SelectedItemContainer is NavigationViewItem item &&
            item.Tag is string tag)
        {
            var pageType = tag switch
            {
                "Dashboard" => typeof(DashboardPage),
                "Devices" => typeof(DevicesPage),
                "Telemetry" => typeof(TelemetryPage),
                "Profiles" => typeof(ProfilesPage),
                _ => typeof(DashboardPage),
            };

            ContentFrame.Navigate(pageType, null, transition);
        }
    }
}