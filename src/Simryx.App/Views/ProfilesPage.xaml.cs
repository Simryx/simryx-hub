using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.ApplicationModel.Resources;
using Simryx.App.Models;
using Simryx.App.Services;
using Windows.Foundation;

namespace Simryx.App.Views;

public sealed partial class ProfilesPage : Page
{
    private const double GroupAnimMs = 260;
    private const double ChevronAnimMs = 220;
    private const double ReorderAnimMs = 300;

    private readonly ResourceLoader _res = new();
    private readonly ProfileService _service = new();

    // Активные анимации высоты по элементу-содержимому (защита от конфликтов).
    private readonly Dictionary<FrameworkElement, Storyboard> _heightAnimations = new();

    public ObservableCollection<GameGroup> Groups { get; } = new();

    public ProfilesPage()
    {
        InitializeComponent();
        Loading += OnLoading;
        Loaded += OnLoaded;
    }

    private void OnLoading(FrameworkElement sender, object args)
    {
        if (!MotionService.Reduced)
            EntranceAnimations.Hide(GroupsScroll);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        Groups.Clear();
        var all = _service.GetAll();
        var priorities = _service.GetPriorities();
        var grouped = all
            .GroupBy(GameCatalog.ResolveGameId)
            .OrderBy(g => GameNameFor(g.Key), StringComparer.CurrentCultureIgnoreCase);
        foreach (var group in grouped)
        {
            var gameId = group.Key;
            priorities.TryGetValue(gameId, out var priorityId);
            var gg = new GameGroup
            {
                GameId = gameId,
                GameName = GameNameFor(gameId),
            };
            foreach (var profile in group
                         .OrderByDescending(p => p.Id == priorityId)
                         .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                profile.IsPriority = profile.Id == priorityId;
                gg.Profiles.Add(profile);
            }
            Groups.Add(gg);
        }

        var any = Groups.Count > 0;
        GroupsScroll.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        if (any && !MotionService.Reduced)
            EntranceAnimations.Play(GroupsScroll);
    }

    private static string GameNameFor(string gameId)
        => GameCatalog.FindById(gameId)?.Name ?? gameId;

