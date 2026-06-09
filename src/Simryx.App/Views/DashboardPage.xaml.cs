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
	private readonly ILocalSettingsService _settings;
	private readonly ProfileService _profiles = new();
	private bool _loaded;
	private UpdateInfo? _pendingUpdate;

	private bool IsEnglish =>
		(_localization.CurrentLanguage ?? string.Empty).StartsWith("en", StringComparison.OrdinalIgnoreCase);

	public DashboardPage()
	{
		InitializeComponent();
		_theme = App.Services.GetRequiredService<IThemeSelectorService>();
		_localization = App.Services.GetRequiredService<ILocalizationService>();
		_settings = App.Services.GetRequiredService<ILocalSettingsService>();
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

		// ── Часть 4: тихая авто-проверка обновлений при запуске ──
		UpdateCoordinator.UpdateFound -= OnAutoUpdateFound;
		UpdateCoordinator.UpdateFound += OnAutoUpdateFound;

		PopulateStatus();

		// Если фоновая проверка уже нашла обновление раньше в этой сессии — показываем сразу.
		if (UpdateCoordinator.LastAvailable is { } cachedUpdate)
			ApplyUpdateResult(cachedUpdate, IsEnglish);

		// Запускаем фоновую проверку (фактически выполнится один раз за сессию). Fire-and-forget.
		_ = UpdateCoordinator.RunStartupCheckAsync();

		if (MotionService.Reduced) return;

		// Премиальное каскадное появление.
		EntranceAnimations.PlayStaggered(40, 100, Tile1, Tile2, Tile3, Tile4, ActionsPanel);
	}

	private void OnUnloaded(object sender, RoutedEventArgs e)
	{
		_loaded = false;
		ProfileService.ActiveChanged -= OnActiveProfileChanged;

		// ── Часть 4: снимаем подписку, чтобы не текло статическое событие ──
		UpdateCoordinator.UpdateFound -= OnAutoUpdateFound;
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

	// ── Часть 4: колбэк тихой авто-проверки (может прийти из фонового потока) ──
	private void OnAutoUpdateFound(object? sender, UpdateCheckResult result)
	{
		DispatcherQueue.TryEnqueue(() =>
		{
			if (!_loaded) return;          // страница уже выгружена — игнорируем
			ApplyUpdateResult(result, IsEnglish); // тот же путь, что и у ручной проверки
		});
	}

	private void PopulateStatus()
	{
		var en = IsEnglish;
		StatusDevicesValue.Text = en ? "5 (demo)" : "5 (демо)";
		StatusGameValue.Text = en ? "Not running" : "Не запущена";

		var active = _profiles.GetActive();
		StatusProfileValue.Text = active is not null
			? active.Name
			: (en ? "Not selected" : "Не выбран");

		StatusUpdatesValue.Text = en ? "Not checked" : "Не проверялось";
	}

	private void Profiles_Click(object sender, RoutedEventArgs e) => NavigateTo("Profiles");

	// ===== Обновления (Часть 2 + 3 + 4) =====

	private async void Updates_Click(object sender, RoutedEventArgs e)
	{
		var en = IsEnglish;
		CheckUpdatesQuickBtn.IsEnabled = false;
		_pendingUpdate = null;
		StatusUpdatesValue.Text = en ? "Checking…" : "Проверка…";
		try
		{
			var channel = (_settings.Read<string>("UpdateChannel") ?? "Stable")
				.Equals("Beta", StringComparison.OrdinalIgnoreCase)
				? UpdateChannel.Beta
				: UpdateChannel.Stable;

			var result = await new UpdateService().CheckForUpdatesAsync(channel);
			ApplyUpdateResult(result, en);
		}
		catch (Exception ex)
		{
			StatusUpdatesValue.Text = en ? "Check error" : "Ошибка проверки";
			ShowUpdateBar(InfoBarSeverity.Error,
				en ? "Update check failed" : "Ошибка проверки обновлений",
				ex.Message);
		}
		finally
		{
			CheckUpdatesQuickBtn.IsEnabled = true;
		}
	}

	private void ApplyUpdateResult(UpdateCheckResult result, bool en)
	{
		switch (result.Status)
		{
			case UpdateStatus.UpdateAvailable when result.Info is not null:
			{
				_pendingUpdate = result.Info;
				var hasInstaller = !string.IsNullOrWhiteSpace(result.Info.DownloadUrl);
				StatusUpdatesValue.Text = en ? $"Available {result.Info.Version}"
					: $"Доступна {result.Info.Version}";
				var action = hasInstaller
					? (en ? "Update now" : "Обновить сейчас")
					: (en ? "Open release page" : "Открыть страницу релиза");
				ShowUpdateBar(InfoBarSeverity.Informational,
					en ? $"Version {result.Info.Version} is available"
						: $"Доступна версия {result.Info.Version}",
					Truncate(result.Info.ReleaseNotes, 400),
					action);
				break;
			}

			// Откат: пользователь на бета-сборке, а в стабильном канале доступна
			// последняя стабильная версия. Предлагаем вернуться на неё прямо с «Главной».
			case UpdateStatus.RollbackAvailable when result.Info is not null:
			{
				_pendingUpdate = result.Info;
				var hasInstaller = !string.IsNullOrWhiteSpace(result.Info.DownloadUrl);
				StatusUpdatesValue.Text = en ? $"Rollback {result.Info.Version}"
					: $"Откат {result.Info.Version}";
				var action = hasInstaller
					? (en ? "Install stable" : "Установить стабильную")
					: (en ? "Open release page" : "Открыть страницу релиза");
				ShowUpdateBar(InfoBarSeverity.Informational,
					en ? $"You can roll back to stable {result.Info.Version}"
						: $"Можно вернуться на стабильную версию {result.Info.Version}",
					en ? $"You're on a beta build ({result.CurrentVersion.ToString(3)}). "
						 + "Switch back to the latest stable release.\n\n"
						 + Truncate(result.Info.ReleaseNotes, 360)
						: $"У вас бета-сборка ({result.CurrentVersion.ToString(3)}). "
						 + "Можно вернуться на последнюю стабильную версию.\n\n"
						 + Truncate(result.Info.ReleaseNotes, 360),
					action);
				break;
			}

			case UpdateStatus.Failed:
				StatusUpdatesValue.Text = en ? "Check error" : "Ошибка проверки";
				ShowUpdateBar(InfoBarSeverity.Error,
					en ? "Update check failed" : "Ошибка проверки обновлений",
					result.Error ?? string.Empty);
				break;

			default:
				StatusUpdatesValue.Text = en ? "Up to date" : "Актуально";
				ShowUpdateBar(InfoBarSeverity.Success,
					en ? "You're up to date" : "Установлена последняя версия",
					en ? $"Current version: {result.CurrentVersion.ToString(3)}"
						: $"Текущая версия: {result.CurrentVersion.ToString(3)}");
				break;
		}
	}

	private void ShowUpdateBar(InfoBarSeverity severity, string title, string message, string? actionText = null)
	{
		UpdateInfoBar.Severity = severity;
		UpdateInfoBar.Title = title;
		UpdateInfoBar.Message = message;

		var showAction = !string.IsNullOrEmpty(actionText) && _pendingUpdate is not null;
		if (showAction)
		{
			UpdateActionButton.Content = actionText;
			UpdateActionButton.Visibility = Visibility.Visible;
		}
		else
		{
			UpdateActionButton.Visibility = Visibility.Collapsed;
		}

		UpdateInfoBar.IsOpen = true;
	}

	private async void UpdateAction_Click(object sender, RoutedEventArgs e)
	{
		var info = _pendingUpdate;
		if (info is null) return;

		if (!string.IsNullOrWhiteSpace(info.DownloadUrl))
			await UpdateFlow.RunAsync(info, XamlRoot, IsEnglish, ActualTheme);
		else if (!string.IsNullOrWhiteSpace(info.ReleaseUrl))
			await Launcher.LaunchUriAsync(new Uri(info.ReleaseUrl));
	}

	private static string Truncate(string? text, int max)
	{
		if (string.IsNullOrEmpty(text)) return string.Empty;
		text = text.Trim();
		return text.Length <= max ? text : text.Substring(0, max).TrimEnd() + "…";
	}

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