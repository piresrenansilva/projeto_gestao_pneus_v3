# Fábrica de Pneus — Estrutura Organizada

Sistema de gestão para fábrica/oficina de pneus (cadastro, estoque, produção, vendas, financeiro).

## Como rodar

1. Configure a connection string e as credenciais fora dos arquivos versionados. Para desenvolvimento local, use User Secrets:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:Fabrica" "Server=localhost;Database=FabricaPneus;Integrated Security=True;TrustServerCertificate=True;"
   $jwtKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
   dotnet user-secrets set "Jwt:Key" $jwtKey
   ```

   O projeto já tem `UserSecretsId`; a conexão e a chave ficam no perfil local do Windows e não são publicadas no Git. O `iniciar.bat` ativa o ambiente de desenvolvimento para carregá-las. Para produção, forneça os valores por um gerenciador de segredos ou variáveis de ambiente (`ConnectionStrings__Fabrica`, `Jwt__Key`); nunca os inclua em `appsettings.json` ou no repositório.
2. Execute o `schema.sql` no SQL Server (ou deixe o `Banco.Preparar` criar automaticamente).
3. Rode o projeto. No terminal, habilite o ambiente de desenvolvimento antes de executar `dotnet run`:
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   dotnet run
   ```
   Ou execute `iniciar.bat`, que abre uma instância compatível já em execução; se encontrar uma versão antiga, informa que ela precisa ser encerrada antes de compilar a nova. A saída do servidor fica em `%LOCALAPPDATA%\FabricaPneus\logs`.

## Ambiente

- .NET 8
- SQL Server (Express ou completo). Para desenvolvimento local, prefira Integrated Security (Windows Auth) e `localhost`. Em produção, use TLS com certificado válido e `TrustServerCertificate=False`.
- Para Docker/produção: use container SQL Server e connection string com usuário/senha ou Azure AD.

## Estrutura

Ver `ESTRUTURA.md`.

## Próximos passos recomendados

- Autenticação (JWT ou cookie).
- Modularização adicional do frontend.
- PDF com biblioteca dedicada (QuestPDF).
- Testes de integração.

## PDF
Geração de PDF de pedidos usa QuestPDF. Após clonar, execute `dotnet restore` para baixar o pacote.
Frontend modularizado em js/utils.js, forms.js, vendas.js e app.js.

## Autenticação
- Login simples via JWT.
- No primeiro acesso, a tela pede para criar usuário e senha. A conta fica na tabela `dbo.logins`; a senha é armazenada como hash PBKDF2-SHA256 com salt aleatório, nunca em texto puro. A criação inicial é permitida uma única vez.
- A chave de assinatura JWT deve ser configurada fora do repositório e ter ao menos 32 bytes.
- Token armazenado em `sessionStorage` (removido ao fechar a aba) e enviado no header `Authorization`.
- Endpoints de API (exceto /api/login) exigem autenticação.
- Após `dotnet restore`, faça login na tela inicial.

## Antes de publicar no GitHub
- Se os segredos anteriormente usados já foram enviados ao GitHub, troque a senha e a chave JWT: removê-los do arquivo atual não os apaga do histórico Git.
- Não publique backups do banco, arquivos `.env` ou logs. O `.gitignore` cobre esses arquivos comuns, mas confira os arquivos staged antes de cada push.
- Para um repositório já publicado, remova credenciais do histórico Git usando uma ferramenta de limpeza de histórico e faça force-push coordenado; revogue/rotacione as credenciais primeiro.
