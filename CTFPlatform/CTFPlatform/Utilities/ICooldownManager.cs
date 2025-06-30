using CTFPlatform.Models;
using CTFPlatform.Models.Settings;
using Microsoft.EntityFrameworkCore;

namespace CTFPlatform.Utilities;

public interface ICooldownManager
{
    Task<DateTime?> GetUserCooldown(CtfUser? user);
}

public class AppCooldownManager(
    IDbContextFactory<BlazorCtfPlatformContext> dbContextFactory,
    IStoredSettingsManager<ApplicationSettings> settingsManager) : ICooldownManager
{
    public async Task<DateTime?> GetUserCooldown(CtfUser? user)
    {
        var settings = await settingsManager.GetSettingsAsync();
        if (!settings.EnableSpawningCooldown)
            return null;

        var cooldownTime = settings.CooldownTimespan;
        var cooldownLimit = settings.CooldownLimit;

        await using var context = await dbContextFactory.CreateDbContextAsync();
        
        var created = context.UserInstances.Where(t =>
            t.User == user && t.RequestCreated >= DateTime.UtcNow.AddMinutes(-cooldownTime));
        
        if (created.Count() < cooldownLimit)
            return null;

        return created.Min(t => t.RequestCreated).AddMinutes(cooldownTime).TrySpecifyKind(DateTimeKind.Utc);
    }
}