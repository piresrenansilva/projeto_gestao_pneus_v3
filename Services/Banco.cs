using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

static class Banco
{
    public static async Task Preparar(string cs, string raiz)
    {
        var sb = new SqlConnectionStringBuilder(cs);
        var nome = sb.InitialCatalog;
        sb.InitialCatalog = "master";

        await using (var master = new SqlConnection(sb.ConnectionString))
        {
            await master.OpenAsync();
            await using var criar = new SqlCommand(
                "IF DB_ID(@db) IS NULL BEGIN DECLARE @sql nvarchar(400) = N'CREATE DATABASE ' + QUOTENAME(@db); EXEC(@sql); END", master);
            criar.Parameters.AddWithValue("@db", nome);
            await criar.ExecuteNonQueryAsync();
        }

        var script = await File.ReadAllTextAsync(Path.Combine(raiz, "schema.sql"));
        await using var con = new SqlConnection(cs);
        await con.OpenAsync();
        foreach (var lote in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(lote)) continue;
            await using var cmd = new SqlCommand(lote, con);
            await cmd.ExecuteNonQueryAsync();
        }


        
        // Migração: código automático para serviços
        await using (var migSrv1 = new SqlCommand(@"
            IF OBJECT_ID('dbo.seq_servico', 'SO') IS NULL
                CREATE SEQUENCE dbo.seq_servico START WITH 1 INCREMENT BY 1;
        ", con))
        {
            await migSrv1.ExecuteNonQueryAsync();
        }

        await using (var migSrv2 = new SqlCommand(@"
            IF COL_LENGTH('dbo.servicos', 'codigo') IS NOT NULL
               AND NOT EXISTS (
                   SELECT 1 FROM sys.default_constraints dc
                   JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                   WHERE dc.parent_object_id = OBJECT_ID('dbo.servicos') AND c.name = 'codigo'
               )
            BEGIN
                ALTER TABLE dbo.servicos ADD CONSTRAINT DF_servicos_codigo
                    DEFAULT (N'SRV-' + FORMAT(NEXT VALUE FOR dbo.seq_servico, '0000')) FOR codigo;
            END
        ", con))
        {
            await migSrv2.ExecuteNonQueryAsync();
        }

        // Migração: adicionar codigo sequencial em clientes se não existir
        await using (var migCliente1 = new SqlCommand(@"
            IF OBJECT_ID('dbo.seq_cliente', 'SO') IS NULL
                CREATE SEQUENCE dbo.seq_cliente START WITH 1 INCREMENT BY 1;
            IF COL_LENGTH('dbo.clientes', 'codigo') IS NULL
                ALTER TABLE dbo.clientes ADD codigo nvarchar(20) NULL;", con))
        {
            await migCliente1.ExecuteNonQueryAsync();
        }

        await using (var migCliente2 = new SqlCommand(@"
            IF COL_LENGTH('dbo.clientes', 'codigo') IS NOT NULL
               AND EXISTS (SELECT 1 FROM dbo.clientes WHERE codigo IS NULL)
            BEGIN
                DECLARE @i int = 1;
                DECLARE @id uniqueidentifier;
                DECLARE cur CURSOR FOR SELECT id FROM dbo.clientes WHERE codigo IS NULL ORDER BY criadoEm;
                OPEN cur;
                FETCH NEXT FROM cur INTO @id;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    UPDATE dbo.clientes SET codigo = 'CLI-' + FORMAT(@i, '0000') WHERE id = @id;
                    SET @i = @i + 1;
                    FETCH NEXT FROM cur INTO @id;
                END
                CLOSE cur;
                DEALLOCATE cur;
            END", con))
        {
            await migCliente2.ExecuteNonQueryAsync();
        }

        await using (var migCliente3 = new SqlCommand(@"
            IF COL_LENGTH('dbo.clientes', 'codigo') IS NOT NULL
               AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.clientes') AND name = 'codigo' AND is_nullable = 1)
            BEGIN
                ALTER TABLE dbo.clientes ALTER COLUMN codigo nvarchar(20) NOT NULL;
                DECLARE @max int = ISNULL((SELECT MAX(CAST(SUBSTRING(codigo, 5, 4) AS int)) FROM dbo.clientes WHERE codigo LIKE 'CLI-[0-9][0-9][0-9][0-9]'), 0);
                DECLARE @sql nvarchar(200) = N'ALTER SEQUENCE dbo.seq_cliente RESTART WITH ' + CAST(@max + 1 AS nvarchar(10));
                EXEC(@sql);
                IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID('dbo.clientes') AND name = 'DF_clientes_codigo')
                    ALTER TABLE dbo.clientes ADD CONSTRAINT DF_clientes_codigo DEFAULT (N'CLI-' + FORMAT(NEXT VALUE FOR dbo.seq_cliente, '0000')) FOR codigo;
            END", con))
        {
            await migCliente3.ExecuteNonQueryAsync();
        }

        await using (var colunasProduto = new SqlCommand(@"
            IF COL_LENGTH('dbo.produtos', 'produtoId') IS NULL AND COL_LENGTH('dbo.produtos', 'codigo') IS NOT NULL
                EXEC sp_rename 'dbo.produtos.codigo', 'produtoId', 'COLUMN';
            IF COL_LENGTH('dbo.produtos', 'obs') IS NULL AND COL_LENGTH('dbo.produtos', 'descricao') IS NOT NULL
                EXEC sp_rename 'dbo.produtos.descricao', 'obs', 'COLUMN';", con))
        {
            await colunasProduto.ExecuteNonQueryAsync();
        }

        await using (var colunaItemPedido = new SqlCommand(@"
            IF COL_LENGTH('dbo.pedido_itens', 'produtoId') IS NULL AND COL_LENGTH('dbo.pedido_itens', 'codigo') IS NOT NULL
                EXEC sp_rename 'dbo.pedido_itens.codigo', 'produtoId', 'COLUMN';", con))
        {
            await colunaItemPedido.ExecuteNonQueryAsync();
        }

        await using (var coluna = new SqlCommand(@"
            IF COL_LENGTH('dbo.estoque', 'produtoRef') IS NULL
                ALTER TABLE dbo.estoque ADD produtoRef uniqueidentifier NULL;", con))
        {
            await coluna.ExecuteNonQueryAsync();
        }

        await using (var migrarEstoque = new SqlCommand(@"
            DECLARE @fk sysname;
            DECLARE @sql nvarchar(max);
            SELECT TOP (1) @fk = fk.name
            FROM sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
            WHERE fk.parent_object_id = OBJECT_ID('dbo.estoque')
                AND fk.referenced_object_id = OBJECT_ID('dbo.produtos')
                AND c.name = 'produtoId';

            IF @fk IS NOT NULL
            BEGIN
                SET @sql = N'ALTER TABLE dbo.estoque DROP CONSTRAINT ' + QUOTENAME(@fk);
                EXEC(@sql);
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.estoque') AND name = 'UX_estoque_produtoId')
                    DROP INDEX UX_estoque_produtoId ON dbo.estoque;
                EXEC(N'UPDATE dbo.estoque
                      SET produtoRef = TRY_CONVERT(uniqueidentifier, produtoId)
                      WHERE produtoRef IS NULL AND TRY_CONVERT(uniqueidentifier, produtoId) IS NOT NULL');

                IF COL_LENGTH('dbo.estoque', 'sku') IS NOT NULL
                BEGIN
                    ALTER TABLE dbo.estoque DROP COLUMN produtoId;
                    EXEC sp_rename 'dbo.estoque.sku', 'produtoId', 'COLUMN';
                END
                ELSE
                    EXEC(N'UPDATE e SET produtoId = p.produtoId
                          FROM dbo.estoque e
                          JOIN dbo.produtos p ON p.id = e.produtoRef');
            END
            ELSE IF COL_LENGTH('dbo.estoque', 'sku') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.estoque', 'produtoId') IS NULL
                    EXEC sp_rename 'dbo.estoque.sku', 'produtoId', 'COLUMN';
                ELSE
                BEGIN
                    EXEC(N'UPDATE dbo.estoque
                          SET produtoId = sku
                          WHERE (produtoId IS NULL OR produtoId = N'''') AND sku IS NOT NULL');
                    ALTER TABLE dbo.estoque DROP COLUMN sku;
                END
            END;

            DECLARE @tipoId sysname;
            DECLARE @tamanhoId smallint;
            DECLARE @tipoRef sysname;
            DECLARE @tamanhoRef smallint;
            SELECT @tipoId = t.name, @tamanhoId = c.max_length
            FROM sys.columns c
            JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.produtos') AND c.name = 'id';
            SELECT @tipoRef = t.name, @tamanhoRef = c.max_length
            FROM sys.columns c
            JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID('dbo.estoque') AND c.name = 'produtoRef';
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.estoque') AND name = 'UX_estoque_produtoRef')
                DROP INDEX UX_estoque_produtoRef ON dbo.estoque;

            IF @tipoId IN ('char', 'varchar', 'nchar', 'nvarchar')
                AND (@tipoRef <> @tipoId OR @tamanhoRef <> @tamanhoId)
            BEGIN
                SET @sql = N'ALTER TABLE dbo.estoque ALTER COLUMN produtoRef ' + @tipoId + N'(' +
                    CASE WHEN @tamanhoId = -1 THEN N'MAX'
                         WHEN @tipoId IN ('nchar', 'nvarchar') THEN CONVERT(nvarchar(10), @tamanhoId / 2)
                         ELSE CONVERT(nvarchar(10), @tamanhoId) END + N') NULL';
                EXEC(@sql);
            END
            ELSE IF @tipoId = 'uniqueidentifier' AND @tipoRef <> 'uniqueidentifier'
                ALTER TABLE dbo.estoque ALTER COLUMN produtoRef uniqueidentifier NULL;

            IF COL_LENGTH('dbo.estoque', 'produtoId') IS NULL
                ALTER TABLE dbo.estoque ADD produtoId nvarchar(60) NULL;", con))
        {
            await migrarEstoque.ExecuteNonQueryAsync();
        }

        // Garante o vínculo e cria uma linha de estoque para produtos já cadastrados.
        const string sincronizarEstoque = @"
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_estoque_produtos')
                ALTER TABLE dbo.estoque ADD CONSTRAINT FK_estoque_produtos
                    FOREIGN KEY (produtoRef) REFERENCES dbo.produtos(id) ON DELETE CASCADE;
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.object_id = OBJECT_ID('dbo.estoque') AND i.is_unique = 1 AND c.name = 'produtoRef')
                CREATE UNIQUE INDEX UX_estoque_produtoRef ON dbo.estoque(produtoRef) WHERE produtoRef IS NOT NULL;
            UPDATE e
            SET produtoId = p.produtoId, item = p.nome, medida = p.medida
            FROM dbo.estoque e
            JOIN dbo.produtos p ON p.id = e.produtoRef
            WHERE e.tipo = N'produto';
            INSERT INTO dbo.estoque (produtoRef, produtoId, item, medida, tipo, qtd, minimo)
            SELECT p.id, p.produtoId, p.nome, p.medida, N'produto', 0, 0
            FROM dbo.produtos p
            WHERE NOT EXISTS (SELECT 1 FROM dbo.estoque e WHERE e.produtoRef = p.id);
            ";
        await using var sync = new SqlCommand(sincronizarEstoque, con);
        await sync.ExecuteNonQueryAsync();
    }

    public static async Task<Dictionary<string, List<Dictionary<string, object?>>>> TudoAsync(string cs)
    {
        await using var con = new SqlConnection(cs);
        await con.OpenAsync();

        var r = new Dictionary<string, List<Dictionary<string, object?>>>();
        foreach (var (rota, def) in Esquema.Tabelas)
            if (rota != "itens") r[rota] = await LerAsync(con, def);

        var itens = await LerAsync(con, Esquema.Tabelas["itens"]);
        var porPedido = itens.GroupBy(i => (string)i["pedidoId"]!).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var p in r["pedidos"])
            p["itens"] = porPedido.TryGetValue((string)p["id"]!, out var l) ? l : new List<Dictionary<string, object?>>();
        return r;
    }

    static async Task<List<Dictionary<string, object?>>> LerAsync(SqlConnection con, Def def)
    {
        var extras = (def.Tabela == "clientes" || def.Tabela == "servicos")
            ? new[] { "codigo" }
            : Array.Empty<string>();
        var nomes = string.Join(", ", new[] { "id" }.Concat(def.Cols.Select(c => c.Nome)).Concat(extras).Select(n => $"[{n}]"));
        await using var cmd = new SqlCommand($"SELECT {nomes} FROM dbo.[{def.Tabela}] ORDER BY [criadoEm] DESC", con);
        await using var rd = await cmd.ExecuteReaderAsync();

        var lista = new List<Dictionary<string, object?>>();
        while (await rd.ReadAsync())
        {
            var linha = new Dictionary<string, object?>();
            for (var i = 0; i < rd.FieldCount; i++)
            {
                var v = rd.GetValue(i);
                linha[rd.GetName(i)] = v switch
                {
                    DBNull => "",
                    DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Guid g => g.ToString(),
                    _ => v
                };
            }
            lista.Add(linha);
        }
        return lista;
    }

    public static async Task<object> CriarVendaAsync(string cs, JsonElement corpo, Guid? pedidoExistente = null)
    {
        static string Texto(JsonElement el, string nome) =>
            el.TryGetProperty(nome, out var valor) && valor.ValueKind != JsonValueKind.Null ? valor.ToString().Trim() : "";
        static Guid? GuidOpcional(JsonElement el, string nome) =>
            Guid.TryParse(Texto(el, nome), out var valor) ? valor : null;
        static DateTime? DataOpcional(string valor) =>
            string.IsNullOrWhiteSpace(valor) ? null : DateTime.ParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var clienteId = GuidOpcional(corpo, "clienteId") ?? throw new InvalidOperationException("Selecione um cliente cadastrado.");
        var veiculoId = GuidOpcional(corpo, "veiculoId");
        var data = DataOpcional(Texto(corpo, "data")) ?? DateTime.Today;
        var previsao = DataOpcional(Texto(corpo, "previsao"));
        var agendamento = DataOpcional(Texto(corpo, "agendamento"));
        var pagamento = Texto(corpo, "pagamento");
        var status = pedidoExistente.HasValue ? "orçamento" : Texto(corpo, "status");
        var vendedor = Texto(corpo, "vendedor");
        var observacao = Texto(corpo, "obs");
        var box = Texto(corpo, "box");
        var horario = Texto(corpo, "horario");
        if (string.IsNullOrWhiteSpace(pagamento)) throw new InvalidOperationException("Selecione a forma de pagamento.");
        if (status is not ("orçamento" or "faturado" or "pago"))
            throw new InvalidOperationException("Status de venda inválido.");
        if (!corpo.TryGetProperty("itens", out var itensJson) || itensJson.ValueKind != JsonValueKind.Array || itensJson.GetArrayLength() == 0)
            throw new InvalidOperationException("Adicione ao menos um pneu ou serviço antes de concluir.");

        var percentual = 0m;
        if (corpo.TryGetProperty("descontoPercentual", out var percentualJson) &&
            !decimal.TryParse(percentualJson.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out percentual))
            throw new InvalidOperationException("Percentual de desconto inválido.");
        if (percentual is < 0 or > 100) throw new InvalidOperationException("O desconto deve estar entre 0% e 100%.");

        await using var con = new SqlConnection(cs);
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync();
        if (pedidoExistente is Guid idExistente)
        {
            await using var existenteCmd = new SqlCommand(
                "SELECT status FROM dbo.pedidos WITH (UPDLOCK, HOLDLOCK) WHERE id = @id", con, tx);
            existenteCmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = idExistente;
            var statusExistente = (string?)await existenteCmd.ExecuteScalarAsync()
                ?? throw new KeyNotFoundException("O orçamento não foi encontrado.");
            if (!string.Equals(statusExistente, "orçamento", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Somente orçamentos podem ser editados. Pedidos faturados não podem ser alterados.");
        }

        string nomeCliente;
        await using (var clienteCmd = new SqlCommand(
            "SELECT nome FROM dbo.clientes WHERE id = @id AND status <> N'Inativo'", con, tx))
        {
            clienteCmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = clienteId;
            nomeCliente = (string?)await clienteCmd.ExecuteScalarAsync()
                ?? throw new InvalidOperationException("O cliente selecionado não existe ou está inativo.");
        }

        if (veiculoId is Guid idVeiculo)
        {
            await using var veiculoCmd = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.veiculos WHERE id = @veiculoId AND clienteId = @clienteId AND status <> N'Inativo'", con, tx);
            veiculoCmd.Parameters.Add("@veiculoId", SqlDbType.UniqueIdentifier).Value = idVeiculo;
            veiculoCmd.Parameters.Add("@clienteId", SqlDbType.UniqueIdentifier).Value = clienteId;
            var veiculoExiste = await veiculoCmd.ExecuteScalarAsync();
            if (veiculoExiste is not int quantidadeVeiculos || quantidadeVeiculos == 0)
                throw new InvalidOperationException("O veículo selecionado não pertence ao cliente ou está inativo.");
        }

        var linhas = new List<(string Tipo, Guid? ProdutoRef, Guid? ServicoRef, string Codigo, string Nome, string Medida, int Qtd, decimal Unitario)>();
        var subtotal = 0m;
        foreach (var item in itensJson.EnumerateArray())
        {
            var tipo = Texto(item, "tipo");
            if (!int.TryParse(Texto(item, "qtd"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var qtd) || qtd < 1)
                throw new InvalidOperationException("Cada item deve ter uma quantidade inteira maior que zero.");

            if (tipo == "pneu")
            {
                var produtoRef = GuidOpcional(item, "produtoRef") ?? throw new InvalidOperationException("Um pneu do pedido não tem produto válido.");
                await using var produtoCmd = new SqlCommand(
                    "SELECT produtoId, nome, medida, preco FROM dbo.produtos WHERE id = @id AND status <> N'Inativo'", con, tx);
                produtoCmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = produtoRef;
                await using var rd = await produtoCmd.ExecuteReaderAsync();
                if (!await rd.ReadAsync()) throw new InvalidOperationException("Um dos pneus selecionados não existe ou está inativo.");
                var codigo = rd.IsDBNull(0) ? "" : rd.GetString(0);
                var nome = rd.GetString(1);
                var medida = rd.IsDBNull(2) ? "" : rd.GetString(2);
                var unitario = rd.GetDecimal(3);
                await rd.CloseAsync();
                linhas.Add((tipo, produtoRef, null, codigo, nome, medida, qtd, unitario));
                subtotal += qtd * unitario;
            }
            else if (tipo == "servico")
            {
                var servicoRef = GuidOpcional(item, "servicoRef") ?? throw new InvalidOperationException("Um serviço do pedido não tem cadastro válido.");
                await using var servicoCmd = new SqlCommand(
                    "SELECT codigo, nome, preco FROM dbo.servicos WHERE id = @id AND status <> N'Inativo'", con, tx);
                servicoCmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = servicoRef;
                await using var rd = await servicoCmd.ExecuteReaderAsync();
                if (!await rd.ReadAsync()) throw new InvalidOperationException("Um dos serviços selecionados não existe ou está inativo.");
                var codigo = rd.IsDBNull(0) ? "" : rd.GetString(0);
                var nome = rd.GetString(1);
                var unitario = rd.GetDecimal(2);
                await rd.CloseAsync();
                linhas.Add((tipo, null, servicoRef, codigo, nome, "", qtd, unitario));
                subtotal += qtd * unitario;
            }
            else
            {
                throw new InvalidOperationException("Tipo de item inválido.");
            }
        }

        var descontoValor = decimal.Round(subtotal * percentual / 100m, 2, MidpointRounding.AwayFromZero);
        if (corpo.TryGetProperty("descontoValor", out var descontoJson))
        {
            if (!decimal.TryParse(descontoJson.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out descontoValor))
                throw new InvalidOperationException("Valor do desconto inválido.");
            if (descontoValor < 0 || descontoValor > subtotal)
                throw new InvalidOperationException("O desconto deve estar entre zero e o subtotal.");
            descontoValor = decimal.Round(descontoValor, 2, MidpointRounding.AwayFromZero);
            percentual = subtotal == 0
                ? 0
                : decimal.Round(descontoValor / subtotal * 100m, 2, MidpointRounding.AwayFromZero);
        }
        var numero = "";
        var idPedido = pedidoExistente ?? Guid.Empty;
        var comandoPedido = pedidoExistente.HasValue
            ? @"UPDATE dbo.pedidos
SET data=@data, cliente=@cliente, pagamento=@pagamento, vendedor=@vendedor, previsao=@previsao,
    obs=@obs, clienteId=@clienteId, veiculoId=@veiculoId, descontoPercentual=@percentual,
    descontoValor=@desconto, box=@box, agendamento=@agendamento, horario=@horario
OUTPUT inserted.id, inserted.numero
WHERE id=@id AND status=N'orçamento';"
            : @"INSERT INTO dbo.pedidos
    (numero, data, cliente, pagamento, status, vendedor, previsao, obs, clienteId, veiculoId,
     descontoPercentual, descontoValor, box, agendamento, horario)
OUTPUT inserted.id, inserted.numero
VALUES
    ('PED-' + FORMAT(NEXT VALUE FOR dbo.seq_pedido, '0000'), @data, @cliente, @pagamento, @status,
     @vendedor, @previsao, @obs, @clienteId, @veiculoId, @percentual, @desconto, @box, @agendamento, @horario);";
        await using (var pedidoCmd = new SqlCommand(comandoPedido, con, tx))
        {
            pedidoCmd.Parameters.Add("@data", SqlDbType.Date).Value = data;
            pedidoCmd.Parameters.Add("@cliente", SqlDbType.NVarChar, 150).Value = nomeCliente;
            pedidoCmd.Parameters.Add("@pagamento", SqlDbType.NVarChar, 30).Value = pagamento;
            pedidoCmd.Parameters.Add("@status", SqlDbType.NVarChar, 30).Value = status;
            pedidoCmd.Parameters.Add("@vendedor", SqlDbType.NVarChar, 100).Value = vendedor;
            pedidoCmd.Parameters.Add("@previsao", SqlDbType.Date).Value = previsao is null ? DBNull.Value : previsao.Value;
            pedidoCmd.Parameters.Add("@obs", SqlDbType.NVarChar, 500).Value = observacao;
            pedidoCmd.Parameters.Add("@clienteId", SqlDbType.UniqueIdentifier).Value = clienteId;
            pedidoCmd.Parameters.Add("@veiculoId", SqlDbType.UniqueIdentifier).Value = veiculoId is null ? DBNull.Value : veiculoId.Value;
            var p = pedidoCmd.Parameters.Add("@percentual", SqlDbType.Decimal);
            p.Precision = 5; p.Scale = 2; p.Value = percentual;
            p = pedidoCmd.Parameters.Add("@desconto", SqlDbType.Decimal);
            p.Precision = 12; p.Scale = 2; p.Value = descontoValor;
            pedidoCmd.Parameters.Add("@box", SqlDbType.NVarChar, 100).Value = box;
            pedidoCmd.Parameters.Add("@agendamento", SqlDbType.Date).Value = agendamento is null ? DBNull.Value : agendamento.Value;
            pedidoCmd.Parameters.Add("@horario", SqlDbType.NVarChar, 10).Value = horario;
            if (pedidoExistente is Guid id)
                pedidoCmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            await using var rd = await pedidoCmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync())
                throw new InvalidOperationException("O pedido deixou de ser um orçamento e não pode ser alterado.");
            idPedido = rd.GetGuid(0);
            numero = rd.GetString(1);
        }

        if (pedidoExistente.HasValue)
        {
            await using var removerItens = new SqlCommand("DELETE FROM dbo.pedido_itens WHERE pedidoId = @id", con, tx);
            removerItens.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = idPedido;
            await removerItens.ExecuteNonQueryAsync();
        }

        foreach (var linha in linhas)
        {
            await using var itemCmd = new SqlCommand(@"
INSERT INTO dbo.pedido_itens (pedidoId, produtoId, descricao, medida, qtd, unitario, tipo, produtoRef, servicoRef)
VALUES (@pedidoId, @produtoId, @descricao, @medida, @qtd, @unitario, @tipo, @produtoRef, @servicoRef);", con, tx);
            itemCmd.Parameters.Add("@pedidoId", SqlDbType.UniqueIdentifier).Value = idPedido;
            itemCmd.Parameters.Add("@produtoId", SqlDbType.NVarChar, 50).Value = linha.Codigo;
            itemCmd.Parameters.Add("@descricao", SqlDbType.NVarChar, 200).Value = linha.Nome;
            itemCmd.Parameters.Add("@medida", SqlDbType.NVarChar, 50).Value = linha.Medida;
            itemCmd.Parameters.Add("@qtd", SqlDbType.Int).Value = linha.Qtd;
            var preco = itemCmd.Parameters.Add("@unitario", SqlDbType.Decimal);
            preco.Precision = 12; preco.Scale = 2; preco.Value = linha.Unitario;
            itemCmd.Parameters.Add("@tipo", SqlDbType.NVarChar, 20).Value = linha.Tipo;
            itemCmd.Parameters.Add("@produtoRef", SqlDbType.UniqueIdentifier).Value = linha.ProdutoRef is null ? DBNull.Value : linha.ProdutoRef.Value;
            itemCmd.Parameters.Add("@servicoRef", SqlDbType.UniqueIdentifier).Value = linha.ServicoRef is null ? DBNull.Value : linha.ServicoRef.Value;
            await itemCmd.ExecuteNonQueryAsync();
        }

        if (VendaFaturada(status))
            await AjustarEstoqueVendaAsync(con, tx, idPedido, baixar: true);

        await tx.CommitAsync();
        return new { id = idPedido, numero };
    }

    static bool VendaFaturada(string status) =>
        status.Equals("faturado", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("pago", StringComparison.OrdinalIgnoreCase);

    static async Task AjustarEstoqueVendaAsync(SqlConnection con, SqlTransaction tx, Guid idPedido, bool baixar)
    {
        await using var cmd = new SqlCommand(@"
SELECT produtoRef, SUM(qtd)
FROM dbo.pedido_itens
WHERE pedidoId = @id AND tipo = N'pneu' AND produtoRef IS NOT NULL
GROUP BY produtoRef;", con, tx);
        cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = idPedido;
        var quantidades = new List<(Guid Produto, int Quantidade)>();
        await using (var rd = await cmd.ExecuteReaderAsync())
            while (await rd.ReadAsync()) quantidades.Add((rd.GetGuid(0), rd.GetInt32(1)));

        foreach (var (produto, quantidade) in quantidades)
        {
            await using var ajuste = new SqlCommand(baixar
                ? "UPDATE dbo.estoque SET qtd = qtd - @qtd WHERE produtoRef = @produto AND qtd >= @qtd;"
                : "UPDATE dbo.estoque SET qtd = qtd + @qtd WHERE produtoRef = @produto;", con, tx);
            ajuste.Parameters.Add("@qtd", SqlDbType.Int).Value = quantidade;
            ajuste.Parameters.Add("@produto", SqlDbType.UniqueIdentifier).Value = produto;
            if (await ajuste.ExecuteNonQueryAsync() != 1 && baixar)
                throw new InvalidOperationException("Estoque local insuficiente para um dos pneus. Ajuste o estoque e tente faturar novamente.");
        }
    }

    public static async Task<Guid> CriarAsync(string cs, Def def, JsonElement corpo)
    {
        await using var con = new SqlConnection(cs);
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync();
        await using var cmd = con.CreateCommand();
        cmd.Transaction = tx;

        if (def.Tabela == "pedido_itens" &&
            corpo.TryGetProperty("pedidoId", out var pedidoIdJson) &&
            Guid.TryParse(pedidoIdJson.ToString(), out var pedidoId))
        {
            await using var statusCmd = new SqlCommand(
                "SELECT status FROM dbo.pedidos WITH (UPDLOCK, HOLDLOCK) WHERE id = @id", con, tx);
            statusCmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = pedidoId;
            var status = (string?)await statusCmd.ExecuteScalarAsync();
            if (status is null) throw new KeyNotFoundException("Pedido não encontrado.");
            if (VendaFaturada(status))
                throw new InvalidOperationException("Itens de pedidos faturados não podem ser alterados.");
        }

        var usadas = Parametros(cmd, def.Cols, corpo);
        var nomes = usadas.Select(c => $"[{c.Nome}]").ToList();
        var valores = usadas.Select(c => "@" + c.Nome).ToList();

        if (def.Tabela == "pedidos")
        {
            nomes.Add("[numero]");
            valores.Add("'PED-' + FORMAT(NEXT VALUE FOR dbo.seq_pedido, '0000')");
        }

        cmd.CommandText = $"INSERT INTO dbo.[{def.Tabela}] ({string.Join(", ", nomes)}) OUTPUT inserted.id VALUES ({string.Join(", ", valores)})";
        var id = (Guid)(await cmd.ExecuteScalarAsync())!;

        if (def.Tabela == "produtos")
            await SincronizarEstoqueProdutoAsync(con, tx, id, corpo);

        await tx.CommitAsync();
        return id;
    }

    public static async Task AtualizarAsync(string cs, Def def, Guid id, JsonElement corpo)
    {
        await using var con = new SqlConnection(cs);
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync();
        await using var cmd = con.CreateCommand();
        cmd.Transaction = tx;

        string? statusAnterior = null;
        if (def.Tabela == "pedidos")
        {
            await using var statusCmd = new SqlCommand("SELECT status FROM dbo.pedidos WITH (UPDLOCK, HOLDLOCK) WHERE id = @id", con, tx);
            statusCmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            statusAnterior = (string?)await statusCmd.ExecuteScalarAsync();
            if (statusAnterior is null) throw new KeyNotFoundException("Pedido não encontrado.");
            if (VendaFaturada(statusAnterior) &&
                corpo.EnumerateObject().Any(propriedade => !propriedade.NameEquals("status")))
                throw new InvalidOperationException("Pedidos faturados não podem ser editados.");
            if (VendaFaturada(statusAnterior) &&
                corpo.TryGetProperty("status", out var novoStatusJson) &&
                novoStatusJson.ToString().Equals("orçamento", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Um pedido faturado não pode voltar ao status de orçamento.");
        }
        else if (def.Tabela == "pedido_itens")
        {
            await using var statusCmd = new SqlCommand(@"
SELECT p.status
FROM dbo.pedido_itens i
JOIN dbo.pedidos p WITH (UPDLOCK, HOLDLOCK) ON p.id = i.pedidoId
WHERE i.id = @id;", con, tx);
            statusCmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            var status = (string?)await statusCmd.ExecuteScalarAsync();
            if (status is null) throw new KeyNotFoundException("Item do pedido não encontrado.");
            if (VendaFaturada(status))
                throw new InvalidOperationException("Itens de pedidos faturados não podem ser alterados.");
        }

        var usadas = Parametros(cmd, def.Cols, corpo);
        if (usadas.Count > 0)
        {
            var sets = string.Join(", ", usadas.Select(c => $"[{c.Nome}] = @{c.Nome}"));
            cmd.CommandText = $"UPDATE dbo.[{def.Tabela}] SET {sets} WHERE id = @id";
            cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            await cmd.ExecuteNonQueryAsync();
        }

        if (statusAnterior is not null && corpo.TryGetProperty("status", out var statusJson))
        {
            var statusNovo = statusJson.ToString();
            if (!VendaFaturada(statusAnterior) && VendaFaturada(statusNovo))
                await AjustarEstoqueVendaAsync(con, tx, id, baixar: true);
            else if (VendaFaturada(statusAnterior) && !VendaFaturada(statusNovo))
                await AjustarEstoqueVendaAsync(con, tx, id, baixar: false);
        }

        if (def.Tabela == "produtos")
            await SincronizarEstoqueProdutoAsync(con, tx, id, corpo);

        await tx.CommitAsync();
    }

    public static async Task ExcluirAsync(string cs, Def def, Guid id)
    {
        await using var con = new SqlConnection(cs);
        await con.OpenAsync();
        await using var tx = (SqlTransaction)await con.BeginTransactionAsync();
        if (def.Tabela == "pedidos")
        {
            await using var statusCmd = new SqlCommand("SELECT status FROM dbo.pedidos WITH (UPDLOCK, HOLDLOCK) WHERE id = @id", con, tx);
            statusCmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            if (VendaFaturada((string?)await statusCmd.ExecuteScalarAsync() ?? ""))
                await AjustarEstoqueVendaAsync(con, tx, id, baixar: false);
        }
        else if (def.Tabela == "pedido_itens")
        {
            await using var statusCmd = new SqlCommand(@"
SELECT p.status
FROM dbo.pedido_itens i
JOIN dbo.pedidos p WITH (UPDLOCK, HOLDLOCK) ON p.id = i.pedidoId
WHERE i.id = @id;", con, tx);
            statusCmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            var status = (string?)await statusCmd.ExecuteScalarAsync();
            if (status is null) throw new KeyNotFoundException("Item do pedido não encontrado.");
            if (VendaFaturada(status))
                throw new InvalidOperationException("Itens de pedidos faturados não podem ser alterados.");
        }
        await using var cmd = new SqlCommand($"DELETE FROM dbo.[{def.Tabela}] WHERE id = @id", con, tx);
        cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
        await cmd.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    static async Task SincronizarEstoqueProdutoAsync(SqlConnection con, SqlTransaction tx, Guid id, JsonElement corpo)
    {
        string Valor(string nome) => corpo.TryGetProperty(nome, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "";
        await using var cmd = con.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"
IF EXISTS (SELECT 1 FROM dbo.estoque WHERE produtoRef = @id)
    UPDATE dbo.estoque SET produtoId=@produtoId, item=@nome, medida=@medida WHERE produtoRef=@id;
ELSE
    INSERT INTO dbo.estoque (produtoRef, produtoId, item, medida, tipo, qtd, minimo)
    VALUES (@id, @produtoId, @nome, @medida, N'produto', 0, 0);";
        cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
        cmd.Parameters.Add("@produtoId", SqlDbType.NVarChar, 60).Value = Valor("produtoId");
        cmd.Parameters.Add("@nome", SqlDbType.NVarChar, 200).Value = Valor("nome");
        cmd.Parameters.Add("@medida", SqlDbType.NVarChar, 60).Value = Valor("medida");
        await cmd.ExecuteNonQueryAsync();
    }

    static List<Col> Parametros(SqlCommand cmd, Col[] cols, JsonElement corpo)
    {
        var usadas = new List<Col>();
        foreach (var c in cols)
        {
            if (c.SoLeitura || !corpo.TryGetProperty(c.Nome, out var el)) continue;

            var tipo = c.Tipo switch
            {
                'i' => SqlDbType.Int,
                'd' => SqlDbType.Decimal,
                't' => SqlDbType.Date,
                'g' => SqlDbType.UniqueIdentifier,
                _ => SqlDbType.NVarChar
            };
            var p = cmd.Parameters.Add("@" + c.Nome, tipo);
            if (c.Tipo == 'd') { p.Precision = 12; p.Scale = 2; }
            p.Value = Converter(c, el);
            usadas.Add(c);
        }
        return usadas;
    }

    static object Converter(Col c, JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Null) return DBNull.Value;
        var txt = el.ValueKind == JsonValueKind.String ? el.GetString() ?? "" : el.ToString();
        var vazio = string.IsNullOrWhiteSpace(txt);

        switch (c.Tipo)
        {
            case 'i':
                if (vazio) return 0;
                return (int)Math.Round(decimal.Parse(txt, CultureInfo.InvariantCulture));
            case 'd':
                if (vazio) return 0m;
                return decimal.Parse(txt, CultureInfo.InvariantCulture);
            case 't':
                if (vazio) return DBNull.Value;
                return DateTime.ParseExact(txt, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            case 'g':
                return Guid.Parse(txt);
            default:
                return txt;
        }
    }
}
