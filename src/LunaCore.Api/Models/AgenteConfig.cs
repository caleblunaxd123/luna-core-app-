namespace LunaCore.Api.Models;

public class AgenteConfig
{
    public int Id { get; set; }
    public int NegocioId { get; set; }
    public string Rol { get; set; } = "ventas";        // ventas | soporte | cobranza | rrhh | admin
    public string NombreAgente { get; set; } = "";
    public string BaseConocimiento { get; set; } = ""; // info del negocio: servicios, precios, FAQ, políticas
    public string Horarios { get; set; } = "";
    public int Adelanto { get; set; } = 0;             // monto de adelanto/seña (rol ventas)
    public bool Activo { get; set; } = true;
}
