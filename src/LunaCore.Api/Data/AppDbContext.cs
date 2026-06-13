using LunaCore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LunaCore.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Negocio> Negocios => Set<Negocio>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<UsoMensual> UsosMensuales => Set<UsoMensual>();
    public DbSet<Suscripcion> Suscripciones => Set<Suscripcion>();
    public DbSet<PagoEvento> PagoEventos => Set<PagoEvento>();
    public DbSet<AgenteConfig> AgentesConfig => Set<AgenteConfig>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<Producto> Productos => Set<Producto>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Negocio>().HasIndex(x => x.Email).IsUnique();
        b.Entity<UsoMensual>().HasIndex(x => new { x.NegocioId, x.Periodo }).IsUnique();
        b.Entity<AgenteConfig>().HasIndex(x => x.NegocioId).IsUnique();
        b.Entity<Plan>().Property(p => p.PrecioMensual).HasColumnType("decimal(10,2)");
        b.Entity<Venta>().Property(v => v.Monto).HasColumnType("decimal(10,2)");
        b.Entity<Venta>().HasIndex(v => new { v.NegocioId, v.CreatedAt });
        b.Entity<Producto>().Property(p => p.Precio).HasColumnType("decimal(10,2)");
        b.Entity<Negocio>().HasIndex(x => x.Slug).IsUnique();

        b.Entity<Plan>().HasData(
            new Plan { Id = 1, Nombre = "Free",    PrecioMensual = 0,   LimiteMensajes = 50 },
            new Plan { Id = 2, Nombre = "Starter", PrecioMensual = 59,  LimiteMensajes = 1000 },
            new Plan { Id = 3, Nombre = "Growth",  PrecioMensual = 149, LimiteMensajes = 5000 },
            new Plan { Id = 4, Nombre = "Pro",     PrecioMensual = 299, LimiteMensajes = 20000 }
        );
    }
}
