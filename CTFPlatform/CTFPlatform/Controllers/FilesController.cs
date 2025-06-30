using System.Net.Mime;
using CTFPlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using SystemFile = System.IO.File;

namespace CTFPlatform.Controllers;

[ApiController]
[Route("[controller]")]
public class FilesController(IDbContextFactory<BlazorCtfPlatformContext> dbFactory, IConfiguration config) : ControllerBase
{
    [Authorize(Roles = CtfUser.UserRole)]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetFile(int id)
    {
        await using var context = await dbFactory.CreateDbContextAsync();
        var file = context.Files.FirstOrDefault(f => f.Id == id);
        if (file == null || !SystemFile.Exists(config["UploadDirectory"] + Path.DirectorySeparatorChar + file.StorageLocation))
            return NotFound();
        var systemFile = SystemFile.OpenRead(config["UploadDirectory"] + Path.DirectorySeparatorChar + file.StorageLocation);
        new FileExtensionContentTypeProvider().TryGetContentType(file.Name, out var contentType);

        return File(systemFile, contentType ?? "application/octet-stream", file.Name);
    }
}