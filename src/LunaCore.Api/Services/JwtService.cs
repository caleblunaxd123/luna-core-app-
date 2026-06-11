using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LunaCore.Api.Models;
using Microsoft.IdentityModel.Tokens;

namespace LunaCore.Api.Services;

public class JwtService(IConfiguration cfg)
{
    public string Create(Negocio n)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("sub", n.Id.ToString()),
            new Claim("nombre", n.Nombre),
            new Claim("email", n.Email),
        };
        var token = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"],
            audience: cfg["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
