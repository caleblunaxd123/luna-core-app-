namespace LunaCore.Api.Models;

public class PagoEvento
{
    public int Id { get; set; }
    public string Gateway { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Payload { get; set; } = "";
    public int? NegocioId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
