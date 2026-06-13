namespace LunaCore.Api.Models;

public class Producto
{
    public int Id { get; set; }
    public int NegocioId { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public string? ImagenData { get; set; }   // data URL (base64) comprimida en el navegador
    public bool Activo { get; set; } = true;
    public int Orden { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
