using CTFPlatform.Models;
using CTFPlatform.Models.Settings;
using Microsoft.EntityFrameworkCore;

namespace CTFPlatform.Utilities;

public interface ICtfActivityManager
{
    Task<bool> CtfActive();
    Task<DateTime?> GetUserCooldown(int userId);
}

public class AppCtfActivityManager(
    IDbContextFactory<BlazorCtfPlatformContext> dbContextFactory,
    IStoredSettingsManager<ApplicationSettings> settingsManager) : ICtfActivityManager
{
    public async Task<bool> CtfActive() =>
        !(await settingsManager.GetSettingsAsync()).FreezeCtf;

    public async Task<DateTime?> GetUserCooldown(int userId)
    {
        var settings = await settingsManager.GetSettingsAsync();
        if(settings.FreezeCtf)
            return DateTime.MaxValue;

        if (!settings.EnableSpawningCooldown)
            return null;

        var cooldownTime = settings.CooldownTimespan;
        var cooldownLimit = settings.CooldownLimit;

        await using var context = await dbContextFactory.CreateDbContextAsync();
        
        var created = context.UserInstances.Where(t =>
            t.User.Id == userId && t.RequestCreated >= DateTime.UtcNow.AddMinutes(-cooldownTime));
        
        if (created.Count() < cooldownLimit)
            return null;
                
        return !created.Any() ? DateTime.MaxValue :
            created.Min(t => t.RequestCreated).AddMinutes(cooldownTime).TrySpecifyKind(DateTimeKind.Utc);
    }
}