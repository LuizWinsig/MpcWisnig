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
