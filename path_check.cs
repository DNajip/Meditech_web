
using System;
using System.IO;

var baseDir = AppDomain.CurrentDomain.BaseDirectory;
Console.WriteLine($"BaseDir: {baseDir}");

var directory = new DirectoryInfo(baseDir);
while (directory != null && !File.Exists(Path.Combine(directory.FullName, "appsettings.json")))
{
    directory = directory.Parent;
}

if (directory != null)
{
    var contentRoot = directory.FullName;
    Console.WriteLine($"ContentRoot: {contentRoot}");
    
    var webRootPath = Path.Combine(contentRoot, "Frontend", "wwwroot");
    Console.WriteLine($"WebRootPath: {webRootPath}");
    Console.WriteLine($"WebRootPath Exists: {Directory.Exists(webRootPath)}");
    
    var logoPath = Path.Combine(webRootPath, "images", "logo.png");
    Console.WriteLine($"LogoPath: {logoPath}");
    Console.WriteLine($"Logo Exists: {File.Exists(logoPath)}");
}
else
{
    Console.WriteLine("appsettings.json not found!");
}
