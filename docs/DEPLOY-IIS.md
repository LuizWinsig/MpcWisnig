# Instalação do winsig MCP Server em IIS (Windows Server)

Guia de publicação do MCP server em `https://suporte.winsig.pt/mcp`, como aplicação IIS sob o site existente `suporte.winsig.pt`.

## Pré-requisitos

- Windows Server com IIS instalado e o site `suporte.winsig.pt` já configurado com certificado HTTPS válido.
- Acesso de administrador ao servidor.
- Na máquina de build: .NET 10 SDK.

## 1. Instalar o .NET 10 Hosting Bundle (uma vez, no servidor)

1. Descarregar em <https://dotnet.microsoft.com/download/dotnet/10.0> → secção **ASP.NET Core Runtime** → **Hosting Bundle** (instala o runtime + módulo ANCM para IIS).
2. Executar o instalador.
3. Reiniciar o IIS:

```powershell
iisreset
```

> Nota: instalar o **Hosting Bundle**, não apenas o SDK ou o runtime — só o bundle inclui o módulo AspNetCoreModuleV2 para IIS.

## 2. Publicar a aplicação (na máquina de desenvolvimento)

```powershell
cd C:\Users\Marco_Carapinha.WINSIG\source\repos\winsig.McpServer
dotnet publish winsig.McpServer -c Release -o .\publish --no-self-contained /p:PublishAot=false /p:PublishSingleFile=false /p:PublishSelfContained=false
```

> **Importante:** o projeto tem `PublishAot`, `PublishSingleFile` e `SelfContained` ativos por omissão (para uso stdio/CLI) — **nenhum** é suportado em IIS (erro 500.38). As flags acima desativam tudo e produzem um publish *framework-dependent* portável (`winsig.McpServer.dll` + `web.config`), que corre em qualquer arquitetura com o Hosting Bundle instalado.

O ficheiro `appsettings.Production.json` (incluído no publish) já define:

```json
{
  "Urls": "",
  "McpServerUrl": "https://suporte.winsig.pt/mcp"
}
```

Em IIS o ambiente é `Production` por omissão, portanto estes valores substituem os de desenvolvimento. O `McpServerUrl` é o URL anunciado no OAuth *resource metadata* — tem de corresponder ao endereço público.

## 3. Copiar para o servidor

Copiar o conteúdo da pasta `publish` para o servidor, p. ex.:

```
C:\inetpub\winsig-mcp
```

## 4. Criar a Application Pool e a aplicação no IIS

Executar no servidor (PowerShell **como administrador**):

```powershell
Import-Module WebAdministration

# Application Pool dedicada
New-WebAppPool -Name "winsig-mcp"
Set-ItemProperty "IIS:\AppPools\winsig-mcp" managedRuntimeVersion ""          # No Managed Code
Set-ItemProperty "IIS:\AppPools\winsig-mcp" startMode "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\winsig-mcp" processModel.idleTimeout "00:00:00"  # sem idle timeout (SSE)

# Aplicação /mcp sob o site existente
New-WebApplication -Site "suporte.winsig.pt" -Name "mcp" `
    -PhysicalPath "D:\wwwroot\suporte.winsig.pt\mcp" -ApplicationPool "winsig-mcp"

# Permissões de leitura para a app pool
icacls D:\wwwroot\suporte.winsig.pt\mcp /grant "IIS AppPool\winsig-mcp:(OI)(CI)R"
```

> Se o nome do site no IIS for diferente de `suporte.winsig.pt`, ajustar o parâmetro `-Site`.

## 5. Configurar as credenciais PHC (variáveis de ambiente)

As credenciais **não** vão no `appsettings.json`. Definir como variáveis de ambiente da aplicação IIS (PowerShell como administrador):

```powershell
$appPath = "suporte.winsig.pt/mcp"

$envVars = @(
    @{ name = "Phc__BaseUrl";  value = "https://suporte.winsig.pt/intranet" },
    @{ name = "Phc__User";     value = "Made2webWS" },
    @{ name = "Phc__Password"; value = "m2wWebServ" }
)

