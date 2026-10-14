using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

static class PdfService
{
    private const string Verde = "#16834A";
    private const string VerdeEscuro = "#105E37";
    private const string TextoEscuro = "#202923";
    private const string TextoSuave = "#65736A";
    private const string Linha = "#DDE5DF";
    private static readonly CultureInfo CulturaBrasileira = CultureInfo.GetCultureInfo("pt-BR");

    public static async Task<(byte[] Arquivo, string Nome)> GerarPedidoPdfAsync(
        string cs,
        Guid id,
        string? caminhoLogo = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        string numero = "", cliente = "", documento = "", telefone = "", veiculo = "",
               data = "", pagamento = "", status = "", vendedor = "", agendamento = "",
               box = "", horario = "", observacao = "";
        decimal percentual = 0, desconto = 0, subtotal = 0;
        var itens = new List<(string Tipo, string Descricao, string Medida, int Qtd, decimal Unitario)>();

        await using var con = new SqlConnection(cs);
        await con.OpenAsync();

        await using (var cmd = new SqlCommand(@"
SELECT p.numero, COALESCE(c.nome, p.cliente, N''), COALESCE(c.documento, N''), COALESCE(c.telefone, N''),
       CONCAT(COALESCE(v.marca + N' ', N''), COALESCE(v.modelo, N''), CASE WHEN v.placa IS NULL THEN N'' ELSE N' · ' + v.placa END),
       p.data, p.pagamento, p.status, p.vendedor, p.agendamento, p.box, p.horario, p.obs,
       p.descontoPercentual, p.descontoValor
FROM dbo.pedidos p
LEFT JOIN dbo.clientes c ON c.id = p.clienteId
LEFT JOIN dbo.veiculos v ON v.id = p.veiculoId
WHERE p.id = @id;", con))
        {
            cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            await using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync()) throw new KeyNotFoundException("Pedido não encontrado.");
            numero = rd.GetString(0);
            cliente = rd.GetString(1);
            documento = rd.GetString(2);
            telefone = rd.GetString(3);
            veiculo = rd.GetString(4);
            data = rd.IsDBNull(5) ? "" : rd.GetDateTime(5).ToString("dd/MM/yyyy");
            pagamento = rd.IsDBNull(6) ? "" : rd.GetString(6);
            status = rd.IsDBNull(7) ? "" : rd.GetString(7);
            vendedor = rd.IsDBNull(8) ? "" : rd.GetString(8);
            agendamento = rd.IsDBNull(9) ? "" : rd.GetDateTime(9).ToString("dd/MM/yyyy");
            box = rd.IsDBNull(10) ? "" : rd.GetString(10);
            horario = rd.IsDBNull(11) ? "" : rd.GetString(11);
            observacao = rd.IsDBNull(12) ? "" : rd.GetString(12);
            percentual = rd.GetDecimal(13);
            desconto = rd.GetDecimal(14);
        }

        await using (var itensCmd = new SqlCommand(
            "SELECT tipo, descricao, medida, qtd, unitario FROM dbo.pedido_itens WHERE pedidoId = @id ORDER BY criadoEm, id;", con))
        {
            itensCmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
            await using var rd = await itensCmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var tipo = rd.IsDBNull(0) ? "" : rd.GetString(0);
                var descricao = rd.IsDBNull(1) ? "" : rd.GetString(1);
                var medida = rd.IsDBNull(2) ? "" : rd.GetString(2);
                var qtd = rd.GetInt32(3);
                var unitario = rd.GetDecimal(4);
                subtotal += qtd * unitario;
                itens.Add((tipo, descricao, medida, qtd, unitario));
            }
        }

        var total = Math.Max(0, subtotal - desconto);
        var nomeArquivo = Regex.Replace(numero, @"[^A-Za-z0-9_-]", "_");
        var logo = !string.IsNullOrWhiteSpace(caminhoLogo) && File.Exists(caminhoLogo)
            ? await File.ReadAllBytesAsync(caminhoLogo)
            : null;

        var pdf = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(38);
                page.MarginVertical(32);
                page.DefaultTextStyle(style => style.FontSize(9).FontColor(TextoEscuro));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        if (logo is not null)
                            row.ConstantItem(155).Height(52).Image(logo).FitArea();
                        else
                            row.ConstantItem(155).AlignMiddle().Text("FÁBRICA DE PNEUS").FontSize(14).Bold().FontColor(VerdeEscuro);

                        row.RelativeItem().AlignRight().Column(title =>
                        {
                            title.Item().AlignRight().Text("PEDIDO DE VENDA").FontSize(9).Bold().FontColor(TextoSuave).LetterSpacing(1.2f);
                            title.Item().PaddingTop(3).AlignRight().Text(numero).FontSize(20).Bold().FontColor(VerdeEscuro);
                        });
                    });

                    header.Item().PaddingTop(12).Height(3).Background(Verde);
                    header.Item().PaddingTop(7).Row(row =>
                    {
                        row.RelativeItem().Text($"EMITIDO EM {DateTime.Today:dd/MM/yyyy}").FontSize(7).Bold().FontColor(TextoSuave).LetterSpacing(0.6f);
                        row.ConstantItem(120).AlignRight().Text(status.ToUpperInvariant()).FontSize(8).Bold().FontColor(VerdeEscuro);
                    });
                });

                page.Content().PaddingTop(18).Column(content =>
                {
                    content.Spacing(14);

                    content.Item().Background("#F6F8F6").Border(1).BorderColor(Linha).Padding(14).Column(customer =>
                    {
                        customer.Item().Text("DADOS DO CLIENTE").FontSize(8).Bold().FontColor(Verde).LetterSpacing(0.8f);
                        customer.Item().PaddingTop(10).Row(row =>
                        {
                            Campo(row, "Cliente", cliente, 2);
                            Campo(row, "CPF / CNPJ", documento);
                            Campo(row, "Telefone", telefone);
                        });
                        customer.Item().PaddingTop(12).Row(row =>
                        {
                            Campo(row, "Veículo", veiculo, 2);
                            Campo(row, "Vendedor", vendedor);
                        });
                    });

                    content.Item().Row(row =>
                    {
                        CampoEmBloco(row, "Data do pedido", data);
                        CampoEmBloco(row, "Forma de pagamento", pagamento);
                        CampoEmBloco(row, "Agendamento", FormatarAgendamento(agendamento, horario, box));
                    });

                    content.Item().Column(itemsSection =>
                    {
                        itemsSection.Item().PaddingBottom(7).Text("ITENS DO PEDIDO").FontSize(10).Bold().FontColor(VerdeEscuro).LetterSpacing(0.5f);
                        itemsSection.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(34);
                                columns.RelativeColumn();
                                columns.ConstantColumn(58);
                                columns.ConstantColumn(82);
                                columns.ConstantColumn(86);
                            });

                            table.Header(header =>
                            {
                                CabecalhoTabela(header.Cell(), "QTD");
                                CabecalhoTabela(header.Cell(), "DESCRIÇÃO");
                                CabecalhoTabela(header.Cell(), "TIPO");
                                CabecalhoTabela(header.Cell(), "UNITÁRIO", true);
                                CabecalhoTabela(header.Cell(), "TOTAL", true);
                            });

                            for (var index = 0; index < itens.Count; index++)
                            {
                                var item = itens[index];
                                var fundo = index % 2 == 0 ? "#FFFFFF" : "#F7F9F7";
                                var descricao = string.Join(" ", new[] { item.Descricao, item.Medida }.Where(value => !string.IsNullOrWhiteSpace(value)));
                                var tipo = string.Equals(item.Tipo, "servico", StringComparison.OrdinalIgnoreCase) ? "Serviço" : "Pneu";
                                var totalItem = item.Qtd * item.Unitario;

                                CelulaTabela(table.Cell(), item.Qtd.ToString(CulturaBrasileira), fundo);
                                CelulaTabela(table.Cell(), descricao, fundo);
                                CelulaTabela(table.Cell(), tipo, fundo);
                                CelulaTabela(table.Cell(), item.Unitario.ToString("C2", CulturaBrasileira), fundo, true);
                                CelulaTabela(table.Cell(), totalItem.ToString("C2", CulturaBrasileira), fundo, true);
                            }
                        });
                    });

                    content.Item().AlignRight().Width(250).Background("#F6F8F6").Border(1).BorderColor(Linha).Padding(12).Column(totals =>
                    {
                        totals.Spacing(5);
                        LinhaTotal(totals, "Subtotal", subtotal.ToString("C2", CulturaBrasileira));
                        LinhaTotal(totals, $"Desconto ({percentual.ToString("0.##", CulturaBrasileira)}%)", $"- {desconto.ToString("C2", CulturaBrasileira)}");
                        totals.Item().PaddingTop(6).BorderTop(1).BorderColor(Linha).Row(row =>
                        {
                            row.RelativeItem().PaddingTop(7).Text("TOTAL DO PEDIDO").FontSize(9).Bold().FontColor(VerdeEscuro);
                            row.AutoItem().PaddingTop(5).Text(total.ToString("C2", CulturaBrasileira)).FontSize(15).Bold().FontColor(VerdeEscuro);
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(observacao))
                    {
                        content.Item().Background("#F6F8F6").BorderLeft(3).BorderColor(Verde).Padding(11).Column(notes =>
                        {
                            notes.Item().Text("OBSERVAÇÕES").FontSize(8).Bold().FontColor(VerdeEscuro).LetterSpacing(0.5f);
                            notes.Item().PaddingTop(4).Text(observacao).FontSize(9).FontColor(TextoSuave);
                        });
                    }

                    content.Item().PaddingTop(8).Text("Agradecemos pela preferência!").FontSize(10).SemiBold().FontColor(VerdeEscuro);
                });

                page.Footer().PaddingTop(8).BorderTop(1).BorderColor(Linha).Row(footer =>
                {
                    footer.RelativeItem().AlignMiddle().Text("Fábrica de Pneus · Documento gerado pelo sistema").FontSize(7).FontColor(TextoSuave);
                    footer.AutoItem().AlignMiddle().Text(text =>
                    {
                        text.Span("Página ").FontSize(7).FontColor(TextoSuave);
                        text.CurrentPageNumber().FontSize(7).Bold().FontColor(VerdeEscuro);
                        text.Span(" de ").FontSize(7).FontColor(TextoSuave);
                        text.TotalPages().FontSize(7).Bold().FontColor(VerdeEscuro);
                    });
                });
            });
        }).GeneratePdf();

        return (pdf, $"Pedido-{nomeArquivo}.pdf");
    }

    private static void Campo(RowDescriptor row, string titulo, string valor, float proporcao = 1)
    {
        row.RelativeItem(proporcao).Column(field =>
        {
            field.Item().Text(titulo.ToUpperInvariant()).FontSize(7).Bold().FontColor(TextoSuave);
            field.Item().PaddingTop(3).Text(string.IsNullOrWhiteSpace(valor) ? "—" : valor).FontSize(9).SemiBold();
        });
    }

    private static void CampoEmBloco(RowDescriptor row, string titulo, string valor)
    {
        row.RelativeItem().Background("#F6F8F6").Border(1).BorderColor(Linha).Padding(10).Column(field =>
        {
            field.Item().Text(titulo.ToUpperInvariant()).FontSize(7).Bold().FontColor(TextoSuave);
            field.Item().PaddingTop(4).Text(string.IsNullOrWhiteSpace(valor) ? "—" : valor).FontSize(9).SemiBold();
        });
    }

    private static void CabecalhoTabela(IContainer cell, string texto, bool alinharDireita = false)
    {
        var content = cell.Background(VerdeEscuro).PaddingVertical(8).PaddingHorizontal(6);
        if (alinharDireita) content = content.AlignRight();
        content.Text(texto).FontSize(7).Bold().FontColor(Colors.White);
    }

    private static void CelulaTabela(IContainer cell, string texto, string fundo, bool alinharDireita = false)
    {
        var content = cell.Background(fundo).BorderBottom(1).BorderColor(Linha).PaddingVertical(8).PaddingHorizontal(6);
        if (alinharDireita) content = content.AlignRight();
        content.Text(string.IsNullOrWhiteSpace(texto) ? "—" : texto).FontSize(8);
    }

    private static void LinhaTotal(ColumnDescriptor totals, string label, string value)
    {
        totals.Item().Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(8).FontColor(TextoSuave);
            row.AutoItem().Text(value).FontSize(8).SemiBold();
        });
    }

    private static string FormatarAgendamento(string data, string horario, string box)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(data)) partes.Add(data);
        if (!string.IsNullOrWhiteSpace(horario)) partes.Add(horario);
        if (!string.IsNullOrWhiteSpace(box)) partes.Add($"Box {box}");
        return partes.Count == 0 ? "—" : string.Join(" · ", partes);
    }
}
