namespace LunaCore.Api.Models;

public class Plan
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public decimal PrecioMensual { get; set; }
    public int LimiteMensajes { get; set; }   // mensajes incluidos por mes
}
