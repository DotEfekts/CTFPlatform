using System.ComponentModel.DataAnnotations;

namespace CTFPlatform.Models;

public class CtfUser
{
    public int Id { get; set; }
    [Required]
    public string AuthId { get; set; }

    public string Email { get; set; } = "unset";
    public string? Avatar { get; set; }
    public string? DisplayName { get; set; }
    public virtual List<FlagSubmission> Submissions { get; set; }
    public virtual List<ChallengeInstance> Instances { get; set; }
    
    public const string AdminRole = "CTF Admin";
    public const string UserRole = "CTF User";
}