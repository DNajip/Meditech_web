using Microsoft.EntityFrameworkCore;
using MediTech.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Ensure appsettings.json is loaded from the correct directory
// This handles cases where VS Code debugger sets CWD to solution root
var projectDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
// When running via 'dotnet run', appsettings.json is in the project directory, not bin
// Try project directory first (where .csproj lives)
var possibleProjectDir = Directory.GetCurrentDirectory();
var appsettingsPath = Path.Combine(possibleProjectDir, "appsettings.json");

if (!File.Exists(appsettingsPath))
{
    // Fallback: walk up from bin/Debug/net10.0 to find the project dir
    var dir = new DirectoryInfo(projectDir);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "appsettings.json")))
    {
        dir = dir.Parent;
    }
    if (dir != null)
    {
        possibleProjectDir = dir.FullName;
    }
}

builder.Configuration
    .SetBasePath(possibleProjectDir)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();


// Add services to the container.
builder.Services.AddControllersWithViews();

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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
