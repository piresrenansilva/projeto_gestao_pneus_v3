function field(name, label, type, extra = "") {
  const wide = type === "textarea" ? " full" : "";
  const rand = Math.random().toString(36).slice(2, 8);
  const fakeName = type === "select" ? name : `f_${rand}`;
  const auto = type !== "select" && type !== "textarea"
    ? 'autocomplete="new-password" autocorrect="off" spellcheck="false" data-lpignore="true"'
    : "";
  const control = type === "textarea"
    ? `<textarea name="${fakeName}" data-realname="${name}" ${extra} autocomplete="off"></textarea>`
    : type === "select"
      ? `<select name="${name}" ${extra}></select>`
      : `<input name="${fakeName}" data-realname="${name}" type="${type}" ${auto} ${extra} />`;
  return `<label class="${wide}">${label}${control}</label>`;
}

const FORMS = {
  clientes: () => [
    field("nome", "Nome / Razão social", "text", "required"),
    field("tipo", "Tipo", "select", "required"),
    `<label>CPF / CNPJ<div style="display:flex;gap:6px;align-items:center"><input name="f_doc" data-realname="documento" type="text" autocomplete="new-password" style="flex:1" data-lpignore="true" /><button type="button" class="btn small" id="btnBuscarCnpj">Buscar</button></div></label>`,
    field("telefone", "Telefone", "tel", "placeholder=\"(XX) 9XXXX-XXXX\" maxlength=\"15\""),
    field("email", "E-mail", "email"),
    field("limiteCredito", "Limite de crédito", "text", "class='preco-mask'"),
    `<label>CEP<div style="display:flex;gap:6px;align-items:center"><input name="f_cep" data-realname="cep" type="text" autocomplete="new-password" style="flex:1" data-lpignore="true" /><button type="button" class="btn small" id="btnBuscarCep">Buscar</button></div></label>`,
    field("endereco", "Endereço", "text"),
    field("numero", "Número", "text"),
    field("complemento", "Complemento", "text"),
    field("bairro", "Bairro", "text"),
    field("cidade", "Cidade", "text"),
    field("estado", "Estado", "text", "maxlength='2'"),
    field("status", "Status", "select"),
    field("obs", "Observações", "textarea")
  ].join(""),
  produtos: () => [
    field("produtoId", "Código do produto", "text"), field("nome", "Nome", "text", "required"),
    field("categoria", "Categoria", "text"), field("medida", "Medida", "text"),
    field("unidade", "Unidade (ex.: un, pneu)", "text"), field("preco", "Preço de venda", "text", "required class='preco-mask'"),
    field("custo", "Custo unitário", "text", "required class='preco-mask'"),
        field("status", "Status", "select"),
    field("obs", "Observações", "textarea")
  ].join(""),
  veiculos: () => [
    field("clienteId", "Entidade", "select", "required"),
    field("marca", "Marca", "text"),
    field("modelo", "Modelo", "text", "required"),
    field("ano", "Ano", "text"),
    field("placa", "Placa", "text"),
    field("medidaPneu", "Medida do pneu", "text"),
    field("status", "Status", "select")
  ].join(""),
  servicos: () => [
    field("nome", "Nome do serviço", "text", "required"),
    field("descricao", "Descrição", "text"),
    field("preco", "Preço", "text", "required class='preco-mask'"),
    field("status", "Status", "select")
  ].join(""),
  carcacas: () => [
    field("fogo", "Marca de fogo", "text", "required"),
    field("medida", "Medida", "text", "required"),
    field("dono", "Dono / cliente", "text", "required"),
    field("origem", "Origem", "select"),
    field("qtd", "Quantidade", "number", "required step='1'"),
    field("entrada", "Entrada", "date", "required"),
    field("status", "Status", "select"),
    field("obs", "Observação", "textarea")
  ].join(""),
  estoque: () => [
    field("produtoId", "Código do produto", "text", "required"), field("item", "Descrição", "text", "required"),
    field("medida", "Medida", "text"),
    field("qtd", "Quantidade", "number", "required"), field("minimo", "Mínimo", "number", "required")
  ].join(""),
  producao: () => [
    field("data", "Data", "date", "required"), field("turno", "Turno", "select"),
    field("medida", "Medida", "text", "required"), field("qtd", "Pneus produzidos", "number", "required"),
    field("carcacas", "Carcaças usadas", "number", "required"), field("banda", "Banda", "text"),
    field("operador", "Operador", "select"), field("status", "Status", "select")
  ].join(""),
  cargas: () => [
    field("tipo", "Tipo", "select"), field("data", "Data", "date", "required"),
    field("placa", "Placa", "text", "required"), field("motorista", "Motorista", "text"),
    field("parceiro", "Entidade / fornecedor", "text", "required"), field("nf", "Nota fiscal", "text"),
    field("qtd", "Quantidade", "number", "required"), field("item", "Item", "text", "required"),
    field("status", "Status", "select")
  ].join(""),
  financeiro: () => [
    field("data", "Data", "date", "required"), field("tipo", "Tipo", "select"),
    field("categoria", "Categoria", "select"), field("descricao", "Descrição", "text", "required"),
    field("valor", "Valor", "text", "required class='preco-mask'")
  ].join(""),
  contas: () => [
    field("tipo", "Tipo", "select"), field("pessoa", "Pessoa", "text", "required"),
    field("descricao", "Descrição", "text", "required"), field("valor", "Valor", "text", "required class='preco-mask'"),
    field("vencimento", "Vencimento", "date", "required"), field("boleto", "Nº do boleto", "text"),
    field("status", "Status", "select")
  ].join(""),
  boletos: () => [
    field("origem", "Origem", "select"), field("pessoa", "Pessoa", "text", "required"),
    field("numero", "Número", "text", "required"), field("valor", "Valor", "text", "required class='preco-mask'"),
    field("vencimento", "Vencimento", "date", "required"), field("ref", "Referência", "text"),
    field("status", "Status", "select")
  ].join(""),
  capa: () => [
    field("cliente", "Entidade", "text", "required list='clientesPedido' autocomplete='off'"), field("data", "Data", "date", "required"),
    field("pagamento", "Pagamento", "select"), field("status", "Status", "select"),
    field("vendedor", "Vendedor", "text"), field("previsao", "Previsão", "date"),
    field("obs", "Observação", "textarea")
  ].join("")
};

