namespace CTFPlatform.Models;

public class UserInstance
{
    public int Id { get; set; }
    public bool KillProcessed { get; set; }
    public virtual CtfUser User { get; set; }
    public virtual ChallengeInstance Instance { get; set; }
    public DateTime RequestCreated { get; set; }
}