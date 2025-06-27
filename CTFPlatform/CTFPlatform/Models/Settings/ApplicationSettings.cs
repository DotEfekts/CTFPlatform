namespace CTFPlatform.Models.Settings;

public class ApplicationSettings
{
    public virtual string? ActivationCode { get; set; }
    public virtual bool EnableVpnManager { get; set; } = false;
    public virtual string RootSubject { get; set; } = "ctfplatform";
    public virtual string ServerSubject { get; set; } = "ctfplatform.vpngateway";
    public virtual string? OpenVPNTemplate { get; set; }
}