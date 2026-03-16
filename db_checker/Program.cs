using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MediTech.Models;

var options = new DbContextOptionsBuilder<MediTechContext>()
    .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MediTech;Trusted_Connection=True;TrustServerCertificate=True")
    .Options;

using (var context = new MediTechContext(options))
{
    var productosNegativos = context.Productos.Where(p => p.Stock < 0).ToList();
    Console.WriteLine($"Productos con stock negativo: {productosNegativos.Count}");
    
    foreach (var p in context.Productos.OrderBy(p => p.Nombre).Take(5))
    {
        Console.WriteLine($"ID: {p.IdProducto}, Nombre: {p.Nombre}, Stock: {p.Stock}");
    }
}
