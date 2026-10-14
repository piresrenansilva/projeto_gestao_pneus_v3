using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using QuestPDF.Drawing.Exceptions;
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
               box = "", horario = "", observacao = "", cep = "", endereco = "",
               bairro = "", cidade = "", estado = "";
        decimal percentual = 0, desconto = 0, subtotal = 0;
        var itens = new List<(string Tipo, string Descricao, string Medida, int Qtd, decimal Unitario)>();

        await using var con = new SqlConnection(cs);
        await con.OpenAsync();

        await using (var cmd = new SqlCommand(@"
SELECT p.numero, COALESCE(c.nome, p.cliente, N''), COALESCE(c.documento, N''), COALESCE(c.telefone, N''),
       CONCAT(COALESCE(v.marca + N' ', N''), COALESCE(v.modelo, N''), CASE WHEN v.placa IS NULL THEN N'' ELSE N' · ' + v.placa END),
       p.data, p.pagamento, p.status, p.vendedor, p.agendamento, p.box, p.horario, p.obs,
       p.descontoPercentual, p.descontoValor,
       COALESCE(c.cep, N''), COALESCE(c.endereco, N''), COALESCE(c.numero, N''), COALESCE(c.bairro, N''), COALESCE(c.cidade, N''), COALESCE(c.estado, N'')
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
            cep = rd.IsDBNull(15) ? "" : rd.GetString(15);
            endereco = rd.IsDBNull(16) ? "" : rd.GetString(16);
            var numeroEndereco = rd.IsDBNull(17) ? "" : rd.GetString(17);
            bairro = rd.IsDBNull(18) ? "" : rd.GetString(18);
            cidade = rd.IsDBNull(19) ? "" : rd.GetString(19);
            estado = rd.IsDBNull(20) ? "" : rd.GetString(20);

            if (!string.IsNullOrWhiteSpace(endereco) && !string.IsNullOrWhiteSpace(numeroEndereco))
                endereco = $"{endereco}, {numeroEndereco}";
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

        try
        {
            var pdf = Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.DefaultTextStyle(style => style.FontFamily("Helvetica").FontSize(9).FontColor("#111"));

                    page.Content().Column(col =>
                    {
                        col.Item().Height(14).Row(top =>
                        {
                            top.ConstantItem(70).Height(14).Background("#01A85A");
                            top.RelativeItem().Height(14).Background("#111111");
                        });

                        col.Item().PaddingHorizontal(53).Column(layout =>
                        {
                            layout.Item().PaddingTop(22).Row(header =>
                            {
                                if (logo is not null)
                                {
                                    header.ConstantItem(253).Height(60).Image(logo).FitArea();
                                }
                                else
                                {
                                    header.ConstantItem(253).Height(60).AlignCenter().AlignMiddle().Text("MS PNEUS").FontSize(22).Bold().FontColor(VerdeEscuro);
                                }

                                header.RelativeItem().AlignRight().Column(contact =>
                                {
                                    contact.Item().Text("46 99110-7935 / 46 99115-2629 ").FontSize(10).Bold().FontColor(VerdeEscuro);
                                    contact.Item().PaddingTop(2).Text("Rua Edgar Faust, 163 · Bairro Padre Ulrico").FontSize(8.5f).FontColor(TextoSuave);
                                    contact.Item().Text("Francisco Beltrão - PR").FontSize(8.5f).FontColor(TextoSuave);
                                    contact.Item().Text("CEP 85604-481").FontSize(8.5f).FontColor(TextoSuave);
                                });
                            });

                            layout.Item().PaddingTop(22).Row(rule =>
                            {
                                rule.ConstantItem(70).Height(2.5f).Background("#111111");
                                rule.RelativeItem().Height(2.5f).Background(Verde);
                            });

                            layout.Item().PaddingTop(16).Row(title =>
                            {
                                title.RelativeItem().AlignLeft().Text("ORÇAMENTO").FontSize(25).Bold().LetterSpacing(0.1f).FontColor("#111111");
                                title.RelativeItem().AlignRight().Column(dateColumn =>
                                {
                                    dateColumn.Item().Text("DATA").FontSize(10).Bold().FontColor(TextoSuave);
                                    dateColumn.Item().PaddingTop(4).Text(string.IsNullOrWhiteSpace(data) ? "____ / ____ / ________" : data).FontSize(12).Bold().FontColor("#111111");
                                });
                            });

                            layout.Item().PaddingTop(22).Background("#F2F8F4").BorderLeft(5).BorderColor(Verde).PaddingVertical(12).PaddingHorizontal(14).Column(card =>
                            {
                                card.Item().Text("DADOS DO CLIENTE").FontSize(8.5f).Bold().LetterSpacing(0.5f).FontColor(VerdeEscuro);
                                card.Item().PaddingTop(12).Row(row =>
                                {
                                    CampoLinha(row, "Nome", cliente, 1f);
                                });
                                card.Item().PaddingTop(12).Row(row =>
                                {
                                    CampoLinha(row, "Endereço", endereco, 3f);
                                    CampoLinha(row, "Fone", telefone, 1.2f);
                                });
                                card.Item().PaddingTop(12).Row(row =>
                                {
                                    CampoLinha(row, "CEP", cep, 1.3f);
                                    CampoLinha(row, "Cidade", cidade, 2.2f);
                                    CampoLinha(row, "UF", estado, 0.9f);
                                });
                            });

                            layout.Item().PaddingTop(24).Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(62);
                                    cols.RelativeColumn(3);
                                    cols.ConstantColumn(92);
                                    cols.ConstantColumn(92);
                                });

                                table.Header(header =>
                                {
                                    CabecalhoTabela(header.Cell(), "QUANT.");
                                    CabecalhoTabela(header.Cell(), "DESCRIÇÃO DOS PRODUTOS / SERVIÇOS");
                                    CabecalhoTabela(header.Cell(), "VALOR UNIT.", true);
                                    CabecalhoTabela(header.Cell(), "VALOR TOTAL", true);
                                });

                                var linhasExibidas = Math.Max(10, itens.Count);
                                for (var i = 0; i < linhasExibidas; i++)
                                {
                                    var item = i < itens.Count ? itens[i] : default;
                                    var fundo = i % 2 == 0 ? "#F2F8F4" : "#FFFFFF";
                                    var quantidade = i < itens.Count ? item.Qtd.ToString(CulturaBrasileira) : "";
                                    var descricao = i < itens.Count ? string.Join(" ", new[] { item.Descricao, item.Medida }.Where(value => !string.IsNullOrWhiteSpace(value))) : "";
                                    var unitario = i < itens.Count ? item.Unitario.ToString("C2", CulturaBrasileira) : "";
                                    var totalItem = i < itens.Count ? (item.Qtd * item.Unitario).ToString("C2", CulturaBrasileira) : "";

                                    CelulaTabela(table.Cell(), quantidade, fundo, false);
                                    CelulaTabela(table.Cell(), descricao, fundo, false);
                                    CelulaTabela(table.Cell(), unitario, fundo, true);
                                    CelulaTabela(table.Cell(), totalItem, fundo, true);
                                }
                            });

                            layout.Item().PaddingTop(16).Row(bottom =>
                            {
                                bottom.RelativeItem().AlignMiddle().Column(left =>
                                {
                                    left.Item().AlignMiddle().Row(seal =>
                                    {
                                        seal.ConstantItem(52).Height(52).Layers(layers =>
{
                                            layers.Layer().Svg("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 74 74'><circle cx='37' cy='37' r='37' fill='#1B8A4B'/></svg>");
                                            layers.PrimaryLayer().AlignCenter().AlignMiddle().Column(c =>
                                            {
                                                c.Item().AlignCenter().Text("6").FontSize(28).Bold().FontColor(Colors.White);
                                                c.Item().AlignCenter().Text("meses").FontSize(8).FontColor(Colors.White);
                                            });
                                        });

                                        seal.RelativeItem().PaddingLeft(12).AlignMiddle().Column(info =>
                                        {
                                            info.Item().Text("GARANTIA").FontSize(14).Bold().FontColor(VerdeEscuro);
                                            info.Item().PaddingTop(2).Text("Pneus remold").FontSize(8.5f).FontColor(TextoSuave);
                                        });
                                    });
                                });

                                bottom.AutoItem().AlignMiddle().Width(230).Column(totalBox =>
                                {
                                    totalBox.Item().Row(row =>
                                    {
                                        row.ConstantItem(90).Height(34).Background("#111111").AlignCenter().AlignMiddle().Text("TOTAL R$").FontSize(12).Bold().FontColor(Colors.White);
                                        row.RelativeItem().Height(34).Background("#F2F8F4").PaddingLeft(12).AlignMiddle().Text(total.ToString("C2", CulturaBrasileira)).FontSize(12).Bold().FontColor("#111111");
                                    });
                                });
                            });

                            layout.Item().PaddingTop(60).Row(sign =>
                            {
                                sign.RelativeItem().AlignCenter().Text("Comprador").FontSize(9).FontColor(TextoSuave);
                                sign.RelativeItem().AlignCenter().Text("Vendedor").FontSize(9).FontColor(TextoSuave);
                            });
                        });
                    });

                    page.Footer().Height(34).Background("#111111").AlignCenter().AlignMiddle().Text("MS PNEUS · REVENDEDOR DE PNEUS · 46 99110.7935 · Francisco Beltrão - PR").FontSize(8.5f).Bold().FontColor(Colors.White);
                });
            }).GeneratePdf();

            return (pdf, $"Pedido-{nomeArquivo}.pdf");
        }
        catch (DocumentLayoutException)
        {
            return GerarPedidoPdfFallback(numero, total, itens, nomeArquivo);
        }
        catch (Exception)
        {
            return GerarPedidoPdfFallback(numero, total, itens, nomeArquivo);
        }
    }

    private static (byte[] Arquivo, string Nome) GerarPedidoPdfFallback(string numero, decimal total, List<(string Tipo, string Descricao, string Medida, int Qtd, decimal Unitario)> itens, string nomeArquivo)
    {
        var pdf = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(style => style.FontFamily("Helvetica").FontSize(10));

                page.Content().Column(col =>
                {
                    col.Item().Text("ORÇAMENTO").FontSize(20).Bold();
                    col.Item().PaddingTop(8).Text($"Pedido: {numero}");
                    col.Item().PaddingTop(6).Text($"Total: {total.ToString("C2", CulturaBrasileira)}");
                    col.Item().PaddingTop(16).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(cell => cell.Background("#111111").Padding(6).Text("Qtd").FontColor(Colors.White).Bold());
                            header.Cell().Element(cell => cell.Background("#111111").Padding(6).Text("Descrição").FontColor(Colors.White).Bold());
                            header.Cell().Element(cell => cell.Background("#111111").Padding(6).Text("Unit.").FontColor(Colors.White).Bold());
                            header.Cell().Element(cell => cell.Background("#111111").Padding(6).Text("Total").FontColor(Colors.White).Bold());
                        });

                        foreach (var item in itens)
                        {
                            var descricao = string.Join(" ", new[] { item.Descricao, item.Medida }.Where(v => !string.IsNullOrWhiteSpace(v)));
                            table.Cell().Element(cell => cell.Padding(6).Text(item.Qtd.ToString(CulturaBrasileira)));
                            table.Cell().Element(cell => cell.Padding(6).Text(descricao));
                            table.Cell().Element(cell => cell.Padding(6).AlignRight().Text(item.Unitario.ToString("C2", CulturaBrasileira)));
                            table.Cell().Element(cell => cell.Padding(6).AlignRight().Text((item.Qtd * item.Unitario).ToString("C2", CulturaBrasileira)));
                        }
                    });
                });
            });
        }).GeneratePdf();

        return (pdf, $"Pedido-{nomeArquivo}.pdf");
    }

    private static void CampoLinha(RowDescriptor row, string titulo, string valor, float proporcao = 1)
    {
        row.RelativeItem(proporcao).Column(field =>
        {
            field.Item().Text($"{titulo}:").FontSize(9).Bold().FontColor(TextoSuave);
            field.Item().PaddingTop(4).Text(string.IsNullOrWhiteSpace(valor) ? "" : valor).FontSize(9).SemiBold().FontColor(TextoEscuro);
            field.Item().PaddingTop(5).Height(1).Background("#8FA89A");
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
