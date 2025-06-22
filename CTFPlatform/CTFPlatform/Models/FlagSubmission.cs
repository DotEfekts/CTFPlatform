namespace CTFPlatform.Models;

public class FlagSubmission
{
    public int Id { get; set; }
    public DateTime DateSubmitted { get; set; }
    public virtual CtfUser User { get; set; }
    public virtual CtfFlag Flag { get; set; }
}