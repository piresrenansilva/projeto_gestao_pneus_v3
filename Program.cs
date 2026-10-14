using System.Text;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["urls"] ?? "http://localhost:5080");

var connectionString = builder.Configuration.GetConnectionString("Fabrica");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure ConnectionStrings:Fabrica com uma variável de ambiente ou User Secrets.");

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Configure Jwt:Key com uma chave aleatória de pelo menos 32 bytes, fora dos arquivos versionados.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Falta Jwt:Issuer na configuração.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Falta Jwt:Audience na configuração.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(5)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddHttpClient<ConsultaExterna>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FabricaPneus/1.0");
});

var app = builder.Build();

try
{
    await Banco.Preparar(connectionString, app.Environment.ContentRootPath);
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Não foi possível preparar o banco de dados.");
    return 1;
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapApiEndpoints(connectionString);

app.Logger.LogInformation("Fábrica de Pneus iniciada.");
app.Run();
return 0;
