using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CTFPlatform.Models.Settings;

namespace CTFPlatform.Utilities;

public interface IVpnCertificateManager
{
    Task<bool> CheckVpnAvailableAsync();
    Task EnsureServerCertificatesCreatedAsync();
    Task GenerateNewRootCertificateAsync();
    Task GenerateNewServerCertificateAsync();
    Task<X509Certificate2> GenerateSignedCertificateAsync(string userId);

    Task<byte[]> GetCaCertificate();
    Task<byte[]> GetServerCertificate();
}

public class AppVpnCertificateManager(
        IStoredSettingsManager<ApplicationSettings> appSettingsManager,
        IStoredSettingsManager<VpnInfo> vpnSettingsManager
    ) : IVpnCertificateManager
{
    private VpnInfo? _vpnInfo;

    public async Task<bool> CheckVpnAvailableAsync()
    {
        var settings = await appSettingsManager.GetSettingsAsync();
        return settings.EnableVpnManager &&
               !string.IsNullOrWhiteSpace(settings.OpenVPNTemplate);
    }

    public async Task EnsureServerCertificatesCreatedAsync()
    {
        _vpnInfo ??= await vpnSettingsManager.GetSettingsAsync();
        if (_vpnInfo.RootCertificate == null || _vpnInfo.RootCertificate.NotAfter <= DateTimeOffset.Now.AddMonths(-6))
            await GenerateNewRootCertificateAsync();
        if (_vpnInfo.ServerCertificate == null || _vpnInfo.ServerCertificate.NotAfter <= DateTimeOffset.Now.AddDays(-14))
            await GenerateNewServerCertificateAsync();
    }

    public async Task GenerateNewRootCertificateAsync()
    {
        _vpnInfo ??= await vpnSettingsManager.GetSettingsAsync();
        _vpnInfo.RootCertificate?.Dispose();
        
        var appSettings = await appSettingsManager.GetSettingsAsync();
        var newCert = GenerateSignedCertificate(appSettings.RootSubject);
        _vpnInfo.RootCertificate = newCert;

        await GenerateNewServerCertificateAsync();
    }

    public async Task GenerateNewServerCertificateAsync()
    {
        _vpnInfo ??= await vpnSettingsManager.GetSettingsAsync();
        _vpnInfo.ServerCertificate?.Dispose();
        
        var appSettings = await appSettingsManager.GetSettingsAsync();
        var newCert = GenerateSignedCertificate(appSettings.ServerSubject, _vpnInfo.RootCertificate!, false);
        _vpnInfo.ServerCertificate = newCert;

        await vpnSettingsManager.SaveSettingsAsync();
    }

    public async Task<X509Certificate2> GenerateSignedCertificateAsync(string userId)
    {
        await EnsureServerCertificatesCreatedAsync();
        return GenerateSignedCertificate("user_" + userId, _vpnInfo!.RootCertificate);
    }

    public async Task<byte[]> GetCaCertificate()
    {
        await EnsureServerCertificatesCreatedAsync();
        _vpnInfo ??= await vpnSettingsManager.GetSettingsAsync();
        return Encoding.UTF8.GetBytes(_vpnInfo.RootCertificate!.ExportCertificatePem());
    }

    public async Task<byte[]> GetServerCertificate()
    {
        await EnsureServerCertificatesCreatedAsync();
        _vpnInfo ??= await vpnSettingsManager.GetSettingsAsync();
        
        
        return Encoding.UTF8.GetBytes(
            _vpnInfo.ServerCertificate!.ExportCertificatePem() + "\n" +  
            _vpnInfo.ServerCertificate!.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem()
        );
    }

    private static X509Certificate2 GenerateSignedCertificate(string subjectName, X509Certificate2? rootCertificate = null, bool isClient = true, int keySize = 2048)
    {
        using var rsa = RSA.Create(keySize);
        var request = new CertificateRequest(
            $"cn={subjectName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );

        request.CertificateExtensions.Add(rootCertificate == null
            ? new X509BasicConstraintsExtension(true, true, 2, true)
            : new X509BasicConstraintsExtension(false, false, 0, true));
        
        request.CertificateExtensions.Add(rootCertificate == null
            ? new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign, true)
            : new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.KeyAgreement, true)
        );

        if (rootCertificate != null)
            if(isClient)
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection()
                {
                    new Oid("serverAuth", "TLS Web Server Authentication")
                }, true));
            else
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection()
                {
                    new Oid("clientAuth", "TLS Web Client Authentication")
                }, true));
        
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension());

        var notBefore = DateTimeOffset.Now;

        X509Certificate2? certificate = null;
            if (rootCertificate == null)
            {
                var notAfter = DateTimeOffset.Now.AddYears(3);
                certificate = request.CreateSelfSigned(notBefore, notAfter);
            }
            else
            {
                var notAfter = isClient ? DateTimeOffset.Now.AddDays(30) : DateTimeOffset.Now.AddMonths(6);
                certificate = request.Create(rootCertificate, notBefore, notAfter, GenerateRandomByteArray(20));
            }

            return certificate.HasPrivateKey ? certificate : 
                X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), "", X509KeyStorageFlags.Exportable).CopyWithPrivateKey(rsa);
    }
    
    private static byte[] GenerateRandomByteArray(int length)
    {
        var randomData = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomData);

        return randomData;
    }
}