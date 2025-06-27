using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using CTFPlatform.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace CTFPlatform.Utilities;

public interface IStoredSettingsManager<T>
{
    Task<T> GetSettingsAsync();
    Task SaveSettingsAsync();
}

// TODO Learn how to do DB proxies
public class DbContextStoredSettingsManager<T>(IDbContextFactory<BlazorCtfPlatformContext> contextFactory) : IStoredSettingsManager<T> where T : new()
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly Dictionary<string, PropertyInfo> Properties;
    private static T? _settings;
    
    static DbContextStoredSettingsManager()
    {
        Properties = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p);
    }


    public async Task<T> GetSettingsAsync() =>
        _settings ??= await FetchSettings();

    private async Task<T> FetchSettings()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var settings = new T();
        
        foreach (var property in Properties)
        {
            var setting = context.Settings.FirstOrDefault(t => t.Key == property.Value.Name);
            if(setting != null)
                property.Value.SetValue(settings, JsonConvert.DeserializeObject(setting.Value, property.Value.PropertyType, new JsonSerializerSettings
                {
                    Converters = [new X509Certificate2JsonConverter()]
                }));
        }

        return settings;
    }

    public async Task SaveSettingsAsync()
    {
        if (_settings == null)
            throw new InvalidDataException("Settings not initialised.");
        
        await using var context = await contextFactory.CreateDbContextAsync();
        
        foreach (var property in Properties)
        {
            var value = property.Value.GetValue(_settings);     
            var setting = context.Settings.FirstOrDefault(t => t.Key == property.Key);
            
            if (setting != null)
                setting.Value = JsonConvert.SerializeObject(value, property.Value.PropertyType, new JsonSerializerSettings
                    {
                        Converters = [new X509Certificate2JsonConverter()]
                    });
            else
                context.Add(new Setting
                {
                    Key = property.Key,
                    Value = JsonConvert.SerializeObject(value, property.Value.PropertyType, new JsonSerializerSettings
                        {
                            Converters = [new X509Certificate2JsonConverter()]
                        })
                });
        }
        
        await context.SaveChangesAsync();
    }
    
    
}