const SELECTS = {
  clientes: { tipo: ["Cliente", "Fornecedor", "Funcionário"], status: ["Ativo", "Inativo"] },
  veiculos: { status: ["Ativo", "Inativo"] },
  servicos: { status: ["Ativo", "Inativo"] },
  produtos: { status: ["Ativo", "Inativo"] },
  carcacas: {
    origem: ["cliente", "compra", "retorno"],
    status: ["recebida", "inspecionada", "aprovada", "reprovada", "em produção", "pronta"]
  },
  estoque: { tipo: ["novo", "recapado", "insumo", "carcaça"] },
  producao: { turno: ["Manhã", "Tarde", "Noite"], status: ["em andamento", "concluída", "refugo"] },
  cargas: { tipo: ["carga", "descarga"], status: ["aguardando", "conferida", "saiu", "divergente"] },
  financeiro: { tipo: ["entrada", "saída"], categoria: ["venda", "recapagem", "insumo", "folha", "frete", "energia", "imposto", "outros"] },
  contas: { tipo: ["pagar", "receber"], status: ["aberto", "pago", "vencido"] },
  boletos: { origem: [["cliente", "Entidade (a receber)"], ["empresa", "Empresa (a pagar)"]], status: ["aberto", "pago", "vencido", "cancelado"] },
  capa: { pagamento: ["boleto", "pix", "dinheiro", "cartão", "a prazo"], status: ["orçamento", "faturado", "pago", "cancelado"] }
};