foreach ($v in $envVars) {
    Add-WebConfigurationProperty -PSPath "IIS:\" -Location $appPath `
        -Filter "system.webServer/aspNetCore/environmentVariables" `
        -Name "." -Value @{ name = $v.name; value = $v.value }
}
```

Substituir `<UTILIZADOR_PHC>` e `<PASSWORD_PHC>` pelos valores reais.

> Alternativa manual: IIS Manager → aplicação `mcp` → **Configuration Editor** → `system.webServer/aspNetCore` → `environmentVariables`.

### PHC REST API (comercial, utilizadores, contabilidade e health)

Estas tools usam uma API separada do SOAP. O login e os tokens ficam internos ao
servidor MCP; a autenticacao dos utilizadores no Entra permanece igual.

| Tool | Permissao |
|---|---|
| `get_balancete` | `Phc.Read` |
| `get_fluxo_caixa` | `Phc.Read` |
| `get_phc_api_health` | `Phc.Read` |
| `get_encomendas` | `Phc.Read` |
| `get_encomendas_detalhe` | `Phc.Read` |
| `get_utilizadores` | `Phc.Read` |
| `criar_fatura_de_encomendas` | `Phc.Admin` |

A execucao de scripts nao e exposta como tool MCP, incluindo para administradores.

Configurar estas variaveis na aplicacao IIS, sem guardar segredos no repositorio:

| Variavel | Valor |
|---|---|
| `PhcApi__BaseUrl` | `https://suporte.winsig.pt/PhcApi/` |
| `PhcApi__Username` | Utilizador da API |
| `PhcApi__Password` | Password da API |
| `PhcApi__ApiKey` | Opcional, enviada como `X-API-Key` se configurada |
| `PhcApi__TimeoutSeconds` | `100` por omissao |

Em desenvolvimento, usar as mesmas chaves em user-secrets com `:` em vez de `__`.
O URL base e a raiz `/PhcApi/`, **sem `/api/` no fim**: o cliente acrescenta
`login`, `health`, `api/contabilidade/...`, `api/comercial/...` ou `api/utilizadores`.
O login deve devolver `token` e `expiresAtUtc`; o token e reutilizado em memoria
e renovado antes de expirar. HTTP 401 invalida a cache para a proxima chamada.
Nao ha repeticao automatica de pedidos.

`ano` e `mes` sao opcionais e, quando omitidos, nao sao enviados.
As respostas de contabilidade sao JSON sem DTO de negocio, pois o OpenAPI nao
descreve os seus campos; `health` tambem aceita `text/plain`.
Comercial e utilizadores devolvem DTOs tipados no envelope `error`, `msg`, `data`;
`error=true` e convertido num erro da tool.

`get_encomendas` aceita `orderNumber`, `year` (1-9999), `clientNumber`, `clientName`,
`startDate`, `endDate` e `isClosed`, todos opcionais. `get_encomendas_detalhe`
aceita os mesmos filtros, mas com `orderNumbers` (array) em vez de `orderNumber`.
Datas sao strings encaminhadas para a API; `isClosed=false` e enviado explicitamente.
O array e enviado com chaves repetidas (`orderNumbers=1&orderNumbers=2`).
`get_utilizadores` usa REST; a tool existente `get_users` continua a usar SOAP.

`criar_fatura_de_encomendas` recebe `options` com `orders` (pelo menos uma encomenda),
`getPDFPrintOut` (0 ou 1, omissao 0) e `closeOrder` (omissao false).
Cada encomenda requer `OrderID` e pode conter `OrderLineID` (array de strings).
Usar os identificadores `orderId`/`lineId` devolvidos pelas consultas, nao o numero
da encomenda. O cliente envia `{ "options": { ... } }` para a API.
A operacao cria faturas reais e pode fechar encomendas: obter confirmacao explicita
do utilizador. As anotacoes MCP assinalam escrita nao idempotente; nao substituem
as permissoes no servidor. Nao repetir apos timeout/erro sem confirmar no PHC se
a fatura ja foi criada. Nao ha chamadas de faturacao real nos testes.

Se o login funcionar mas os restantes endpoints devolverem 401, confirmar os
requisitos de `X-API-Key` e as permissoes/configuracao do token na PhcApi.
As credenciais locais de user-secrets nao sao publicadas para o IIS.

## 6. Verificar a instalação

```powershell
curl.exe -i https://suporte.winsig.pt/mcp
```
ou
``` 
try {
    Invoke-WebRequest -Uri "https://suporte.winsig.pt/mcp" -Method Post `
        -ContentType "application/json" `
        -Headers @{ Accept = "application/json, text/event-stream" } `
        -Body '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}' `
        -UseBasicParsing
} catch {
    $r = $_.Exception.Response
    "Status: $([int]$r.StatusCode)"
    "WWW-Authenticate: $($r.Headers['WWW-Authenticate'])"
}
```

Resultado esperado: **HTTP 401** com header `WWW-Authenticate` a apontar para o resource metadata OAuth. Isto confirma que a app está a correr e a autenticação Entra ID está ativa.

Teste completo do metadata:

```powershell
curl.exe -s https://suporte.winsig.pt/mcp/.well-known/oauth-protected-resource
```
ou
```
Invoke-RestMethod -Uri "https://suporte.winsig.pt/mcp/.well-known/oauth-protected-resource" -UseBasicParsing
```

Deve devolver JSON com `resource: "https://suporte.winsig.pt/mcp"` e o authorization server do tenant Entra.

## 7. Atualizações futuras

```powershell
# na máquina de dev
dotnet publish winsig.McpServer -c Release -o .\publish --no-self-contained /p:PublishAot=false /p:PublishSingleFile=false /p:PublishSelfContained=false

# no servidor (parar a app, copiar, reiniciar)
Stop-WebAppPool -Name "winsig-mcp"
# ... copiar ficheiros para C:\inetpub\winsig-mcp ...
Start-WebAppPool -Name "winsig-mcp"
```

## Resolução de problemas

| Sintoma | Causa provável | Solução |
|---|---|---|
| 500.30 / 500.31 | Hosting Bundle em falta ou versão errada | Reinstalar .NET 10 Hosting Bundle + `iisreset` |
| 500 e evento "BaseUrl not configured" | Variáveis `Phc__*` não definidas | Rever passo 5 |
| 401 nunca aparece (403/404) | App não arrancou ou path errado | Ver Event Viewer → Windows Logs → Application |
| Login OAuth falha no cliente | `McpServerUrl` diferente do URL público | Confirmar `appsettings.Production.json` |
| Ligações caem ao fim de tempo | Idle timeout da app pool | Confirmar `idleTimeout = 00:00:00` e `startMode = AlwaysRunning` |

## Configuração Entra ID (referência)

- **Tenant:** `918efd35-3da7-43a2-b2ec-7e453e8ed341`
- **App API (winsig-mcp-server):** `a77aca82-2b88-4dca-8537-7534ac3f7bbe` — expõe `api://.../access_as_user`, roles `Phc.Read` e `Phc.ReadDetail`
- **App cliente (winsig-mcp-client):** `9c0c29df-333b-4dc2-812c-bd6bb68841d5` — public client, redirect URIs `http://localhost:40768/oauth/callback` e `http://127.0.0.1:40768/oauth/callback`

Nenhuma alteração no Entra é necessária ao mudar o servidor de alojamento — os redirect URIs são locais à máquina de cada utilizador.

## Atribuir acesso a utilizadores

1. <https://portal.azure.com> → Entra ID → **Enterprise Applications** → `winsig-mcp-server` → **Users and groups** → **Add user/group**.
2. Selecionar o utilizador (ou grupo de segurança) e a role: `Phc.Read` e/ou `Phc.ReadDetail`.
3. O utilizador instala o conector seguindo `docs\INSTALACAO-UTILIZADOR.md`.
