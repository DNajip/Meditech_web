using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.FileProviders;

// 1. Precise Path Resolution — always use the project root (where Frontend/ lives)
// When launched from VS debugger the DLL runs from bin/Debug/net10.0/,
// so we climb the directory tree to find the true project root.
// NOTE: We look for the "Frontend" directory (NOT appsettings.json) because
// the SDK copies appsettings.json into the bin/ output, which would cause
// the search to stop too early and miss the real project root.
var baseDirectory = AppContext.BaseDirectory;
var contentRoot = baseDirectory;

var dirInfo = new DirectoryInfo(baseDirectory);
while (dirInfo != null)
{
    // The real project root has the Frontend directory AND the .csproj file
    if (Directory.Exists(Path.Combine(dirInfo.FullName, "Frontend"))
        && File.Exists(Path.Combine(dirInfo.FullName, "MediTech.csproj")))
    {
        break;
    }
    dirInfo = dirInfo.Parent;
}

if (dirInfo != null)
    contentRoot = dirInfo.FullName;

// The wwwroot physically lives at <project>/Frontend/wwwroot
var webRootPath = Path.Combine(contentRoot, "Frontend", "wwwroot");

// Fail fast if the folder doesn't exist — this surfaces the real problem immediately
if (!Directory.Exists(webRootPath))
    throw new DirectoryNotFoundException(
        $"WebRoot not found at '{webRootPath}'. " +
        $"ContentRoot resolved to: '{contentRoot}'. " +
        $"BaseDirectory was: '{baseDirectory}'");

// === DIAGNOSTIC OUTPUT (remove after debugging) ===
Console.WriteLine($"=== MediTech Path Diagnostics ===");
Console.WriteLine($"  AppContext.BaseDirectory : {baseDirectory}");
Console.WriteLine($"  ContentRoot              : {contentRoot}");
Console.WriteLine($"  WebRootPath              : {webRootPath}");
Console.WriteLine($"  WebRoot exists           : {Directory.Exists(webRootPath)}");
Console.WriteLine($"  logo.png exists          : {File.Exists(Path.Combine(webRootPath, "images", "logo.png"))}");
Console.WriteLine($"=================================");

// Initialize Builder with explicit paths
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot,
    WebRootPath = webRootPath
});

// Configure Configuration to use the correct ContentRoot
builder.Configuration
    .SetBasePath(contentRoot)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();


// Add services to the container.
builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Clear();
    options.ViewLocationFormats.Add("/Frontend/Views/{1}/{0}.cshtml");
    options.ViewLocationFormats.Add("/Frontend/Views/Shared/{0}.cshtml");
});

var mvcBuilder = builder.Services.AddControllersWithViews();

builder.Services.AddMemoryCache();

if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation(options => {
        var viewsPath = Path.Combine(builder.Environment.ContentRootPath, "Frontend", "Views");
        if (Directory.Exists(viewsPath))
        {
            options.FileProviders.Add(new PhysicalFileProvider(viewsPath));
        }
    });
}

// Database Context — validate connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found. " +
        "Ensure appsettings.json exists in the project directory and contains a valid ConnectionStrings section.");
}

builder.Services.AddDbContext<MediTechContext>(options =>
    options.UseSqlServer(connectionString));

// Custom Services
// Authentication & Session
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(60);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

// Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Administrador"));
    options.AddPolicy("DoctorOnly", policy => policy.RequireRole("Doctor"));
    options.AddPolicy("AsistenteOnly", policy => policy.RequireRole("Asistente"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
// 4. Serving Static Files — use the pre-calculated path, not app.Environment.WebRootPath
// (which can be null when the host hasn't resolved the path correctly)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRootPath),
    RequestPath = ""
});

app.UseRouting();
app.Use(async (context, next) => {
    if (context.Request.Path.Value.Contains("GetFinanzasPaciente")) {
        Console.WriteLine($"[Diagnostic] Request Path: {context.Request.Path}");
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<MediTechContext>();
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
