using Microsoft.Extensions.DependencyInjection;

namespace Simryx.App.Services;

public static class OnboardingService
{
    private const string Key = "OnboardingDone";

    public static bool IsDone
    {
        get
        {
            var settings = App.Services.GetRequiredService<ILocalSettingsService>();
            return settings.Read<bool?>(Key) ?? false;
        }
    }

    public static void MarkDone()
    {
        var settings = App.Services.GetRequiredService<ILocalSettingsService>();
        settings.Save(Key, true);
    }
}