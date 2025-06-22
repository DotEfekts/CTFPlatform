namespace CTFPlatform.Utilities;

public static class FileExtensions
{
    public static bool IsSubdirectoryOf(this DirectoryInfo directory, DirectoryInfo parentCheck)
    {
        if (directory.FullName == parentCheck.FullName)
            return true;
        
        var traversalDirectory = new DirectoryInfo(directory.FullName);
        var isParent = false;
        while (traversalDirectory.Parent != null)
        {
            if (traversalDirectory.Parent.FullName == parentCheck.FullName)
            {
                isParent = true;
                break;
            }
            
            traversalDirectory = traversalDirectory.Parent;
        }

        return isParent;
    }
    
    public static void CopyTo(this DirectoryInfo dir, string copyPath, bool recursive = true)
    {
        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        // Cache directories before we start copying
        var dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(copyPath);

        // Get the files in the source directory and copy to the destination directory
        foreach (var file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(copyPath, file.Name);
            file.CopyTo(targetFilePath);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (!recursive) return;
        
        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(copyPath, subDir.Name);
            CopyTo(subDir, newDestinationDir, true);
        }
    }
}