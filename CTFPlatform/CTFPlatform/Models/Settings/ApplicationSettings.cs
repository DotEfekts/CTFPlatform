namespace CTFPlatform.Models.Settings;

public class ApplicationSettings
{
    public bool FreezeCtf { get; set; }
    public string? ActivationCode { get; set; }
    public bool EnableSpawningCooldown { get; set; }
    public int CooldownTimespan { get; set; }
    public int CooldownLimit { get; set; }
    public bool EnableVpnManager { get; set; }
    public string RootSubject { get; set; } = "ctfplatform";
    public string ServerSubject { get; set; } = "ctfplatform.vpngateway";
    public string? OpenVpnTemplate { get; set; }
}