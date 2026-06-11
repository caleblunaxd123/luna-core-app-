# Luna Core — App (producto)

Producto SaaS de Luna IT Solutions: el **Agente de Ventas IA** con cuentas, límites por plan y pagos. Backend **.NET 9** + panel web. (La demo pública anónima vive aparte, en el repo `luna-core` / demo.lunaitsolution.com.)

## Estructura
- `src/LunaCore.Api/` — API ASP.NET Core 9 (auth JWT, EF Core + SQL Server, chat con Groq, pagos).
- `panel/` — panel web (SPA) que consume el API: registro/login, chat del agente, dashboard de uso, planes.
- `src/LunaCore.Api/Migrations/` — migraciones EF (LocalDB en desarrollo).

## Correr en local
**1) Backend** (crea la BD `LunaCore` en LocalDB y la migra solo al iniciar):
```
cd src/LunaCore.Api
dotnet user-secrets set "GROQ_API_KEY" "TU_KEY_DE_GROQ"
# opcional, para activar pagos:
dotnet user-secrets set "MercadoPago:AccessToken" "TU_ACCESS_TOKEN_MP"
dotnet run
```
Queda en `http://localhost:5048` (ver `Properties/launchSettings.json`).

**2) Panel:**
```
cd panel
python -m http.server 8095
```
Abre `http://localhost:8095`. Si el API está en otra URL, cámbiala en el campo "API" del login.

## Endpoints
| Método | Ruta | Auth | Qué hace |
|---|---|---|---|
| POST | `/api/auth/register` | — | crea negocio (plan Free) + devuelve JWT |
| POST | `/api/auth/login` | — | login → JWT |
| GET | `/api/me` | JWT | perfil + plan + uso del mes |
| POST | `/api/chat` | JWT | Agente de Ventas (Groq) con **límite por cuenta** |
| GET | `/api/plans` | — | lista de planes |
| POST | `/api/billing/checkout` | JWT | crea checkout (MercadoPago) → URL de pago |
| POST | `/api/billing/webhook/{gateway}` | — | recibe notificaciones de la pasarela (MercadoPago activa el plan) |
| GET/PUT | `/api/agente` | JWT | ver/configurar el empleado digital (rol + base de conocimiento) |
| GET | `/api/leads` | JWT | leads capturados por WhatsApp |
| PUT | `/api/whatsapp/connect` | JWT | conectar el número (phone_number_id) del negocio |
| GET/POST | `/api/whatsapp/webhook` | — | verificación + recepción de mensajes de WhatsApp (Meta) |

## Planes (sembrados)
Free 50 msj/mes · Starter S/59 1.000 · Growth S/149 5.000 · Pro S/299 20.000.

## Pagos
Abstracción `IPaymentGateway` (`src/LunaCore.Api/Payments/`):
- **MercadoPagoGateway** — funcional; requiere `MercadoPago:AccessToken`. (Hoy usa Checkout Pro; el cobro recurrente real = `preapproval`, se eleva al integrar con credenciales.)
- **StripeGateway / CulqiGateway** — stubs listos para implementar (mismo contrato).
- El webhook registra eventos en `PagoEventos`; la activación de plan tras pago aprobado se finaliza con credenciales reales (leer `external_reference = negocioId:planId`).

## Estado — código completo (v0.4)
- ✅ API + chat (Groq) · auth JWT · EF Core + SQL Server · planes · **límite por cuenta**
- ✅ **Empleado digital configurable**: rol (Ventas/Soporte/Cobranza/RRHH/Admin) + base de conocimiento por negocio
- ✅ Pagos: MercadoPago (con **activación de plan** por webhook) + Stripe/Culqi vía `IPaymentGateway`
- ✅ Panel web: registro/login, configurar agente, chat, dashboard de uso, planes
- ✅ **WhatsApp Cloud API**: webhook (verificación + recepción) → el agente responde y **captura el lead**
- ⏳ Siguiente: memoria de conversación por contacto en WhatsApp · onboarding multi-número (embedded signup) · panel de leads

## 🚀 Puesta en producción (lo que requiere tus cuentas)
El código está completo; para estar EN VIVO:
1. **GitHub + deploy del API:** súbelo y despliega `LunaCore.Api` en Azure App Service / Railway / Render con una base **SQL Server / Azure SQL**. Configura variables: `ConnectionStrings__Default`, `GROQ_API_KEY`, `Jwt__Key`, `MercadoPago__AccessToken`, `WhatsApp__VerifyToken`, `WhatsApp__AccessToken`, `WhatsApp__PhoneNumberId`.
2. **Panel:** despliega `panel/` en Netlify; en el login apunta el campo "API" a la URL pública del backend.
3. **WhatsApp (Meta):** crea una app en developers.facebook.com → producto *WhatsApp* → obtén `AccessToken` y `PhoneNumberId`. En *Configuration → Webhook* pon `https://TU-API/api/whatsapp/webhook` y el *Verify Token* igual a `WhatsApp:VerifyToken`. Suscríbete al campo `messages`. En el panel, conecta el número con `PUT /api/whatsapp/connect`.
4. **Pagos:** pon tu `MercadoPago:AccessToken`; el webhook ya activa el plan al aprobarse el pago.
5. Listo: un cliente paga → sube de plan; un paciente escribe al WhatsApp → el agente responde con la info del negocio y queda el lead.

## Deploy (cuando toque)
- API → Azure App Service / Railway / Render (con SQL Server/Azure SQL y `GROQ_API_KEY` + `MercadoPago:AccessToken` como variables).
- Panel → Netlify (apuntando al dominio del API).
- EF tools fijadas a **9.0.0** en `.config/dotnet-tools.json` (las v10 rompen con net9).
