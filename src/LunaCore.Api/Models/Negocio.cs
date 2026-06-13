namespace LunaCore.Api.Models;

public class Negocio
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Rubro { get; set; } = "estetica";
    public int PlanId { get; set; } = 1;
    public Plan? Plan { get; set; }
    public string? WhatsappPhoneNumberId { get; set; }   // id del número de WhatsApp Cloud API conectado
    public int StockRestante { get; set; } = 0;          // prendas/productos disponibles (Caja en vivo)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
