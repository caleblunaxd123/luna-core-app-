namespace LunaCore.Api.Models;

public class UsoMensual
{
    public int Id { get; set; }
    public int NegocioId { get; set; }
    public string Periodo { get; set; } = "";   // formato yyyyMM
    public int Mensajes { get; set; }
}
