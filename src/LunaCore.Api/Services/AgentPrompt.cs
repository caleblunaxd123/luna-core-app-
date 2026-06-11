using LunaCore.Api.Models;

namespace LunaCore.Api.Services;

/// <summary>
/// Construye el "cerebro" (system prompt) del empleado digital combinando
/// rol + rubro del negocio + base de conocimiento configurada por el cliente.
/// </summary>
public static class AgentPrompt
{
    static readonly Dictionary<string, string> Rubros = new()
    {
        ["estetica"] = "clínica de estética", ["dental"] = "clínica dental",
        ["inmobiliaria"] = "inmobiliaria", ["gimnasio"] = "gimnasio",
        ["restaurante"] = "restaurante", ["veterinaria"] = "veterinaria",
    };

    static readonly Dictionary<string, string> Roles = new()
    {
        ["ventas"] = "agente de ventas", ["soporte"] = "agente de soporte",
        ["cobranza"] = "agente de cobranza", ["rrhh"] = "agente de RR.HH.",
        ["admin"] = "asistente administrativo",
    };

    public static string DefaultNombre(string rubro) => rubro switch
    {
        "estetica" => "Bella", "dental" => "Sonríe", "inmobiliaria" => "Mateo",
        "gimnasio" => "Fit", "restaurante" => "Sazón", "veterinaria" => "Huellitas",
        _ => "Luna"
    };

    static string Objetivo(string rol, int adelanto) => rol switch
    {
        "ventas" => "Tu objetivo: responde al instante y con calidez, califica al cliente con 1 o 2 preguntas, ofrece 2 horarios concretos y agenda la cita."
                    + (adelanto > 0 ? $" Para asegurar la cita y reducir las inasistencias, pide un adelanto de S/{adelanto} por Yape (se descuenta del total)." : ""),
        "soporte" => "Tu objetivo: resuelve las dudas del cliente usando la información del negocio, con respuestas cortas y claras. Si no tienes el dato o el caso es complejo, ofrece escalar a una persona del equipo.",
        "cobranza" => "Tu objetivo: recuerda pagos pendientes con tono respetuoso y empático, ofrece los medios de pago y registra el compromiso de pago (en qué fecha pagará).",
        "rrhh" => "Tu objetivo: atiende a los candidatos, preselecciona con 2 o 3 preguntas clave del puesto y coordina una entrevista.",
        "admin" => "Tu objetivo: atiende consultas administrativas, toma los datos necesarios y registra o deriva la solicitud.",
        _ => "Tu objetivo: atiende al cliente con calidez y resuelve su consulta."
    };

    public static string Build(Negocio n, AgenteConfig c)
    {
        var rubro = Rubros.GetValueOrDefault(n.Rubro, n.Rubro);
        var rol = Roles.GetValueOrDefault(c.Rol, "asistente");
        var nombre = string.IsNullOrWhiteSpace(c.NombreAgente) ? DefaultNombre(n.Rubro) : c.NombreAgente;

        var parts = new List<string>
        {
            $"Eres {nombre}, {rol} por WhatsApp de \"{n.Nombre}\", un(a) {rubro} en Lima, Perú.",
            Objetivo(c.Rol, c.Adelanto),
        };
        if (!string.IsNullOrWhiteSpace(c.Horarios))
            parts.Add($"Horarios de atención: {c.Horarios}.");
        if (!string.IsNullOrWhiteSpace(c.BaseConocimiento))
            parts.Add("INFORMACIÓN DEL NEGOCIO (úsala como ÚNICA fuente de verdad; si algo no está aquí, dilo con honestidad y ofrece confirmarlo con el equipo):\n" + c.BaseConocimiento);
        parts.Add("REGLAS DE ESTILO: escribe como en WhatsApp, mensajes MUY cortos (1 a 3 frases), tono cercano y peruano, máximo un emoji por mensaje, UNA sola pregunta por turno. No inventes datos. Atiendes 24/7.");
        return string.Join("\n", parts);
    }
}