    // ===== Раскрытие/сворачивание группы (переключает только кнопка-стрелка) =====
    private void ToggleGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement button) return;
        if (button.DataContext is not GameGroup group) return;
        if (button.Parent is not FrameworkElement header) return;   // header Grid
        if (header.Parent is not Panel panel) return;               // StackPanel

        var content = panel.Children.OfType<ListView>().FirstOrDefault();
        if (content is null) return;

        var chevron = FindByTag(button, "chevron");
        group.IsExpanded = !group.IsExpanded;
        AnimateGroup(content, chevron, group.IsExpanded);
    }

    private void AnimateGroup(FrameworkElement content, FrameworkElement? chevron, bool expand)
    {
        // Поворот стрелки.
        if (chevron is not null)
        {
            if (chevron.RenderTransform is not RotateTransform rotate)
            {
                rotate = new RotateTransform();
                chevron.RenderTransformOrigin = new Point(0.5, 0.5);
                chevron.RenderTransform = rotate;
            }
            var targetAngle = expand ? 0d : 180d;
            if (MotionService.Reduced) rotate.Angle = targetAngle;
            else AnimateChevron(rotate, targetAngle);
        }

        // Останавливаем предыдущую анимацию высоты, чтобы продолжить с текущего значения.
        var currentHeight = content.ActualHeight;
        var wasAnimating = _heightAnimations.TryGetValue(content, out var previous);
        if (wasAnimating)
        {
            previous!.Stop();
            _heightAnimations.Remove(content);
        }

        if (MotionService.Reduced)
        {
            content.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
            content.Height = double.NaN;
            content.Opacity = 1;
            return;
        }

        double from, to;
        if (expand)
        {
            content.Visibility = Visibility.Visible;
            content.Height = double.NaN;
            var availableWidth = double.PositiveInfinity;
            if (content.Parent is FrameworkElement parent && parent.ActualWidth > 0)
                availableWidth = Math.Max(0, parent.ActualWidth - content.Margin.Left - content.Margin.Right);
            content.Measure(new Size(availableWidth, double.PositiveInfinity));
            to = content.DesiredSize.Height;
            from = wasAnimating ? currentHeight : 0;
        }
        else
        {
            from = wasAnimating ? currentHeight : content.ActualHeight;
            to = 0;
        }

        content.Height = from;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var storyboard = new Storyboard();

        var heightAnim = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(GroupAnimMs)),
            EasingFunction = ease,
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(heightAnim, content);
        Storyboard.SetTargetProperty(heightAnim, "Height");
        storyboard.Children.Add(heightAnim);

        var fadeAnim = new DoubleAnimation
        {
            To = expand ? 1 : 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(GroupAnimMs)),
            EasingFunction = ease,
        };
        Storyboard.SetTarget(fadeAnim, content);
        Storyboard.SetTargetProperty(fadeAnim, "Opacity");
        storyboard.Children.Add(fadeAnim);

        _heightAnimations[content] = storyboard;
        storyboard.Completed += (_, _) =>
        {
            _heightAnimations.Remove(content);
            if (expand)
            {
                content.Height = double.NaN; // обратно в авто-высоту
                content.Opacity = 1;
            }
            else
            {
                content.Visibility = Visibility.Collapsed;
                content.Height = 0;
            }
        };
        storyboard.Begin();
    }

    private static void AnimateChevron(RotateTransform rotate, double targetAngle)
    {
        var storyboard = new Storyboard();
        var anim = new DoubleAnimation
        {
            To = targetAngle,
            Duration = new Duration(TimeSpan.FromMilliseconds(ChevronAnimMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(anim, rotate);
        Storyboard.SetTargetProperty(anim, "Angle");
        storyboard.Children.Add(anim);
        storyboard.Begin();
    }

    // ===== Приоритет (плавная перестановка приёмом FLIP) =====
    private void SetPriority_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RacingProfile profile) return;

        var gameId = GameCatalog.ResolveGameId(profile);
        _service.SetPriority(gameId, profile.Id);

        var group = Groups.FirstOrDefault(g => g.GameId == gameId);
        if (group is null) { Refresh(); return; }

        foreach (var p in group.Profiles)
            p.IsPriority = p.Id == profile.Id;

        var index = group.Profiles.IndexOf(profile);
        if (index <= 0) return;

        var listView = FindAncestor<ListView>(sender as DependencyObject);
        if (listView is null || MotionService.Reduced)
        {
            group.Profiles.Move(index, 0);
            return;
        }

        AnimateReorder(listView, group.Profiles, () => group.Profiles.Move(index, 0));
    }

    /// <summary>
    /// Анимирует перестановку элементов списка приёмом FLIP:
    /// запомнить старые позиции → переставить → сдвинуть контейнеры на старые места
    /// и плавно вернуть в новые.
    /// </summary>
    private void AnimateReorder(ListView listView, IEnumerable<object> items, Action reorder)
    {
        var snapshot = items.ToList();

        // 1. Старые позиции контейнеров (относительно списка).
        var oldPositions = new Dictionary<object, double>();
        foreach (var item in snapshot)
        {
            if (listView.ContainerFromItem(item) is UIElement container)
                oldPositions[item] = container.TransformToVisual(listView)
                                              .TransformPoint(new Point(0, 0)).Y;
        }

        // 2. Перестановка.
        reorder();

        // 3. После пересчёта раскладки анимируем разницу.
        listView.UpdateLayout();
        foreach (var item in snapshot)
        {
            if (listView.ContainerFromItem(item) is not UIElement container) continue;
            if (!oldPositions.TryGetValue(item, out var oldY)) continue;
            var newY = container.TransformToVisual(listView)
                                .TransformPoint(new Point(0, 0)).Y;
            var delta = oldY - newY;
            if (Math.Abs(delta) < 0.5) continue;
            AnimateTranslateFrom(container, delta);
        }
    }

    private static void AnimateTranslateFrom(UIElement element, double fromOffsetY)
    {
        if (element.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            element.RenderTransform = transform;
        }
        transform.Y = fromOffsetY;

        var storyboard = new Storyboard();
        var anim = new DoubleAnimation
        {
            From = fromOffsetY,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(ReorderAnimMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(anim, transform);
        Storyboard.SetTargetProperty(anim, "Y");
        storyboard.Children.Add(anim);
        storyboard.Begin();
    }

    // ===== CRUD =====
    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var profile = new RacingProfile();
        if (await ShowEditorAsync(profile, isNew: true))
        {
            _service.Add(profile);
            Refresh();
        }
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RacingProfile profile) return;
        var draft = Clone(profile);
        if (await ShowEditorAsync(draft, isNew: false))
        {
            _service.Update(draft);
            Refresh();
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RacingProfile profile) return;
        _service.Delete(profile.Id);
        Refresh();
    }

    private static RacingProfile Clone(RacingProfile p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        GameId = p.GameId,
        Game = p.Game,
        Sensitivity = p.Sensitivity,
        ForceFeedback = p.ForceFeedback,
        Notes = p.Notes,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
    };

    private async Task<bool> ShowEditorAsync(RacingProfile profile, bool isNew)
    {
        var nameBox = new TextBox
        {
            Header = _res.GetString("ProfileEditorName"),
            PlaceholderText = _res.GetString("ProfileEditorNamePlaceholder"),
            Text = profile.Name,
        };
        var gameCombo = new ComboBox
        {
            Header = _res.GetString("ProfileEditorGame"),
            PlaceholderText = _res.GetString("ProfileEditorGamePlaceholder"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = GameCatalog.All,
            DisplayMemberPath = nameof(SimGame.Name),
        };
        var current = GameCatalog.FindById(profile.GameId) ?? GameCatalog.FindByName(profile.Game);
        if (current is not null) gameCombo.SelectedItem = current;

        var sensSlider = new Slider
        {
            Header = _res.GetString("ProfileEditorSensitivity"),
            Minimum = 0,
            Maximum = 100,
            Value = profile.Sensitivity,
        };
        var forceSlider = new Slider
        {
            Header = _res.GetString("ProfileEditorForce"),
            Minimum = 0,
            Maximum = 100,
            Value = profile.ForceFeedback,
        };
        var notesBox = new TextBox
        {
            Header = _res.GetString("ProfileEditorNotes"),
            Text = profile.Notes,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 90,
        };

        var panel = new StackPanel { Spacing = 12, Width = 360 };
        panel.Children.Add(nameBox);
        panel.Children.Add(gameCombo);
        panel.Children.Add(sensSlider);
        panel.Children.Add(forceSlider);
        panel.Children.Add(notesBox);

        var dialog = new ContentDialog
        {
            Title = isNew
                ? _res.GetString("ProfileEditorNewTitle")
                : _res.GetString("ProfileEditorEditTitle"),
            PrimaryButtonText = _res.GetString("ProfileEditorSave"),
            CloseButtonText = _res.GetString("ProfileEditorCancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme,
            Content = panel,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(profile.Name),
        };
        nameBox.TextChanged += (_, _) =>
            dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;

        profile.Name = nameBox.Text?.Trim() ?? string.Empty;
        if (gameCombo.SelectedItem is SimGame game)
        {
            profile.GameId = game.Id;
            profile.Game = game.Name;
        }
        profile.Sensitivity = (int)Math.Round(sensSlider.Value);
        profile.ForceFeedback = (int)Math.Round(forceSlider.Value);
        profile.Notes = notesBox.Text?.Trim() ?? string.Empty;
        return true;
    }

    // ===== Хелперы дерева =====
    private static FrameworkElement? FindByTag(DependencyObject root, string tag)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement fe && fe.Tag as string == tag)
                return fe;
            var nested = FindByTag(child, tag);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : class
    {
        while (node is not null)
        {
            if (node is T match) return match;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }
}