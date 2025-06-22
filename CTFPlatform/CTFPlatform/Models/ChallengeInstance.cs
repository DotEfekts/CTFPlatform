using System.ComponentModel.DataAnnotations;

namespace CTFPlatform.Models;

public class ChallengeInstance
{
    public int Id { get; set; }
    public DateTime InstanceExpiry { get; set; }
    [Required]
    public string DeploymentPath { get; set; }
    [Required]
    public string Host { get; set; }
    public virtual InstanceChallenge Challenge { get; set; }
    public virtual CtfUser User { get; set; }
}