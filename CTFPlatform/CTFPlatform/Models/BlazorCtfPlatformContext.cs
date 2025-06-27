using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace CTFPlatform.Models;

// CTFPlatform.Models.BlazorCtfPlatformContext, CTFPlatform, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
public class BlazorCtfPlatformContext(DbContextOptions<BlazorCtfPlatformContext> options) : DbContext(options)
{
    public DbSet<Setting> Settings { get; set; }
    public DbSet<Challenge> Challenges { get; set; }
    public DbSet<ChallengeInstance> ChallengeInstances { get; set; }
    public DbSet<CtfFile> Files { get; set; }
    public DbSet<CtfFlag> Flags { get; set; }
    public DbSet<FlagSubmission> FlagSubmissions { get; set; }
    public DbSet<CtfUser> Users { get; set; }
    public DbSet<VpnCertificate> VpnCertificates { get; set; }

    public CtfUser? GetOrCreateUser(ClaimsPrincipal stateUser)
    {
        var authId = stateUser.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(authId))
            throw new InvalidDataException("User must have name claim.");
        
        var user = Users.FirstOrDefault(t => t.AuthId == authId);
        if (user != null)
            return user;
        
        user = new CtfUser()
        {
            AuthId = authId,
            Email = stateUser.FindFirstValue("name") ?? "Not set",
            Avatar = stateUser.FindFirstValue("picture")
        };

        Users.Add(user);
        SaveChanges();

        return user;
    }

    public void CreateOrUpdateUser(ClaimsPrincipal stateUser)
    {
        if (!stateUser.IsInRole(CtfUser.UserRole))
            return;
        
        var authId = stateUser.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(authId))
            throw new InvalidDataException("User must have name claim.");
        
        var user = Users.FirstOrDefault(t => t.AuthId == authId);
        if (user == null)
        {
            user = new CtfUser()
            {
                AuthId = authId,
                Email = stateUser.FindFirstValue("name") ?? "Not set",
                Avatar = stateUser.FindFirstValue("picture")
            };

            Users.Add(user);
        }
        else
        {
            user.Email = stateUser.FindFirstValue("name") ?? "Not set";
            user.Avatar = stateUser.FindFirstValue("picture");
        }
        
        SaveChanges();
    }
}