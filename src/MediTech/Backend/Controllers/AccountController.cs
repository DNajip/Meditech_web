using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MediTech.Backend.Models;

namespace MediTech.Backend.Controllers;

public class AccountController : Controller
{
    private readonly MediTechContext _context;

    public AccountController(MediTechContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ViewData["ErrorMessage"] = "Por favor ingrese usuario y contraseña.";
            return View();
        }

        var user = await _context.Usuarios
            .Include(u => u.Role)
            .Include(u => u.Empleado)
                .ThenInclude(e => e!.Persona)
            .Include(u => u.UsuarioModulos)
                .ThenInclude(um => um.Modulo)
            .FirstOrDefaultAsync(u => u.Username == username && u.IdEstado == 1);

        if (user == null || !VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
        {
            ViewData["ErrorMessage"] = "Credenciales inválidas o usuario inactivo.";
            return View();
        }

        var fullName = user.Empleado?.Persona != null 
            ? $"{user.Empleado.Persona.PrimerNombre} {user.Empleado.Persona.PrimerApellido}"
            : user.Username;

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.IdUsuario.ToString()),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Name, user.Username)
        };

        if (user.Role != null)
        {
            claims.Add(new Claim(ClaimTypes.Role, user.Role.DescRol));
        }

        // Agregar claims de módulos permitidos
        if (user.UsuarioModulos != null)
        {
            foreach (var um in user.UsuarioModulos)
            {
                if (um.Modulo != null)
                {
                    claims.Add(new Claim("Module", um.Modulo.Controller));
                }
            }
        }

        var claimsIdentity = new ClaimsIdentity(
            claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, 
            new ClaimsPrincipal(claimsIdentity), 
            authProperties);

        // Session setup
        HttpContext.Session.SetString("UserName", fullName);
        HttpContext.Session.SetInt32("UserId", user.IdUsuario);
        if (user.Role != null)
        {
            HttpContext.Session.SetString("UserRole", user.Role.DescRol);
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    private bool VerifyPassword(string password, byte[]? storedHash, byte[]? storedSalt)
    {
        if (storedHash == null || storedSalt == null)
        {
            // Fallback for migration/plain text if needed, but per-script it should be binary
            // For now, let's assume standard HMACSHA512
            return false; 
        }

        using (var hmac = new System.Security.Cryptography.HMACSHA512(storedSalt))
        {
            var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            for (int i = 0; i < computedHash.Length; i++)
            {
                if (computedHash[i] != storedHash[i]) return false;
            }
        }
        return true;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        return RedirectToAction("Login", "Account");
    }

    public IActionResult AccessDenied()
    {
        return View();
    }
}

