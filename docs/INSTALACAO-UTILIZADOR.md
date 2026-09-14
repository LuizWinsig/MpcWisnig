# Ligar o Claude Desktop ao PHC (winsig MCP Server)

Este guia explica como ligar o teu Claude Desktop ao servidor PHC da WINSIG. Depois de instalado, podes perguntar ao Claude coisas como *"quantas horas reconhecidas tenho este mês?"* e ele consulta diretamente o PHC.

## Requisitos

- Windows 10/11
- **Claude Desktop** instalado (<https://claude.ai/download>)
- Conta Office 365 da WINSIG com acesso atribuído (pedir ao administrador se necessário)

## Instalação automática (recomendado)

1. Guarda o ficheiro `Install-ClaudeConnector.ps1` (fornecido pela WINSIG) numa pasta local, p. ex. `Downloads`.
2. Abre o **PowerShell** (não precisa de ser administrador, exceto se faltar o Node.js).
3. Executa:

```powershell
cd $env:USERPROFILE\Downloads
powershell -ExecutionPolicy Bypass -File .\Install-ClaudeConnector.ps1
```

O script trata de tudo:

- Instala o Node.js automaticamente se não existir (via winget)
- Localiza a configuração do Claude Desktop (versão Store ou clássica) e faz backup
- Adiciona o conector `winsig-phc`
- Fecha o Claude Desktop para aplicar as alterações

4. Abre o **Claude Desktop**.
5. Na primeira utilização abre-se o browser para **login Office 365** — inicia sessão com a tua conta WINSIG.
   > Nota: podem abrir-se **duas** janelas de login; completa apenas uma e fecha a outra.
6. Confirma em **Settings → Connectors** que o `winsig-phc` está ativo.

## Experimentar

Pergunta ao Claude, por exemplo:

- *"Quantas horas reconhecidas tenho entre 1 e 31 de agosto?"*
- *"Mostra o detalhe de horas do projeto 25AC081 este mês."*

O Claude usa as ferramentas `get_recognized_hours` e `get_detail_recognized_hours` conforme necessário.

## Resolução de problemas

| Problema | Solução |
|---|---|
| "Server disconnected" ao abrir o Claude | Fechar totalmente o Claude (ícone da bandeja → Quit), apagar a pasta `%USERPROFILE%\.mcp-auth` e abrir de novo |
| Browser não abre para login | Fechar e reabrir o Claude Desktop |
| "Access denied" ao usar uma ferramenta | Falta a role no Office 365 — pedir ao administrador para atribuir `Phc.Read` / `Phc.ReadDetail` |
| Pede login outra vez passado uns dias | Normal — o token expirou; basta iniciar sessão de novo |
| winget não encontrado (instalação do Node) | Instalar Node.js LTS manualmente em <https://nodejs.org> e correr o script outra vez |

## Desinstalar

Apagar a entrada `winsig-phc` do ficheiro de configuração do Claude (o script criou um backup `.bak` na mesma pasta) e apagar `%USERPROFILE%\.mcp-auth`.

## O que é instalado, em detalhe

- **Node.js** — necessário para o proxy `mcp-remote`, que faz a ponte entre o Claude Desktop e o servidor HTTPS.
- **Entrada na configuração do Claude** (`claude_desktop_config.json`) que arranca o `mcp-remote` a apontar para `https://suporte.winsig.pt/mcp` com autenticação OAuth (Entra ID).
- **Tokens de sessão** guardados em `%USERPROFILE%\.mcp-auth` após o primeiro login.

Nenhuma password fica guardada localmente — a autenticação é feita pelo Office 365 (Entra ID) e cada ferramenta só funciona se a tua conta tiver a role adequada.

## Script de instalação (referência)

O script completo está em `scripts\Install-ClaudeConnector.ps1` neste repositório. Parâmetros por omissão:

```powershell
param(
    [string]$ServerUrl = "https://suporte.winsig.pt/mcp",
    [string]$ClientId  = "9c0c29df-333b-4dc2-812c-bd6bb68841d5",
    [string]$Scope     = "api://a77aca82-2b88-4dca-8537-7534ac3f7bbe/access_as_user",
    [string]$Name      = "winsig-phc"
)
```

Para apontar para outro servidor (ex.: testes locais): `.\Install-ClaudeConnector.ps1 -ServerUrl "http://localhost:6194"`.
