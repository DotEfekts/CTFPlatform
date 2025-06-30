using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CTFPlatform.Migrations;
using CTFPlatform.Models;
using CTFPlatform.Models.Settings;
using CTFPlatform.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace CTFPlatform.Controllers;

[ApiController]
[Route("[controller]")]
public class CertificatesController(
    BlazorCtfPlatformContext context, 
    IStoredSettingsManager<ApplicationSettings> settingsManager, 
    IVpnCertificateManager certificateManager,
    ILogger<CertificatesController> logger) : ControllerBase
{
    [Authorize(Roles = $"{CtfUser.UserRole},{CtfUser.AdminRole}")]
    [HttpGet("ca.crt")]
    public async Task<ActionResult> GetCaCertificate()
    {
        await certificateManager.EnsureServerCertificatesCreatedAsync();
        var caCert = await certificateManager.GetCaCertificate();
        return File(caCert, "application/x-x509-ca-cert", "ca.crt");
    }
    
    [Authorize(Roles = CtfUser.UserRole)]
    [HttpGet("vpnprofile")]
    public async Task<ActionResult> GetOpenVpnConfig()
    {
        if (!await certificateManager.CheckVpnAvailableAsync())
            return BadRequest();
        
        var user = context.GetOrCreateUser(User);
        if (user == null || user.Locked)
            return BadRequest();

        
        logger.LogInformation("VPN profile request - User: ({UserId}, {UserAuthId}, {UserDisplayName}).", 
            user.Id, user.AuthId, user.DisplayName ?? user.Email);
        var certStore = user.Certificates.Where(t => t.Expiry >= DateTime.UtcNow.AddDays(5) && t.Valid).ToList();
        foreach (var storedCert in certStore)
        {
            try
            {
                var cert = JsonConvert.DeserializeObject<X509Certificate2>(storedCert.Certificate,
                    new JsonSerializerSettings
                    {
                        Converters = [new X509Certificate2JsonConverter()]
                    });

                if (cert != null)
                    return File(await GenerateVpnConfigAsync(cert), "application/octet-stream",
                        RemoveInvalidChars(user.DisplayName ?? user.Email) + "-ctfplatform-profile.ovpn");
            }
            catch
            {
                // ignored
            }

            storedCert.Valid = false;
            await context.SaveChangesAsync();
        }
        
        
        await certificateManager.EnsureServerCertificatesCreatedAsync();
        var newCert = await certificateManager.GenerateSignedCertificateAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        user.Certificates.Add(new VpnCertificate()
        {
            Certificate = JsonConvert.SerializeObject(newCert, new JsonSerializerSettings
            {
                Converters = [new X509Certificate2JsonConverter()]
            }),
            Expiry = newCert.NotAfter,
            Valid = true
        });
        await context.SaveChangesAsync();
        
        return File(await GenerateVpnConfigAsync(newCert), "application/octet-stream",
            RemoveInvalidChars(user.DisplayName ?? user.Email) + "-ctfplatform-profile.ovpn");
    }

    [Authorize(Roles = CtfUser.AdminRole)]
    [HttpGet("server.pem")]
    public async Task<ActionResult> GetServerCertificate()
    {
        await certificateManager.EnsureServerCertificatesCreatedAsync();
        var serverPem = await certificateManager.GetServerCertificate();
        return File(serverPem, "application/x-pem-file", "server.pem");
    }

    private async Task<byte[]> GenerateVpnConfigAsync(X509Certificate2 cert)
    {
        var settings = await settingsManager.GetSettingsAsync();
        var template = settings.OpenVPNTemplate!;
        
        template = template.Replace("$(ca.crt)", Encoding.UTF8.GetString(await certificateManager.GetCaCertificate()));
        template = template.Replace("$(client.crt)", cert.ExportCertificatePem());
        template = template.Replace("$(client.key)", cert.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem());

        return Encoding.UTF8.GetBytes(template);
    }
    
    private string RemoveInvalidChars(string filename)
    {
        return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
    }
}