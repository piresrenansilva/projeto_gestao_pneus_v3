using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

static class AuthService
{
    private const int IteracoesHash = 600_000;
    private const int TamanhoSalt = 16;
    private const int TamanhoHash = 32;

    public static async Task<bool> TemUsuarioAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM dbo.logins) THEN 1 ELSE 0 END);", connection);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    public static async Task<bool> CriarPrimeiroUsuarioAsync(string connectionString, string usuario, string senha)
    {
        usuario = usuario.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(usuario) || usuario.Length > 100)
            throw new InvalidOperationException("O usuário deve ter entre 1 e 100 caracteres.");
        if (senha.Length < 12)
            throw new InvalidOperationException("A senha deve ter pelo menos 12 caracteres.");
        if (senha.Length > 256)
            throw new InvalidOperationException("A senha não pode exceder 256 caracteres.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        await using (var lockCommand = new SqlCommand(
            "DECLARE @resultado int; EXEC @resultado = sys.sp_getapplock @Resource = N'FabricaPneus-LoginSetup', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; SELECT @resultado;",
            connection, transaction))
        {
            var lockResult = Convert.ToInt32(await lockCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            if (lockResult < 0)
                throw new InvalidOperationException("A configuração inicial está ocupada. Tente novamente.");
        }

        await using (var existingCommand = new SqlCommand("SELECT COUNT_BIG(1) FROM dbo.logins;", connection, transaction))
        {
            if (Convert.ToInt64(await existingCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture) != 0)
                return false;
        }

        var senhaHash = CriarHashSenha(senha);
        await using (var insertCommand = new SqlCommand(
            "INSERT INTO dbo.logins (usuario, senhaHash) VALUES (@usuario, @senhaHash);",
            connection, transaction))
        {
            insertCommand.Parameters.Add("@usuario", System.Data.SqlDbType.NVarChar, 100).Value = usuario;
            insertCommand.Parameters.Add("@senhaHash", System.Data.SqlDbType.NVarChar, 200).Value = senhaHash;
            await insertCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return true;
    }

    public static async Task<string?> GerarTokenAsync(
        string connectionString,
        string? usuario,
        string? senha,
        IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(usuario) || usuario.Length > 100 || senha is null || senha.Length > 256)
            return null;

        usuario = usuario.Trim();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT senhaHash FROM dbo.logins WHERE usuario = @usuario;",
            connection);
        command.Parameters.Add("@usuario", System.Data.SqlDbType.NVarChar, 100).Value = usuario.ToUpperInvariant();
        var storedHash = await command.ExecuteScalarAsync() as string;

        if (storedHash is null || !VerificarSenha(senha, storedHash))
            return null;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Obrigatorio(config, "Jwt:Key")));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        if (!double.TryParse(config["Jwt:ExpiresMinutes"], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var expiresMinutes) || expiresMinutes <= 0)
            throw new InvalidOperationException("Jwt:ExpiresMinutes deve ser um número positivo.");

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, usuario),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var token = new JwtSecurityToken(
            issuer: Obrigatorio(config, "Jwt:Issuer"),
            audience: Obrigatorio(config, "Jwt:Audience"),
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CriarHashSenha(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, IteracoesHash, HashAlgorithmName.SHA256, TamanhoHash);
        return $"$pbkdf2-sha256${IteracoesHash}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerificarSenha(string senha, string valorArmazenado)
    {
        var partes = valorArmazenado.Split('$');
        if (partes.Length != 5 ||
            partes[1] != "pbkdf2-sha256" ||
            !int.TryParse(partes[2], NumberStyles.None, CultureInfo.InvariantCulture, out var iteracoes) ||
            iteracoes is < 100_000 or > 2_000_000)
            return false;

        try
        {
            var salt = Convert.FromBase64String(partes[3]);
            var hashArmazenado = Convert.FromBase64String(partes[4]);
            if (salt.Length != TamanhoSalt || hashArmazenado.Length != TamanhoHash)
                return false;

            var hashInformado = Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, hashArmazenado.Length);
            return CryptographicOperations.FixedTimeEquals(hashInformado, hashArmazenado);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string Obrigatorio(IConfiguration config, string chave) =>
        !string.IsNullOrWhiteSpace(config[chave])
            ? config[chave]!
            : throw new InvalidOperationException($"Configuração obrigatória ausente: {chave}.");
}
