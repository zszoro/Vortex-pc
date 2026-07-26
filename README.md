# VORTEX PC

Agente pessoal de IA para Windows, construído em C# com WPF e .NET 8.

## VORTEX Workspace 1.2

O modo Workspace transforma o chat em um agente de desenvolvimento:

- Modal de autorização para ler, criar, editar, excluir e mover arquivos.
- Autorização para comandos, programas, pastas e acesso à internet.
- A permissão vale somente para a ação apresentada no modal.
- Nova Conversa permite começar sem projeto, abrir uma pasta ou criar um projeto.
- Projetos novos são criados em `Documentos\VORTEX\Projetos`.
- Indexação de até 20 mil arquivos, ignorando saídas geradas e dependências vendorizadas.
- Detecção automática de linguagens, frameworks e manifestos de dependências.
- Workspace atual persistida entre mensagens e reinicializações.
- Seleção dos arquivos mais relevantes para cada solicitação.
- Conteúdo real dos arquivos relevantes enviado ao modelo.
- Planos estruturados de criação, edição, exclusão e movimentação.
- Modal mostra todos os caminhos antes da aplicação.
- Backup completo antes de qualquer plano de alteração.
- Tela para selecionar e restaurar backups anteriores.

## O que já funciona

- Chat com OpenAI e Groq.
- Interface desktop própria, com janela principal e chat rápido.
- Histórico local persistente em SQLite.
- Credenciais protegidas com Windows DPAPI para o usuário atual.
- Companion flutuante para acesso rápido.
- Indicador visual dos estados online, pensando e erro.
- Pet VORTEX vetorial, arrastável e com chat rápido embutido.
- Pet roxo em repouso, azul ao digitar/processar e vermelho em erros.
- Opção para manter o pet acima ou abaixo de outros aplicativos.
- Verificação de versão por manifesto do GitHub.
- Avisos de atualização na tela principal e nas configurações.
- Resolução de aplicativos pelo registro do Windows e pastas de instalação.
- Temas Vortex, Nebula, Oceano e Claro.
- Aparências Orb, Cyber, Minimal e Ghost para o núcleo.
- Ações de copiar, mencionar e regenerar em cada mensagem.
- Ferramentas laterais para arquivos, terminal, memória, automações e resumo.
- Verificação de atualização periódica sem reiniciar o aplicativo.
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

## Atualizações

O arquivo `version.json` informa a versão mais recente. Quando ela for superior
à versão instalada, o aplicativo exibe “Atualização VORTEX …” com as ações
“Atualizar” e “Agora não”. A guia Atualizações permanece disponível nas
configurações para uma verificação manual. Nenhuma instalação ocorre sem
confirmação do usuário.

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
