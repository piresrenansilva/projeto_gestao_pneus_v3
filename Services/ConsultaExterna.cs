sealed class ConsultaExterna(HttpClient http, ILogger<ConsultaExterna> logger)
{
    public async Task<IResult> ConsultarCnpj(string cnpj)
    {
        cnpj = new string(cnpj.Where(char.IsDigit).ToArray());
        if (cnpj.Length != 14)
            return Results.BadRequest(new { detail = "CNPJ deve ter 14 dígitos." });

        var provedores = new[]
        {
            $"https://brasilapi.com.br/api/cnpj/v1/{cnpj}",
            $"https://www.receitaws.com.br/v1/cnpj/{cnpj}"
        };

        foreach (var url in provedores)
        {
            try
            {
                using var response = await http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    return Results.Content(json, "application/json");
                }
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Falha ao consultar um provedor de CNPJ.");
            }
            catch (TaskCanceledException ex)
            {
                logger.LogWarning(ex, "Tempo esgotado ao consultar um provedor de CNPJ.");
            }
        }

        return Results.NotFound(new { detail = "CNPJ não encontrado." });
    }

    public async Task<IResult> ConsultarCep(string cep)
    {
        cep = new string(cep.Where(char.IsDigit).ToArray());
        if (cep.Length != 8)
            return Results.BadRequest(new { detail = "CEP deve ter 8 dígitos." });

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var response = await http.GetAsync($"https://viacep.com.br/ws/{cep}/json/", timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(timeout.Token);
                return Results.Content(json, "application/json");
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Falha ao consultar o provedor de CEP.");
        }
        catch (TaskCanceledException ex)
        {
            logger.LogWarning(ex, "Tempo esgotado ao consultar o provedor de CEP.");
        }

        return Results.NotFound(new { detail = "CEP não encontrado." });
    }
}
