using System.Diagnostics;
using CTFPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace CTFPlatform.Utilities;

public interface IInstanceManager
{
    Task<ChallengeInstance?> CheckChallengeInstance(InstanceChallenge challenge, CtfUser user);
    Task<ChallengeInstance?> GetOrDeployChallengeInstance(InstanceChallenge challenge, CtfUser user);
    Task<bool> KillChallengeInstance(ChallengeInstance challengeInstance);
    Task Clean();
}

public interface ICleanupManager
{
    Task Clean();
}

public class TerraformCleanupManager(IServiceProvider services) : ICleanupManager
{
    public async Task Clean()
    {
        var instanceManager = services.GetRequiredService<IInstanceManager>();
        await instanceManager.Clean();
    }
}

public class InstanceRunnerException(string message, Exception innerException) : Exception(message, innerException);

public class TerraformInstanceManager(
    IConfiguration config,
    IDbContextFactory<BlazorCtfPlatformContext> dbFactory) : IInstanceManager
{
    public async Task<ChallengeInstance?> CheckChallengeInstance(InstanceChallenge challenge, CtfUser user)
    {
        await using var context = await dbFactory.CreateDbContextAsync();
        return context.ChallengeInstances.FirstOrDefault(t => t.User == user && t.Challenge == challenge);
    }

    public async Task<ChallengeInstance?> GetOrDeployChallengeInstance(InstanceChallenge challenge, CtfUser user)
    {
        var instance = await CheckChallengeInstance(challenge, user);
        if (instance != null)
            return instance;

        await using var context = await dbFactory.CreateDbContextAsync();
        if (challenge.Shared)
        {
            var sharedInstance = context.ChallengeInstances.FirstOrDefault(t => t.Challenge == challenge);
            if (sharedInstance != null)
            {
                instance = new ChallengeInstance
                {
                    Challenge = challenge,
                    User = user,
                    InstanceExpiry = DateTime.UtcNow.AddSeconds(challenge.ExpiryTime ?? 86400),
                    Host = sharedInstance.Host,
                    DeploymentPath = sharedInstance.DeploymentPath
                };
                context.ChallengeInstances.Add(instance);
                await context.SaveChangesAsync();
            }
        }
        
        Guid directory;
        do directory = Guid.NewGuid();
        while(Directory.Exists(config["ManifestDirectory"] + Path.DirectorySeparatorChar + "Deployments" + Path.DirectorySeparatorChar + directory));
            
        context.Attach(challenge).State = EntityState.Unchanged;
        context.Attach(user).State = EntityState.Unchanged;
        
        instance = new ChallengeInstance
        {
            Challenge = challenge,
            User = user,
            InstanceExpiry = DateTime.UtcNow.AddSeconds(challenge.ExpiryTime ?? 86400),
            Host = "",
            DeploymentPath = directory.ToString()
        };
        context.ChallengeInstances.Add(instance);
        await context.SaveChangesAsync();
        
        var manifestPath = config["ManifestDirectory"] + Path.DirectorySeparatorChar + challenge.DeploymentManifestPath;
        var deploymentPath = config["ManifestDirectory"] + Path.DirectorySeparatorChar + "Deployments" + Path.DirectorySeparatorChar + directory;

        Dictionary<string, string> outputs;
        try
        {
            Directory.CreateDirectory(deploymentPath);
            outputs = await StartDeployment(deploymentPath, Path.GetRelativePath(deploymentPath, manifestPath));
        }
        catch
        {
            return null;
        }

        instance.Host = CreateHostString(challenge.HostFormat, outputs);
        await context.SaveChangesAsync();
        return instance;
    }

    public async Task<bool> KillChallengeInstance(ChallengeInstance challengeInstance)
    {
        await using var context = await dbFactory.CreateDbContextAsync();
        
        var instance = context.ChallengeInstances
            .Include(i => i.Challenge)
            .FirstOrDefault(t => t.Id == challengeInstance.Id);
        
        if(instance == null)
            return true;
            
        if (instance.Challenge.Shared && context.ChallengeInstances
                .Any(t => t.DeploymentPath == challengeInstance.DeploymentPath && t != challengeInstance))
        {
            context.ChallengeInstances.Remove(instance);
            await context.SaveChangesAsync();
    
            return true;
        }
        
        var directory = challengeInstance.DeploymentPath;
        var deploymentPath = config["ManifestDirectory"] + Path.DirectorySeparatorChar + "Deployments" + Path.DirectorySeparatorChar + directory;
        if (Directory.Exists(deploymentPath))
        {
            try
            {
                await KillDeployment(deploymentPath);
                Directory.Delete(deploymentPath, true);
            }
            catch
            {
                return false;
            }
        }
        
        context.ChallengeInstances.Remove(instance);
        await context.SaveChangesAsync();
        
        return true;
    }

    public async Task Clean()
    {
        await using var context = await dbFactory.CreateDbContextAsync();
        var expiredInstances = context.ChallengeInstances.Where(t => t.InstanceExpiry <= DateTime.UtcNow).ToList();
        foreach (var instance in expiredInstances)
            await KillChallengeInstance(instance);

        var deploymentBasePath = new DirectoryInfo(config["ManifestDirectory"] + Path.DirectorySeparatorChar + "Deployments");
        var instanceDeployments = context.ChallengeInstances.Select(t => t.DeploymentPath).Distinct().ToList();
        var deploymentFolders = Directory.GetDirectories(deploymentBasePath.FullName).Select(t => t.Replace(deploymentBasePath.FullName + Path.DirectorySeparatorChar, ""));
        var orphaned = deploymentFolders.Where(t => !instanceDeployments.Contains(t));
        foreach (var orphan in orphaned)
        {
            try
            {
                var deploymentPath = config["ManifestDirectory"] + Path.DirectorySeparatorChar + "Deployments" +
                                     Path.DirectorySeparatorChar + orphan;
                await KillDeployment(deploymentPath);
                Directory.Delete(deploymentPath, true);
            }
            catch
            {
                // ignored
            }
        }
    }

    private string CreateHostString(string hostFormat, Dictionary<string, string> outputs)
    {
        foreach (var output in outputs)
            hostFormat = hostFormat.Replace($"$({output.Key})", output.Value);
        return hostFormat;
    }

    private async Task<Dictionary<string, string>> StartDeployment(string deploymentPath, string manifestPath)
    {
        var outputText = "";
        var standardError = "";
        
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "terraform",
                Arguments = $"init -force-copy -from-module={manifestPath}",
                WorkingDirectory = deploymentPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();
            outputText = await process.StandardOutput.ReadToEndAsync();
            Console.Write(outputText);
            standardError = await process.StandardError.ReadToEndAsync();
            Console.Write(standardError);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception("Program did not exit correctly.");
        }
        catch (Exception e)
        {
            throw new InstanceRunnerException("Error initialising instance.", e)
            {
                Data = { { "stdout", outputText }, { "stderr", standardError } }
            };
        }
        
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "terraform",
                Arguments = $"apply -auto-approve",
                WorkingDirectory = deploymentPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();
            outputText = await process.StandardOutput.ReadToEndAsync();
            Console.Write(outputText);
            standardError = await process.StandardError.ReadToEndAsync();
            Console.Write(standardError);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception("Program did not exit correctly.");
        }
        catch (Exception e)
        {
            throw new InstanceRunnerException("Error starting instance.", e)
            {
                Data = { { "stdout", outputText }, { "stderr", standardError } }
            };
        }
        
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "terraform",
                Arguments = $"output",
                WorkingDirectory = deploymentPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();
            outputText = await process.StandardOutput.ReadToEndAsync();
            Console.Write(outputText);
            standardError = await process.StandardError.ReadToEndAsync();
            Console.Write(standardError);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception("Program did not exit correctly.");
        }
        catch (Exception e)
        {
            throw new InstanceRunnerException("Error starting instance.", e)
            {
                Data = { { "stdout", outputText }, { "stderr", standardError } }
            };
        }

        var outputSplit = outputText.Split('\n');
        var outputs = new Dictionary<string, string>();
        foreach (var pair in outputSplit)
        {
            var pairSplit = pair.Split(" = ");
            if(pairSplit.Length != 2)
                continue;
            outputs.Add(pairSplit[0], pairSplit[1]);
        }

        return outputs;
    }

    private async Task KillDeployment(string deploymentPath)
    {
        var outputText = "";
        var standardError = "";
        
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "terraform",
                Arguments = @$"destroy -auto-approve",
                WorkingDirectory = deploymentPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();
            outputText = await process.StandardOutput.ReadToEndAsync();
            Console.Write(outputText);
            standardError = await process.StandardError.ReadToEndAsync();
            Console.Write(standardError);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception("Program did not exit correctly.");
        }
        catch (Exception e)
        {
            throw new InstanceRunnerException("Error killing instance.", e)
            {
                Data = { { "stdout", outputText }, { "stderr", standardError } }
            };
        }
    }
}