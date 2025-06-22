using System.ComponentModel.DataAnnotations;

namespace CTFPlatform.Models;

public class CtfFile
{
    public int Id { get; set; }
    [Required]
    public string Name { get; set; }
    [Required]
    public string StorageLocation { get; set; }
    public virtual Challenge Challenge { get; set; }
}