using System.Text.Json;

static class ApiEndpoints
{
    public static void MapApiEndpoints(this WebApplication app, string connectionString)
    {
        MapLogin(app, connectionString);

        var api = app.MapGroup("/api").RequireAuthorization();
        MapConsultas(app, api);
        MapPedidos(app, api, connectionString);
        MapCrud(app, api, connectionString);
    }

    private static void MapLogin(WebApplication app, string connectionString)
    {
        app.MapGet("/api/login/status", () => ExecutarAsync(app.Logger, async () =>
            Results.Ok(new { configurado = await AuthService.TemUsuarioAsync(connectionString) })));

        app.MapPost("/api/login/setup", (SetupLoginRequest? request) => ExecutarAsync(app.Logger, async () =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Usuario) || string.IsNullOrEmpty(request.Senha))
                return Results.BadRequest(new { detail = "Informe usuário e senha." });
            if (!string.Equals(request.Senha, request.ConfirmacaoSenha, StringComparison.Ordinal))
                return Results.BadRequest(new { detail = "A confirmação da senha não corresponde." });

            if (!await AuthService.CriarPrimeiroUsuarioAsync(connectionString, request.Usuario, request.Senha))
                return Results.Conflict(new { detail = "O acesso inicial já foi configurado. Entre com seu usuário e senha." });

            var token = await AuthService.GerarTokenAsync(connectionString, request.Usuario, request.Senha, app.Configuration);
            if (token is null)
                throw new InvalidOperationException("Não foi possível iniciar a sessão após configurar o acesso.");

            return Results.Json(new { token, usuario = request.Usuario.Trim() });
        }));

        app.MapPost("/api/login", (LoginRequest? request) => ExecutarAsync(app.Logger, async () =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Usuario) || string.IsNullOrEmpty(request.Senha))
                return Results.BadRequest(new { detail = "Informe usuário e senha." });

            var token = await AuthService.GerarTokenAsync(connectionString, request.Usuario, request.Senha, app.Configuration);
            if (token is null)
                return Results.Unauthorized();

            return Results.Json(new { token, usuario = request.Usuario.Trim() });
        }));
    }

    private static void MapConsultas(WebApplication app, RouteGroupBuilder api)
    {
        api.MapGet("/consulta/cnpj/{cnpj}", (string cnpj, ConsultaExterna consulta) =>
            ExecutarAsync(app.Logger, () => consulta.ConsultarCnpj(cnpj)));

        api.MapGet("/consulta/cep/{cep}", (string cep, ConsultaExterna consulta) =>
            ExecutarAsync(app.Logger, () => consulta.ConsultarCep(cep)));
    }

    private static void MapPedidos(WebApplication app, RouteGroupBuilder api, string connectionString)
    {
        api.MapGet("/dados", () => ExecutarAsync(app.Logger, async () =>
            Results.Json(await Banco.TudoAsync(connectionString))));

        api.MapPost("/vendas", (JsonElement corpo) => ExecutarAsync(app.Logger, async () =>
            Results.Json(await Banco.CriarVendaAsync(connectionString, corpo))));

        api.MapPut("/vendas/{id:guid}", (Guid id, JsonElement corpo) => ExecutarAsync(app.Logger, async () =>
            Results.Json(await Banco.CriarVendaAsync(connectionString, corpo, id))));

        api.MapGet("/pedidos/{id:guid}/pdf", (Guid id) => ExecutarAsync(app.Logger, async () =>
        {
            var caminhoLogo = string.IsNullOrWhiteSpace(app.Environment.WebRootPath)
                ? null
                : Path.Combine(app.Environment.WebRootPath, "fotos", "logo", "logo.PNG");
            var (arquivo, nome) = await PdfService.GerarPedidoPdfAsync(connectionString, id, caminhoLogo);
            return Results.File(arquivo, "application/pdf", nome);
        }));
    }

    private static void MapCrud(WebApplication app, RouteGroupBuilder api, string connectionString)
    {
        foreach (var (rota, definicao) in Esquema.Tabelas)
        {
            api.MapPost($"/{rota}", (JsonElement corpo) => ExecutarAsync(app.Logger, async () =>
                Results.Json(new { id = await Banco.CriarAsync(connectionString, definicao, corpo) })));

            api.MapPut($"/{rota}/{{id:guid}}", (Guid id, JsonElement corpo) => ExecutarAsync(app.Logger, async () =>
            {
                await Banco.AtualizarAsync(connectionString, definicao, id, corpo);
                return Results.NoContent();
            }));

            api.MapDelete($"/{rota}/{{id:guid}}", (Guid id) => ExecutarAsync(app.Logger, async () =>
            {
                await Banco.ExcluirAsync(connectionString, definicao, id);
                return Results.NoContent();
            }));
        }
    }

    private static async Task<IResult> ExecutarAsync(ILogger logger, Func<Task<IResult>> operacao)
    {
        try
        {
            return await operacao();
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao processar requisição da API.");
            return Results.Problem("Erro interno no servidor.");
        }
    }
}

record LoginRequest(string? Usuario, string? Senha);
record SetupLoginRequest(string? Usuario, string? Senha, string? ConfirmacaoSenha);
