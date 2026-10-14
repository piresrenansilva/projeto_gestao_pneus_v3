# Estrutura Organizada

## Frontend
- wwwroot/
  - index.html
  - styles.css
  - js/
    - utils.js     # Helpers, api(), load, máscaras, NAV, toast
    - forms.js     # Formulários, field, openForm, CNPJ/CEP
    - vendas.js    # Fluxo de vendas em etapas, PDF client-side
    - app.js       # render(), views das páginas, autenticação
  - fotos/

## Backend
- Program.cs
- Endpoints/ApiEndpoints.cs # Mapeamento das rotas da API
- Services/ (Esquema, Banco, ConsultaExterna, PdfService, AuthService)
- schema.sql cria dbo.logins para o acesso inicial (senha armazenada com hash)
- schema.sql
- appsettings.json
- FabricaPneus.csproj

## Observações
- Frontend modularizado (scripts carregados em ordem com defer).
- Autenticação JWT com sessionStorage (some ao fechar o navegador).
- PDF com QuestPDF.

- Login e demais interações do frontend usam JavaScript vanilla, sem dependência de CDN.
