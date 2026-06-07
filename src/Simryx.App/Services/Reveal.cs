using Microsoft.UI.Xaml;

namespace Simryx.App.Services;

/// <summary>
/// Плавное «дорогое» появление цельного элемента целиком.
/// Использование в XAML: svc:Reveal.IsEnabled="True"
/// </summary>
public static class Reveal
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(Reveal),
            new PropertyMetadata(false, OnChanged));

    public static bool GetIsEnabled(DependencyObject o) => (bool)o.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject o, bool v) => o.SetValue(IsEnabledProperty, v);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;

        fe.Loading -= OnLoading;
        fe.Loaded -= OnLoaded;

        if (e.NewValue is true)
        {
            fe.Loading += OnLoading;
            fe.Loaded += OnLoaded;
        }
    }

    private static void OnLoading(FrameworkElement sender, object args)
    {
        if (MotionService.Reduced) return;
        EntranceAnimations.Hide(sender); // спрятать ДО первого кадра
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not UIElement el) return;
        if (MotionService.Reduced) return;
        EntranceAnimations.Play(el);
    }
}