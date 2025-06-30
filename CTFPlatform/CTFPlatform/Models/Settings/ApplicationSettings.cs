namespace CTFPlatform.Models.Settings;

public class ApplicationSettings
{
    public string? ActivationCode { get; set; }
    public bool EnableSpawningCooldown { get; set; } = false;
    public int CooldownTimespan { get; set; }
    public int CooldownLimit { get; set; }
    public bool EnableVpnManager { get; set; } = false;
    public string RootSubject { get; set; } = "ctfplatform";
    public string ServerSubject { get; set; } = "ctfplatform.vpngateway";
    public string? OpenVPNTemplate { get; set; }
}