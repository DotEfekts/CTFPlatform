using System.Security.Cryptography.X509Certificates;
using CTFPlatform.Utilities;
using Newtonsoft.Json;

namespace CTFPlatform.Models.Settings;

public class VpnInfo
{
    [JsonConverter(typeof(X509Certificate2JsonConverter))]
    public X509Certificate2? RootCertificate { get; set; }
    public X509Certificate2? ServerCertificate { get; set; }
}