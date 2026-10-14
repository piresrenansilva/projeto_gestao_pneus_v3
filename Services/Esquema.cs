record Col(string Nome, char Tipo, bool SoLeitura = false);
record Def(string Tabela, Col[] Cols);

static class Esquema
{
    static Col S(string n) => new(n, 's');
    static Col I(string n) => new(n, 'i');
    static Col M(string n) => new(n, 'd');
    static Col T(string n) => new(n, 't');

    // rota da API -> tabela e colunas (os nomes batem com o JSON usado no site)
    public static readonly Dictionary<string, Def> Tabelas = new()
    {
        ["clientes"] = new("clientes", new[] { S("nome"), S("tipo"), S("documento"), S("telefone"), S("email"), M("limiteCredito"), S("cep"), S("endereco"), S("numero"), S("complemento"), S("bairro"), S("cidade"), S("estado"), S("status"), S("obs") }),
        ["veiculos"] = new("veiculos", new[] { new Col("clienteId", 'g'), S("marca"), S("modelo"), S("ano"), S("placa"), S("medidaPneu"), S("status") }),
        ["servicos"] = new("servicos", new[] { S("nome"), S("descricao"), M("preco"), S("status") }),
        ["produtos"] = new("produtos", new[] { S("produtoId"), S("nome"), S("categoria"), S("medida"), S("unidade"), M("preco"), M("custo"), S("obs"), S("status") }),
        ["carcacas"] = new("carcacas", new[] { S("fogo"), S("medida"), S("dono"), S("origem"), I("qtd"), T("entrada"), S("status"), S("obs") }),
        ["estoque"] = new("estoque", new[] { new Col("produtoRef", 'g'), S("produtoId"), S("item"), S("medida"), S("tipo"), I("qtd"), I("minimo"), S("local") }),
        ["producao"] = new("producao", new[] { T("data"), S("turno"), S("medida"), I("qtd"), I("carcacas"), S("banda"), S("operador"), S("status") }),
        ["cargas"] = new("cargas", new[] { S("tipo"), T("data"), S("placa"), S("motorista"), S("parceiro"), S("nf"), I("qtd"), S("item"), S("status") }),
        ["pedidos"] = new("pedidos", new[] { new Col("numero", 's', true), T("data"), S("cliente"), S("pagamento"), S("status"), S("vendedor"), T("previsao"), S("obs"), new Col("clienteId", 'g'), new Col("veiculoId", 'g'), M("descontoPercentual"), M("descontoValor"), S("box"), T("agendamento"), S("horario") }),
        ["itens"] = new("pedido_itens", new[] { new Col("pedidoId", 'g'), S("produtoId"), S("descricao"), S("medida"), I("qtd"), M("unitario"), S("tipo"), new Col("produtoRef", 'g'), new Col("servicoRef", 'g') }),
        ["financeiro"] = new("financeiro", new[] { T("data"), S("tipo"), S("categoria"), S("descricao"), M("valor") }),
        ["contas"] = new("contas", new[] { S("tipo"), S("pessoa"), S("descricao"), M("valor"), T("vencimento"), S("boleto"), S("status") }),
        ["boletos"] = new("boletos", new[] { S("origem"), S("pessoa"), S("numero"), M("valor"), T("vencimento"), S("ref"), S("status") }),
    };
}

