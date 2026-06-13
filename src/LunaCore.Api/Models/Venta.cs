namespace LunaCore.Api.Models;

public class Venta
{
    public int Id { get; set; }
    public int NegocioId { get; set; }
    public decimal Monto { get; set; }
    public string Origen { get; set; } = "manual";   // manual | whatsapp
    public string Estado { get; set; } = "pendiente"; // pendiente | confirmado (pago)
    public string? Nota { get; set; }                 // producto / referencia (opcional)
    // Datos para el rótulo de envío (agencia Shalom, etc.)
    public string? Cliente { get; set; }
    public string? Dni { get; set; }
    public string? Telefono { get; set; }
    public string? Agencia { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