function fillSelects(root, kind) {
  const spec = SELECTS[kind] || {};
  root.querySelectorAll("select").forEach((sel) => {
    const options = spec[sel.name] || [];
    const isCliente = sel.name === "clienteId";
    const isOperador = sel.name === "operador";
    let choices = options;
    if (isCliente) {
      choices = db.clientes
        .filter((cliente) => String(cliente.status || "").toLowerCase() !== "inativo")
        .map((cliente) => [cliente.id, `${cliente.nome}${cliente.documento ? ` · ${cliente.documento}` : ""}`]);
    } else if (isOperador) {
      choices = db.clientes
        .filter((c) => String(c.tipo || "").toLowerCase() === "funcionário" && String(c.status || "").toLowerCase() !== "inativo")
        .map((c) => c.nome);
    }
    const dinamico = isCliente || isOperador;
    sel.innerHTML = (dinamico ? `<option value="">Selecione...</option>` : "") + choices.map((o) => Array.isArray(o)
      ? `<option value="${escapeHtml(o[0])}">${escapeHtml(o[1])}</option>`
      : `<option value="${escapeHtml(o)}">${escapeHtml(o)}</option>`).join("");
  });
}

document.getElementById("nav").innerHTML = NAV.map(([id, label]) => `<button data-page="${id}">${label}</button>`).join("");
document.getElementById("nav").onclick = (e) => {
  const b = e.target.closest("button");
  if (!b) return;
  page = b.dataset.page;
  pedidoId = null;
  editingPedidoId = null;
  vendaRascunho = null;
  query = "";
  confirmDelete = "";
  render();
};
document.getElementById("cancelBtn").onclick = () => document.getElementById("modal").classList.remove("show");
document.getElementById("form").onsubmit = async (e) => {
  e.preventDefault();
  const data = {};
  for (const el of e.target.elements) {
    if (!el.name || el.disabled || el.type === "button") continue;
    const key = el.dataset.realname || el.name;
    data[key] = el.value;
  }
  const camposPreco = ["custo", "preco", "valor", "unitario", "limiteCredito"];
  const camposNumero = ["qtd", "minimo", "carcacas"];
  camposPreco.forEach((k) => {
    if (k in data) data[k] = parsePrecoBR(data[k]);
  });
  camposNumero.forEach((k) => {
    if (k in data) data[k] = Number(data[k] || 0);
  });
  try {
    if (editing.id) {
      await api(`/api/${editing.kind}/${editing.id}`, { method: "PUT", body: JSON.stringify(data) });
    } else {
      await api(`/api/${editing.kind}`, { method: "POST", body: JSON.stringify(data) });
    }
    document.getElementById("modal").classList.remove("show");
    await load();
    render();
  } catch (error) {
    toast(`Não foi possível salvar: ${error.message}`, "bad");
  }
};


