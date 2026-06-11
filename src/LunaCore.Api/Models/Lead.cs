namespace LunaCore.Api.Models;

public class Lead
{
    public int Id { get; set; }
    public int NegocioId { get; set; }
    public string Contacto { get; set; } = "";   // teléfono de WhatsApp (o email)
    public string Mensaje { get; set; } = "";     // primer mensaje / consulta
    public string Canal { get; set; } = "whatsapp";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
