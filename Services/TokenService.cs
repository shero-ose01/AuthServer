using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GameAuth.Models;
using Microsoft.IdentityModel.Tokens;

namespace GameAuth.Services;

public class TokenService{

    private readonly IConfiguration _config;

    public TokenService(IConfiguration config){
        _config = config;
    }

    public string GenerateToken(User user){
        var claims = new[]{
            new Claim(ClaimTypes.NameIdentifier, user.ID.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username),
        };

        SymmetricSecurityKey key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        SigningCredentials creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        JwtSecurityToken token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            signingCredentials: creds,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiryMinutes"]!))
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateToken(int size){
        var bytes = RandomNumberGenerator.GetBytes(size);
        return Convert.ToBase64String(bytes);
    }

    public string GenerateTokenUrl(int size){
        var bytes = RandomNumberGenerator.GetBytes(size);
        return Base64UrlEncoder.Encode(bytes);
    }

    public string HashToken(string token){
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }


}
