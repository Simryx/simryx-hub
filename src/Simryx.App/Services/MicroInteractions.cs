using System;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;

namespace Simryx.App.Services;

/// <summary>
/// Переиспользуемые микро-анимации наведения и нажатия для любого FrameworkElement.
/// Подключение в XAML: svc:MicroInteractions.IsEnabled="True".
/// Учитывает MotionService.Reduced (настройка «Уменьшить движение»).
/// </summary>
public static class MicroInteractions
{
	// --- IsEnabled ---
	public static readonly DependencyProperty IsEnabledProperty =
		DependencyProperty.RegisterAttached(
			"IsEnabled", typeof(bool), typeof(MicroInteractions),
			new PropertyMetadata(false, OnIsEnabledChanged));

	public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
	public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

	// --- HoverScale ---
	public static readonly DependencyProperty HoverScaleProperty =
		DependencyProperty.RegisterAttached(
			"HoverScale", typeof(double), typeof(MicroInteractions),
			new PropertyMetadata(1.03d));

	public static double GetHoverScale(DependencyObject obj) => (double)obj.GetValue(HoverScaleProperty);
	public static void SetHoverScale(DependencyObject obj, double value) => obj.SetValue(HoverScaleProperty, value);

	// --- PressScale ---
	public static readonly DependencyProperty PressScaleProperty =
		DependencyProperty.RegisterAttached(
			"PressScale", typeof(double), typeof(MicroInteractions),
			new PropertyMetadata(0.97d));

	public static double GetPressScale(DependencyObject obj) => (double)obj.GetValue(PressScaleProperty);
	public static void SetPressScale(DependencyObject obj, double value) => obj.SetValue(PressScaleProperty, value);

	private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is not FrameworkElement element) return;

		// Снять старые подписки, чтобы не дублировать
		element.PointerEntered -= OnPointerEntered;
		element.PointerExited -= OnPointerExited;
		element.PointerPressed -= OnPointerPressed;
		element.PointerReleased -= OnPointerReleased;
		element.PointerCanceled -= OnPointerExited;
		element.PointerCaptureLost -= OnPointerExited;

		if (e.NewValue is true)
		{
			element.PointerEntered += OnPointerEntered;
			element.PointerExited += OnPointerExited;
			element.PointerPressed += OnPointerPressed;
			element.PointerReleased += OnPointerReleased;
			element.PointerCanceled += OnPointerExited;
			element.PointerCaptureLost += OnPointerExited;
		}
	}

	private static void OnPointerEntered(object sender, PointerRoutedEventArgs e)
	{
		if (sender is FrameworkElement fe) ScaleTo(fe, (float)GetHoverScale(fe));
	}

	private static void OnPointerExited(object sender, PointerRoutedEventArgs e)
	{
		if (sender is FrameworkElement fe) ScaleTo(fe, 1.0f);
	}

	private static void OnPointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (sender is FrameworkElement fe) ScaleTo(fe, (float)GetPressScale(fe), 90);
	}

	private static void OnPointerReleased(object sender, PointerRoutedEventArgs e)
	{
		// Вернуться к hover-состоянию (курсор ещё над элементом)
		if (sender is FrameworkElement fe) ScaleTo(fe, (float)GetHoverScale(fe));
	}

	private static void ScaleTo(FrameworkElement element, float scale, int durationMs = 150)
	{
		if (MotionService.Reduced)
		{
			// Сбросить любой остаточный масштаб без анимации
			var v = ElementCompositionPreview.GetElementVisual(element);
			v.Scale = Vector3.One;
			return;
		}

		if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return;

		var visual = ElementCompositionPreview.GetElementVisual(element);
		var comp = visual.Compositor;

		visual.CenterPoint = new Vector3(
			(float)element.ActualWidth / 2f,
			(float)element.ActualHeight / 2f,
			0f);

		var anim = comp.CreateVector3KeyFrameAnimation();
		anim.InsertKeyFrame(1f, new Vector3(scale, scale, 1f));
		anim.Duration = TimeSpan.FromMilliseconds(durationMs);

		visual.StartAnimation("Scale", anim);
	}
}