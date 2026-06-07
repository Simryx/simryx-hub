using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace Simryx.App.Services;

public static class EntranceAnimations
{
    private const double DurationMs = 520;

    // Входное проявление проигрывается ТОЛЬКО ОДИН РАЗ за сессию приложения.
    // После того как первая страница проявилась, любые последующие переходы
    // показывают контент мгновенно (без fade по прозрачности), поэтому
    // переключение между вкладками больше не вызывает мерцания подложек карточек.
    private static bool _sessionRevealed;

    public static void Hide(UIElement element)
    {
        // Если входная анимация уже отыграла за эту сессию — ничего не прячем,
        // чтобы контент не «проявлялся» заново при возврате на страницу.
        if (_sessionRevealed) return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.Opacity = 0f;
    }

    public static void HideAll(params UIElement[] elements)
    {
        if (_sessionRevealed) return;

        foreach (var element in elements)
            Hide(element);
    }

    public static void Play(UIElement element)
    {
        // Повторные показы — мгновенно, без анимации.
        if (_sessionRevealed)
        {
            ShowInstantly(element);
            return;
        }

        _sessionRevealed = true;
        Animate(element, 0);
    }

    public static void PlayStaggered(int baseDelayMs, int stepMs, params UIElement[] elements)
    {
        if (elements.Length == 0) return;

        if (_sessionRevealed)
        {
            foreach (var element in elements)
                ShowInstantly(element);
            return;
        }

        _sessionRevealed = true;

        var delay = baseDelayMs;
        foreach (var element in elements)
        {
            Animate(element, delay);
            delay += stepMs;
        }
    }

    private static void ShowInstantly(UIElement element)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.StopAnimation("Opacity");
        visual.Opacity = 1f;
    }

    private static void Animate(UIElement element, int delayMs)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.Offset = Vector3.Zero;
        visual.Opacity = 0f;

        var compositor = visual.Compositor;
        var ease = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.16f, 1f), new Vector2(0.3f, 1f));

        var fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1f, 1f, ease);
        fade.Duration = TimeSpan.FromMilliseconds(DurationMs);
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        if (delayMs > 0)
            fade.DelayTime = TimeSpan.FromMilliseconds(delayMs);

        visual.StartAnimation("Opacity", fade);
    }
}