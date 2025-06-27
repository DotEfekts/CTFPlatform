using System.ComponentModel.DataAnnotations;

namespace CTFPlatform.Models;

public class VpnCertificate
{
    public int Id { get; set; }
    [Required]
    public string Certificate { get; set; }
    public DateTime Expiry { get; set; }
    public bool Valid { get; set; }
    public virtual CtfUser User { get; set; }
}