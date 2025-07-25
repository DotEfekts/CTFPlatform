using System.Diagnostics;
using CTFPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace CTFPlatform.Utilities;

public interface IInstanceManager
{
    Task<ChallengeInstance?> CheckChallengeInstance(int challengeId, int userId);
    Task<ChallengeInstance?> GetOrDeployChallengeInstance(int challengeId, int userId);
    Task<bool> KillUserInstance(int instanceId, int userId);
    Task<bool> KillChallengeInstance(int instanceId);
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
    IDbContextFactory<BlazorCtfPlatformContext> dbFactory,
    ILogger<TerraformInstanceManager> logger)
    : IInstanceManager
{
    private readonly string _manifestPath = config["ManifestDirectory"] + Path.DirectorySeparatorChar;
    private readonly string _deploymentsPath = config["DeploymentDirectory"] + Path.DirectorySeparatorChar;

    public async Task<ChallengeInstance?> CheckChallengeInstance(int challengeId, int userId)
    {
        await using var context = await dbFactory.CreateDbContextAsync();
        return context.UserInstances.Include(userInstance => userInstance.Instance)
            .FirstOrDefault(t => t.User.Id == userId && !t.KillProcessed && t.Instance.Challenge.Id == challengeId)?.Instance;
    }

    public async Task<ChallengeInstance?> GetOrDeployChallengeInstance(int challengeId, int userId)
    {
        var instance = await CheckChallengeInstance(challengeId, userId);
        if (instance != null)
            return instance;

        await using var context = await dbFactory.CreateDbContextAsync();
        
        var user = context.Users.FirstOrDefault(t => t.Id == userId);
        var challenge = context.Challenges.OfType<InstanceChallenge>().FirstOrDefault(t => t.Id == challengeId);
        if (user == null || challenge == null)
            return null;
        
        if (challenge.Shared)
        {           
            var sharedInstance = context.ChallengeInstances.FirstOrDefault(t => t.Challenge.Id == challenge.Id && !t.Destroyed);
            
            if (sharedInstance != null)
            {
                logger.LogInformation("User joining instance - User: ({UserId}, {UserAuthId}, {UserDisplayName}), Instance: ({InstanceId}, {InstanceLoggingInfo}), Challenge: ({ChallengeId}, {ChallengeName}).", 
                    user.Id, user.AuthId, user.DisplayName ?? user.Email, sharedInstance.Id, sharedInstance.LoggingInfo, challenge.Id, challenge.Title); 
                
                sharedInstance.InstanceExpiry = DateTime.UtcNow.AddMinutes(challenge.ExpiryTime);
                context.UserInstances.Add(new UserInstance
                {
                    KillProcessed = false,
                    RequestCreated = DateTime.UtcNow,
                    Instance = sharedInstance,
                    User = user
                });
                await context.SaveChangesAsync();

                return sharedInstance;
            }
        }
        
        logger.LogTrace("Deploying new instance - User: ({UserId}, {UserAuthId}, {UserDisplayName}), Challenge: ({ChallengeId}, {ChallengeName}).", 
            user.Id, user.AuthId, user.DisplayName ?? user.Email, challenge.Id, challenge.Title); 
        
        Guid directory;
        do directory = Guid.NewGuid();
        while(Directory.Exists(_deploymentsPath + directory));
            
        instance = new ChallengeInstance
        {
            Challenge = challenge,
            Destroyed = false,
            InstanceExpiry = DateTime.UtcNow.AddMinutes(challenge.ExpiryTime),
            Host = "",
            LoggingInfo = "",
            DeploymentPath = directory.ToString()
        };
        instance.UserInstances =
        [
            new UserInstance
            {
                KillProcessed = false,
                RequestCreated = DateTime.UtcNow,
                Instance = instance,
                User = user
            }
        ];
        
        context.ChallengeInstances.Add(instance);
        await context.SaveChangesAsync();
        
        var manifestPath = _manifestPath + challenge.DeploymentManifestPath;
        var deploymentPath = _deploymentsPath + directory;

        Dictionary<string, string> outputs;
        try
        {
            Directory.CreateDirectory(deploymentPath);
            outputs = await StartDeployment(deploymentPath, Path.GetRelativePath(deploymentPath, manifestPath));
        }
        catch(Exception e)
        {
            logger.LogError(e, "Instance deployment failed - User: ({UserId}, {UserAuthId}, {UserDisplayName}), Challenge: ({ChallengeId}, {ChallengeName}).", 
                user.Id, user.AuthId, user.DisplayName ?? user.Email, challenge.Id, challenge.Title);
            return null;
        }

        instance.Host = FormatTerraformString(challenge.HostFormat, outputs);
        instance.LoggingInfo = FormatTerraformString(challenge.LoggingInfoFormat, outputs);
        
        logger.LogInformation("New instance deployed - User: ({UserId}, {UserAuthId}, {UserDisplayName}), Instance: ({InstanceId}, {InstanceLoggingInfo}), Challenge: ({ChallengeId}, {ChallengeName}).", 
            user.Id, user.AuthId, user.DisplayName ?? user.Email, instance.Id, instance.LoggingInfo, challenge.Id, challenge.Title); 
        
        await context.SaveChangesAsync();
        return instance;
    }

    public async Task<bool> KillUserInstance(int instanceId, int userId)
    {
        await using var context = await dbFactory.CreateDbContextAsync();
        
        var user = context.Users.FirstOrDefault(t => t.Id == userId);
        var instance = context.ChallengeInstances
            .Include(i => i.Challenge)
            .Include(i => i.UserInstances)
                .ThenInclude(userInstance => userInstance.User)
            .FirstOrDefault(t => t.Id == instanceId);
        
        var userInstance = instance?.UserInstances.FirstOrDefault(t => !t.KillProcessed && t.User.Id == userId);
        if (user == null || userInstance == null)
            return true;

        if (instance!.UserInstances.All(t => t.KillProcessed || t.Id == userInstance.Id))
        {
            logger.LogInformation("User killing instance - User: ({UserId}, {UserAuthId}, {UserDisplayName}), Instance: ({InstanceId}, {InstanceLoggingInfo}), Challenge: ({ChallengeId}, {ChallengeName}).", 
                user.Id, user.AuthId, user.DisplayName ?? user.Email, instance.Id, instance.LoggingInfo, instance.Challenge.Id, instance.Challenge.Title);
            return await KillChallengeInstance(instanceId);
        }
        
        logger.LogInformation("User left instance - User: ({UserId}, {UserAuthId}, {UserDisplayName}), Instance: ({InstanceId}, {InstanceLoggingInfo}), Challenge: ({ChallengeId}, {ChallengeName}).", 
            user.Id, user.AuthId, user.DisplayName ?? user.Email, instance.Id, instance.LoggingInfo, instance.Challenge.Id, instance.Challenge.Title); 
        
        userInstance.KillProcessed = true;
        await context.SaveChangesAsync();
        return true;

    }

    public async Task<bool> KillChallengeInstance(int instanceId)
    {
        await using var context = await dbFactory.CreateDbContextAsync();
        
        var instance = context.ChallengeInstances
            .Include(i => i.Challenge)
            .Include(i => i.UserInstances)
            .FirstOrDefault(t => t.Id == instanceId);
        
        if(instance == null)
            return true;
            
        logger.LogTrace("Killing challenge instance - Instance: ({InstanceId}, {InstanceLoggingInfo}), Challenge: ({ChallengeId}, {ChallengeName}).", 
            instance.Id, instance.LoggingInfo, instance.Challenge.Id, instance.Challenge.Title); 
        
        var directory = instance.DeploymentPath;
        var deploymentPath = _deploymentsPath + directory;
        if (Directory.Exists(deploymentPath))
        {
            try
            {
                await KillDeployment(deploymentPath);
                Directory.Delete(deploymentPath, true);
            }
            catch(Exception e)
            {
                logger.LogError(e, "Instance destruction failed - Instance: ({InstanceId}, {InstanceLoggingInfo}), Challenge: ({ChallengeId}, {ChallengeName}).", 
                    instance.Id, instance.LoggingInfo, instance.Challenge.Id, instance.Challenge.Title);
                return false;
            }
        }

        instance.Destroyed = true;
        foreach (var userInstance in instance.UserInstances)
            userInstance.KillProcessed = true;
        
        await context.SaveChangesAsync();
        
        return true;
    }

    public async Task Clean()
    {
        await using var context = await dbFactory.CreateDbContextAsync();
        var expiredInstances = context.ChallengeInstances.Where(t => t.InstanceExpiry <= DateTime.UtcNow).ToList();
        foreach (var instance in expiredInstances)
            await KillChallengeInstance(instance.Id);

        var deploymentBasePath = new DirectoryInfo(_deploymentsPath);
        var deploymentFullName = deploymentBasePath.FullName + (deploymentBasePath.FullName.EndsWith(Path.DirectorySeparatorChar) ? "" : Path.DirectorySeparatorChar);
        var instanceDeployments = context.ChallengeInstances.Select(t => t.DeploymentPath).Distinct()
            .Select(t => deploymentFullName + t).ToList();
        var deploymentFolders = Directory.GetDirectories(deploymentFullName);
        var orphaned = deploymentFolders.Where(t => !instanceDeployments.Contains(t));
        foreach (var orphan in orphaned)
        {
            try
            {
                var deploymentPath = _deploymentsPath + orphan;
                await KillDeployment(deploymentPath);
                Directory.Delete(deploymentPath, true);
            }
            catch(Exception e)
            {
                logger.LogError(e, "Orphan cleanup failed - Orphan: ({OrphanPath}).", orphan);
            }
        }
    }

    private string FormatTerraformString(string format, Dictionary<string, string> outputs)
    {
        foreach (var output in outputs)
        {
            var value = output.Value.StartsWith('"') && output.Value.EndsWith('"') ?
                output.Value[1..^1] : output.Value;
            format = format.Replace($"$({output.Key})", value);
        }
        return format;
    }

    private async Task<Dictionary<string, string>> StartDeployment(string deploymentPath, string manifestPath)
    {
        var outputText = "";
        var standardError = "";

        logger.LogTrace("Running Terraform init - Deployment path: {DeploymentPath}.", deploymentPath); 
        
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

            logger.LogTrace("Terraform init run - Deployment path: {DeploymentPath}, stdout: {Output}, stderr: {Error}.",
                deploymentPath, outputText, standardError); 

            if (process.ExitCode != 0)
                throw new Exception("Program did not exit correctly.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Terraform init failed - Manifest path: {ManifestPath}, Deployment path: {DeploymentPath}, stdout: {Output}, stderr: {Error}.", 
                manifestPath, deploymentPath, outputText, standardError); 
            throw new InstanceRunnerException("Error initialising instance.", e)
            {
                Data = { { "stdout", outputText }, { "stderr", standardError } }
            };
        }
        
        logger.LogTrace("Running Terraform apply - Deployment path: {DeploymentPath}.", deploymentPath); 
        
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

            logger.LogTrace("Terraform apply run - Deployment path: {DeploymentPath}, stdout: {Output}, stderr: {Error}.",
                deploymentPath, outputText, standardError); 

            if (process.ExitCode != 0)
                throw new Exception("Program did not exit correctly.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Terraform apply failed - Manifest path: {ManifestPath}, Deployment path: {DeploymentPath}, stdout: {Output}, stderr: {Error}.", 
                manifestPath, deploymentPath, outputText, standardError); 
            throw new InstanceRunnerException("Error starting instance.", e)
            {
                Data = { { "stdout", outputText }, { "stderr", standardError } }
            };
        }
        
        logger.LogTrace("Running Terraform output - Deployment path: {DeploymentPath}.", deploymentPath); 
        
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

            logger.LogTrace("Terraform output run - Deployment path: {DeploymentPath}, stdout: {Output}, stderr: {Error}.",
                deploymentPath, outputText, standardError); 

            if (process.ExitCode != 0)
                throw new Exception("Program did not exit correctly.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Terraform output failed - Manifest path: {ManifestPath}, Deployment path: {DeploymentPath}, stdout: {Output}, stderr: {Error}.", 
                manifestPath, deploymentPath, outputText, standardError); 
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
        
        logger.LogTrace("Running Terraform destroy - Deployment path: {DeploymentPath}.", deploymentPath); 
        
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

            logger.LogTrace("Terraform destroy run - Deployment path: {DeploymentPath}, stdout: {Output}, stderr: {Error}.",
                deploymentPath, outputText, standardError); 
            
            if (process.ExitCode != 0)
                throw new Exception("Program did not exit correctly.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Terraform destroy failed - Deployment path: {DeploymentPath}, stdout: {Output}, stderr: {Error}.",
                deploymentPath, outputText, standardError); 
            throw new InstanceRunnerException("Error killing instance.", e)
            {
                Data = { { "stdout", outputText }, { "stderr", standardError } }
            };
        }
    }
}