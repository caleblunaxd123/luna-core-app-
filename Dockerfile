# Luna Core API — imagen lista para desplegar en cualquier host (Render, Railway, Azure, Fly, VPS).
# build context = raíz del repo (luna-core-app)

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/LunaCore.Api/LunaCore.Api.csproj ./LunaCore.Api/
RUN dotnet restore ./LunaCore.Api/LunaCore.Api.csproj
COPY src/LunaCore.Api/ ./LunaCore.Api/
RUN dotnet publish ./LunaCore.Api/LunaCore.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# El host suele inyectar PORT; por defecto escuchamos en 8080.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LunaCore.Api.dll"]

# Variables de entorno requeridas en el host (NO se hornean en la imagen):
#   ConnectionStrings__Default   (PostgreSQL — p.ej. Neon: Host=...;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true)
#   GROQ_API_KEY
#   Jwt__Key                     (cadena larga, >=32 chars)
#   MercadoPago__AccessToken     (opcional, para pagos)
#   WhatsApp__VerifyToken, WhatsApp__AccessToken, WhatsApp__PhoneNumberId  (opcional, para WhatsApp)
