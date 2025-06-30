using System.ComponentModel.DataAnnotations;

namespace CTFPlatform.Models;

public class Challenge
{
    public int Id { get; set; }
    [Required]
    public string Title { get; set; }
    [Required]
    public string Category { get; set; }
    [Required]
    public string Description { get; set; }

    public bool Hidden { get; set; } = false;
    [MinLength(1)]
    [ValidateComplexType]
    public virtual List<CtfFlag> Flags { get; set; }
    public virtual List<CtfFile> Files { get; set; }
}

public class InstanceChallenge : Challenge
{
    [Required]
    public string DeploymentManifestPath { get; set; }
    [Required]
    public int ExpiryTime { get; set; }
    public bool Shared { get; set; }
    [Required]
    public string HostFormat { get; set; }
    [Required]
    public string LoggingInfoFormat { get; set; }
    public virtual List<ChallengeInstance> Instances { get; set; }
}
