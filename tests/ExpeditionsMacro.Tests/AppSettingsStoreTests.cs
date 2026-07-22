using ExpeditionsMacro.Core.Models;
using ExpeditionsMacro.Core.Persistence;

namespace ExpeditionsMacro.Tests;

public sealed class AppSettingsStoreTests
{
    [Fact]
    public async Task ReportingSettings_SurviveAStoreRestart()
    {
        string root = Path.Combine(Path.GetTempPath(), $"expeditions-settings-{Guid.NewGuid():N}");
        try
        {
            AppSettingsStore firstProcess = new(new AppPaths(root));
            await firstProcess.SaveAsync(new AppSettings
            {
                SelectedMacroPlanId = "daily-plan",
                EncryptedWebhook = "dpapi-protected-test-value",
                DiscordErrorUserId = "123456789012345678",
            });

            AppSettingsStore restartedProcess = new(new AppPaths(root));
            AppSettings loaded = await restartedProcess.LoadAsync();

            Assert.Equal("daily-plan", loaded.SelectedMacroPlanId);
            Assert.Equal("dpapi-protected-test-value", loaded.EncryptedWebhook);
            Assert.Equal("123456789012345678", loaded.DiscordErrorUserId);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
