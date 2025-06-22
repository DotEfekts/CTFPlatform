using System.ComponentModel.DataAnnotations;

namespace CTFPlatform.Models;

public class CtfFlag
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; }
    [Required]
    public string Flag { get; set; }
    public int Points { get; set; }
    public virtual Challenge Challenge { get; set; }
    public virtual List<FlagSubmission> Submissions { get; set; }
}