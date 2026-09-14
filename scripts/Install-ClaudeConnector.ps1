# Instala o conector "winsig-phc" no Claude Desktop do utilizador atual.
# Uso: powershell -ExecutionPolicy Bypass -File .\Install-ClaudeConnector.ps1
param(
    [string]$ServerUrl = "https://suporte.winsig.pt/mcp",
    [string]$ClientId  = "9c0c29df-333b-4dc2-812c-bd6bb68841d5",
    [string]$Scope     = "api://a77aca82-2b88-4dca-8537-7534ac3f7bbe/access_as_user",
    [string]$Name      = "winsig-phc"
)

$ErrorActionPreference = "Stop"

# 1. Verificar Node.js / npx
$npx = Get-Command npx.cmd -ErrorAction SilentlyContinue
if (-not $npx) {
    Write-Host "Node.js nao encontrado. A instalar via winget..." -ForegroundColor Yellow
    winget install OpenJS.NodeJS.LTS --accept-package-agreements --accept-source-agreements
    $env:PATH = [Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [Environment]::GetEnvironmentVariable("PATH", "User")
    $npx = Get-Command npx.cmd -ErrorAction Stop
}

# Caminho curto 8.3 (o Claude invoca via cmd.exe sem aspas; espacos partem o comando)
$fso = New-Object -ComObject Scripting.FileSystemObject
$npxShort = $fso.GetFile($npx.Source).ShortPath
Write-Host "npx: $npxShort"

# 2. Localizar config do Claude Desktop (MSIX/Store ou instalacao classica)
$candidates = @()
$msix = Get-ChildItem "$env:LOCALAPPDATA\Packages" -Directory -Filter "Claude_*" -ErrorAction SilentlyContinue
foreach ($p in $msix) { $candidates += Join-Path $p.FullName "LocalCache\Roaming\Claude" }
$candidates += Join-Path $env:APPDATA "Claude"

$claudeDir = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $claudeDir) {
    Write-Error "Claude Desktop nao encontrado. Instale primeiro: https://claude.ai/download"
}
$configPath = Join-Path $claudeDir "claude_desktop_config.json"
Write-Host "Config: $configPath"

# 3. Ler/criar config e adicionar o servidor (preserva o resto da config)
if (Test-Path $configPath) {
    Copy-Item $configPath "$configPath.bak" -Force
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
} else {
    $config = [pscustomobject]@{}
}
if (-not ($config.PSObject.Properties.Name -contains "mcpServers")) {
    $config | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([pscustomobject]@{})
}

$mcpArgs = @("-y", "mcp-remote", $ServerUrl)
if ($ServerUrl.StartsWith("http://")) { $mcpArgs += "--allow-http" }
$mcpArgs += @(
    "--disable-resource-parameter",
    "--static-oauth-client-info", "{`"client_id`":`"$ClientId`"}",
    "--static-oauth-client-metadata", "{`"scope`":`"$Scope`"}"
)

$server = [pscustomobject]@{
    command = $npxShort
    args    = $mcpArgs
}

if ($config.mcpServers.PSObject.Properties.Name -contains $Name) {
    $config.mcpServers.$Name = $server
} else {
    $config.mcpServers | Add-Member -NotePropertyName $Name -NotePropertyValue $server
}

# 4. Gravar SEM BOM (o parser do Claude rejeita BOM)
$json = $config | ConvertTo-Json -Depth 10
[IO.File]::WriteAllText($configPath, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Config gravada com sucesso." -ForegroundColor Green

# 5. Reiniciar Claude se estiver aberto
$claude = Get-Process -Name claude -ErrorAction SilentlyContinue
if ($claude) {
    Write-Host "A fechar o Claude Desktop..."
    $claude | ForEach-Object { Stop-Process -Id $_.Id -Force }
    Start-Sleep 2
}

Write-Host ""
Write-Host "Instalacao concluida!" -ForegroundColor Green
Write-Host "1. Abra o Claude Desktop."
Write-Host "2. Vai abrir o browser para login Office 365 (pode abrir 2 janelas; complete apenas uma)."
Write-Host "3. O conector '$Name' fica ativo em Settings > Connectors."
