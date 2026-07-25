# VORTEX PC

Agente pessoal de IA para Windows, construído em C# com WPF e .NET 8.

## O que já funciona

- Chat com OpenAI e Groq.
- Interface desktop própria, com janela principal e chat rápido.
- Histórico local persistente em SQLite.
- Credenciais protegidas com Windows DPAPI para o usuário atual.
- Companion flutuante para acesso rápido.
- Indicador visual dos estados online, pensando e erro.
- Executável autocontido para Windows x64.
- Contexto das últimas mensagens enviado ao modelo.
- Terminal PowerShell integrado ao chat.
- Abertura de aplicativos e pastas por linguagem natural.
- Movimentação e edição de arquivos com confirmação explícita.

## Comandos locais

O VORTEX reconhece comandos comuns diretamente no chat:

```text
dir
git status
dotnet build
/terminal Get-Process
abra o bloco de notas
abra a pasta "C:\Projetos"
```

Para operações que podem apagar, sobrescrever ou mover dados, reenvie a
instrução com confirmação:

```text
/confirmar Move-Item -LiteralPath "C:\origem" -Destination "D:\destino"
/confirmar modifique o arquivo "C:\notas.txt" com "novo conteúdo"
```

O diretório alterado com `cd` permanece ativo para os próximos comandos da
sessão. A saída é limitada no chat e processos são interrompidos após 45
segundos para evitar travamentos.

## Segurança

Nenhuma chave de API é incluída no código ou no repositório. A chave informada
no aplicativo é protegida pelo Windows antes de ser gravada no banco local.
Nunca publique chaves em commits, issues ou screenshots.

## Executar no desenvolvimento

Requisitos: Windows 10/11 e .NET 8 SDK.

```powershell
dotnet restore VORTEX.sln
dotnet run --project src/VORTEX.UI/VORTEX.UI.csproj
```

## Gerar o `.exe`

```powershell
.\build-release.ps1
```

O pacote será criado em `artifacts/VORTEX-win-x64`. A publicação é
autocontida, portanto o computador de destino não precisa ter o .NET instalado.

## Estrutura

- `VORTEX.Core`: contratos e modelos.
- `VORTEX.AIProviders`: integrações com modelos de IA.
- `VORTEX.Database`: persistência e proteção de credenciais.
- `VORTEX.Services`: coordenação da aplicação.
- `VORTEX.ViewModels`: estado e comandos MVVM.
- `VORTEX.UI`: experiência WPF.

## Próximas etapas

- Ferramentas locais com consentimento e trilha de auditoria.
- Memória semântica por projeto.
- Voz, atalhos globais e notificações.
- Streaming de respostas e cancelamento.
- Testes adicionais da persistência e da interface.