async function buscarCnpj() {
  const input = document.querySelector('#form input[data-realname="documento"]') || document.querySelector('#form input[name="documento"]');
  if (!input) return;
  let cnpj = (input.value || "").replace(/\D/g, "");
  if (cnpj.length !== 14) {
    toast("Informe um CNPJ válido com 14 dígitos.", "warn");
    return;
  }

  const btn = document.getElementById("btnBuscarCnpj");
  const originalText = btn ? btn.textContent : "Buscar";
  if (btn) { btn.disabled = true; btn.textContent = "Buscando..."; }

  try {
    const token = sessionStorage.getItem("token");
    const headers = token ? { "Authorization": "Bearer " + token } : {};
    const res = await fetch(`/api/consulta/cnpj/${cnpj}`, { headers });
    if (!res.ok) {
      const err = await res.json().catch(() => ({}));
      throw new Error(err.detail || "CNPJ não encontrado");
    }
    const raw = await res.json();

    // Normaliza tanto BrasilAPI quanto ReceitaWS
    function formatarTelefone(num) {
      if (!num) return "";
      const d = String(num).replace(/\D/g, "");
      if (d.length === 10) return `(${d.slice(0,2)}) ${d.slice(2,6)}-${d.slice(6)}`;
      if (d.length === 11) return `(${d.slice(0,2)}) ${d.slice(2,7)}-${d.slice(7)}`;
      return d;
    }

    let tels = [];
    if (raw.ddd_telefone_1) tels.push(raw.ddd_telefone_1);
    if (raw.ddd_telefone_2) tels.push(raw.ddd_telefone_2);
    if (raw.telefone) {
      // ReceitaWS pode vir com um ou mais números juntos ou separados
      const limpo = String(raw.telefone).replace(/\D/g, "");
      if (limpo.length > 11) {
        // tenta separar em blocos de 10 ou 11
        for (let i = 0; i < limpo.length; ) {
          const resto = limpo.length - i;
          const tam = resto >= 11 && resto % 10 !== 0 ? 11 : 10;
          if (resto < 10) break;
          tels.push(limpo.slice(i, i + tam));
          i += tam;
        }
      } else if (limpo) {
        tels.push(limpo);
      }
    }
    tels = [...new Set(tels.map(t => String(t).replace(/\D/g, "")).filter(t => t.length >= 10))];
    const telefoneFormatado = tels.map(formatarTelefone).join(" / ");

    const data = raw.razao_social || raw.nome
      ? {
          razao_social: raw.razao_social || raw.nome,
          nome_fantasia: raw.nome_fantasia || raw.fantasia,
          cep: raw.cep,
          logradouro: raw.logradouro,
          numero: raw.numero,
          complemento: raw.complemento,
          bairro: raw.bairro,
          municipio: raw.municipio,
          uf: raw.uf,
          telefone: telefoneFormatado
        }
      : { ...raw, telefone: telefoneFormatado };

    const form = document.getElementById("form");
    const set = (n, v) => { const el = getField(form, n); if (el && v != null) el.value = v; };
    if (!getField(form, "nome")?.value) set("nome", data.razao_social || data.nome_fantasia || "");
    set("cep", (data.cep || "").replace(/\D/g, ""));
    set("endereco", data.logradouro || "");
    set("numero", data.numero || "");
    set("complemento", data.complemento || "");
    set("bairro", data.bairro || "");
    set("cidade", data.municipio || "");
    set("estado", data.uf || "");
    if (data.telefone && !getField(form, "telefone")?.value) set("telefone", data.telefone);

    toast("Dados preenchidos com sucesso.", "ok");
  } catch (e) {
    toast("Erro ao consultar CNPJ: " + e.message, "bad");
  } finally {
    if (btn) { btn.disabled = false; btn.textContent = originalText; }
  }
}

async function buscarCep(auto = false) {
  const input = document.querySelector('#form input[data-realname="cep"]') || document.querySelector('#form input[name="cep"]');
  if (!input) return;
  let cep = (input.value || "").replace(/\D/g, "");
  if (cep.length !== 8) {
    if (!auto) toast("Informe um CEP válido com 8 dígitos.", "warn");
    return;
  }
  try {
    const token = sessionStorage.getItem("token");
    const headers = token ? { "Authorization": "Bearer " + token } : {};
    const res = await fetch(`/api/consulta/cep/${cep}`, { headers });
    const data = await res.json();
    if (data.erro) throw new Error("CEP não encontrado");
    const form = document.getElementById("form");
    const set = (n, v) => { const el = getField(form, n); if (el && v != null) el.value = v; };
    set("endereco", data.logradouro || "");
    set("bairro", data.bairro || "");
    set("cidade", data.localidade || "");
    set("estado", data.uf || "");
    if (!auto) toast("Endereço preenchido automaticamente.", "ok");
  } catch (e) {
    if (!auto) toast("Erro ao consultar CEP: " + e.message, "bad");
  }
}


function getField(form, realname) {
  return form.querySelector(`[data-realname="${realname}"]`) || form.elements[realname];
}

