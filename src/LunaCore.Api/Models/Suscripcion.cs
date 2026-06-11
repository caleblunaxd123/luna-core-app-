namespace LunaCore.Api.Models;

public class Suscripcion
{
    public int Id { get; set; }
    public int NegocioId { get; set; }
    public int PlanId { get; set; }
    public string Estado { get; set; } = "pending";   // pending | active | past_due | cancelled
    public string Gateway { get; set; } = "mercadopago";
    public string? GatewayRef { get; set; }            // id de la suscripción/pago en la pasarela
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProximaRenovacion { get; set; }
}