function openForm(kind, row) {
  // Máscaras no cadastro de cliente
  setTimeout(() => {
    if (kind === "clientes") {
      const tel = document.querySelector('#form input[data-realname="telefone"], #form input[name="f_telefone"]');
      if (tel && typeof aplicarMascaraTelefone === "function") aplicarMascaraTelefone(tel);
      const doc = document.querySelector('#form input[data-realname="documento"], #form input[name="f_doc"]');
      if (doc && typeof aplicarMascaraCpfCnpj === "function") aplicarMascaraCpfCnpj(doc);
    }
  }, 50);

  editing = { kind, id: row?.id || null };
  const titles = { clientes: row ? "Editar entidade" : "Cadastrar entidade", produtos: row ? "Editar produto" : "Cadastrar produto", carcacas: "Carcaça", estoque: "Estoque", producao: "Produção", cargas: "Movimento", financeiro: "Lançamento", contas: "Título", boletos: "Boleto" };
  document.getElementById("modalTitle").textContent = titles[kind] || "Registro";
  document.getElementById("formFields").innerHTML = FORMS[kind]();
  const form = document.getElementById("form");
  fillSelects(form, kind);
  if (kind === "clientes") {
    const btnCnpj = document.getElementById("btnBuscarCnpj");
    if (btnCnpj) btnCnpj.onclick = buscarCnpj;
    const btnCep = document.getElementById("btnBuscarCep");
    if (btnCep) btnCep.onclick = () => buscarCep(false);
    const cepInput = getField(form, "cep");
    if (cepInput) {
      cepInput.addEventListener("input", () => {
        const digits = cepInput.value.replace(/\D/g, "");
        if (digits.length === 8) buscarCep(true);
      });
    }
  }
  if (row) {
    Object.entries(row).forEach(([k, v]) => {
      const el = getField(form, k);
      if (el) el.value = v ?? "";
    });
    // Garante que o select de tipo receba o valor correto
    if (kind === "clientes" && row.tipo) {
      const tipoSel = form.querySelector('select[name="tipo"]');
      if (tipoSel) tipoSel.value = row.tipo;
    }
    // Em registros vinculados, usa a descrição atual do cadastro do produto
    // quando o texto legado da tabela de estoque estiver vazio.
    if (kind === "estoque") {
      // Localiza o produto pelo código textual compartilhado com o estoque.
      const produto = db.produtos.find(p =>
        p.produtoId && row.produtoId && String(p.produtoId).trim().toLowerCase() === String(row.produtoId).trim().toLowerCase()
      );
      if (form.elements.item) form.elements.item.value = produto?.nome || row.item || "";
      if (form.elements.produtoId) form.elements.produtoId.value = row.produtoId || produto?.produtoId || "";
      if (form.elements.medida) form.elements.medida.value = row.medida || produto?.medida || "";
    }
  } else if (form.elements.data) form.elements.data.value = today();
  document.getElementById("modal").classList.add("show");
}

async function removeRow(kind, id) {
  if (!confirm("Deseja excluir este registro?")) return;
  try {
    await api(`/api/${kind}/${id}`, { method: "DELETE" });
    await load();
    render();
  } catch (error) { toast(`Não foi possível excluir: ${error.message}`, "bad"); }
}

function table(kind, heads, map) {
  const q = query.toLowerCase();
  const rows = db[kind].filter((row) => !q || JSON.stringify(row).toLowerCase().includes(q));
  return `<div class="toolbar"><input id="q" placeholder="Filtrar" value="${escapeAttr(query)}" /></div>
    <div class="table-wrap"><table><thead><tr>${heads.map((h) => `<th>${h}</th>`).join("")}<th></th></tr></thead><tbody>
    ${rows.map((row) => `<tr>${map(row).map((c) => `<td>${c}</td>`).join("")}
      <td><button class="btn ghost small" data-edit="${kind}:${row.id}">Editar</button>
      <button class="btn danger small" data-del="${kind}:${row.id}">Excluir</button></td></tr>`).join("") || `<tr><td colspan="${heads.length + 1}">Nenhum registro.</td></tr>`}
    </tbody></table></div>`;
}
function escapeAttr(s) { return String(s).replace(/"/g, "&" + "quot;"); }

