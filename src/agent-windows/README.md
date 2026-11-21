# ConectaRemote Portable

Aplicativo portátil para Windows 10 e 11, 64 bits, que reúne em um único executável:

- a janela do ConectaRemote (a mesma interface, conta, códigos e sessões da versão Web);
- o agente local que executa mouse e teclado quando o controle remoto é autorizado.

Sem instalação tradicional, sem serviço, sem exigir o .NET na máquina e sem abrir portas para a
internet.

## O que o executável faz

- Abre a mesma conta do site: nenhum cadastro separado e nenhum sistema paralelo de códigos.
- Gera o código de acesso de 9 dígitos, com validade de 10 minutos, igual à versão Web.
- Compartilha a tela mediante autorização explícita, com escolha da tela.
- Permite controle de mouse e teclado somente depois da liberação na sessão.
- Mostra o status da conexão, o tempo da sessão e o indicador de controle ativo.
- Encerra a sessão imediatamente e solta todas as teclas e botões.
- Escuta o servidor local apenas em `127.0.0.1:8765`, com validação de origem e pareamento por
  código de uso único de 6 dígitos.

## Como gerar o pacote

Requisitos: Windows 10/11 e [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
cd src\agent-windows
pwsh ./build-portable.ps1 -Version 1.0.0
```

Saída em `src\agent-windows\dist\`:

| Arquivo                                | Conteúdo                                                    |
| -------------------------------------- | ----------------------------------------------------------- |
| `ConectaRemotePortable-win-x64.zip`    | executável self-contained, `appsettings.json` e este README |
| `checksums.txt`                        | SHA-256 do ZIP                                              |
| `version.txt`                          | versão, tamanho e hash                                      |

O script publica com `--self-contained true`, arquivo único e compressão, então o runtime do .NET
vai dentro do pacote. Nada precisa ser instalado no computador que recebe a assistência.

Se o `restore` reclamar do pacote `Microsoft.Web.WebView2`, atualize a versão no
`ConectaRemotePortable.csproj` para a mais recente do NuGet.

### Compilar sem Windows local

Copie `build-portable.workflow.yml` para `.github/workflows/build-portable.yml` no repositório. O
pipeline roda em `windows-latest`, compila, guarda o artefato e publica o ZIP como release quando
a tag `portable-v*` é enviada:

```bash
git tag portable-v1.0.0
git push origin portable-v1.0.0
```

O app não pode criar arquivos em `.github/workflows` pela permissão da integração GitHub, por isso
o workflow fica nesta pasta para ser copiado.

## Publicar o download no site

O site não usa link digitado à mão. Ele consulta o release do repositório e só libera o botão
quando encontra, no mesmo release:

- o ZIP `ConectaRemotePortable-win-x64.zip`;
- o `checksums.txt` com um SHA-256 válido.

Para ligar, preencha apenas o repositório em `src/lib/portable-release.js`:

```js
export const PORTABLE_REPO = "usuario/repositorio";
```

Sem release completo, a página **Baixar para Windows** mostra o download como em compilação e não
entrega nenhum arquivo. A rota `/api/download/portable` responde 404 explicando o motivo e só
redireciona para o release quando o pacote e o hash existem.

## Assinatura digital

Assinar reduz o aviso do SmartScreen e comprova a origem do executável.

1. Obtenha um certificado de assinatura de código (Authenticode, OV ou EV).
2. Gere o pacote com assinatura:

```powershell
pwsh ./build-portable.ps1 -Version 1.0.0 `
  -CertificatePfx C:\caminho\certificado.pfx -CertificatePassword "senha"
```

O script localiza o `signtool.exe` no Windows SDK, assina com SHA-256 e aplica carimbo de tempo.
No pipeline, configure os segredos `WINDOWS_SIGNING_CERT_BASE64` (PFX em base64) e
`WINDOWS_SIGNING_CERT_PASSWORD`.

Sem certificado, o pacote é gerado com aviso explícito e o SmartScreen exibe "aplicativo não
reconhecido" na primeira execução. Um certificado EV elimina o aviso imediatamente; OV ganha
reputação com o tempo.

## Permissões do Windows

| Item                          | Comportamento                                                     |
| ----------------------------- | ----------------------------------------------------------------- |
| Administrador (UAC)           | não é necessário                                                  |
| Instalação / serviço          | nenhum: o app roda direto do ZIP                                  |
| Firewall / portas de entrada  | nenhuma: escuta só em `127.0.0.1`                                 |
| SmartScreen                   | aviso na primeira execução se não houver assinatura                |
| Antivírus                     | pode sinalizar executável novo sem reputação; assinatura resolve   |
| WebView2 Runtime              | vem com o Windows 10/11 atualizado; se faltar, o app orienta       |

O WebView2 Runtime é um componente do Windows (Edge), não o .NET. O app detecta a ausência e
mostra um botão para baixar o componente.

## Distribuição segura

- Publique sempre por release oficial, com `checksums.txt` ao lado do ZIP.
- Divulgue o SHA-256 no site e confira antes de executar:

```powershell
Get-FileHash .\ConectaRemotePortable-win-x64.zip -Algorithm SHA256
```

- Nunca distribua o ZIP por mensagem, pen drive de terceiros ou link encurtado.
- Mantenha a versão publicada em `src/lib/portable-release.js` igual à do release.
- Reassine a cada versão e mantenha o histórico de releases para auditoria.

## Teste antes de considerar pronto

1. Baixe o ZIP, extraia e abra `ConectaRemotePortable.exe` (dois cliques).
2. Entre com uma conta. Confirme que a janela abre a mesma conta da versão Web.
3. Gere o código e conecte a partir de outro computador pelo navegador.
4. Autorize a conexão, escolha a tela e confirme vídeo e chat.
5. Ative o controle remoto, teste mouse e teclado, e use `Ctrl + Alt + Shift + F12` para interromper.
6. Encerre a sessão e confirme o registro no histórico.
7. Feche e reabra o app: o servidor local precisa subir sem instalação e sem administrador.

O aplicativo só deve ser apresentado como pronto depois desses passos em duas máquinas Windows.

## Limitações conhecidas

- Somente Windows 10 e 11, 64 bits.
- Login por Google dentro da janela embarcada pode ser recusado pelo Google; use e-mail e senha, ou
  abra o Google no navegador padrão.
- Redes corporativas com NAT simétrico exigem servidor TURN próprio para o vídeo.
- A área de transferência entre os computadores não é sincronizada.

## Protocolo

Documentação completa em **Agente Windows** dentro do app (`/agent`). Resumo:

| Mensagem         | Direção            | Conteúdo                                                        |
| ---------------- | ------------------ | --------------------------------------------------------------- |
| `hello`          | navegador → agente | `pairingCode`, `sessionId`, `appOrigin`, `sessionToken`         |
| `hello_ok`       | agente → navegador | `device { name, version, platform }`, `screens[]`               |
| `pointer`        | navegador → agente | `action` (`move`/`down`/`up`/`wheel`), `x`, `y` (0–1), `button` |
| `key`            | navegador → agente | `action` (`down`/`up`), `code` (`KeyC`, `Enter`, `F5`)           |
| `release_all`    | navegador → agente | solta tudo                                                      |
| `revoke`         | navegador → agente | encerra a permissão da sessão                                   |
| `emergency_stop` | agente → navegador | interrupção local pelo usuário assistido                        |

## Estrutura do código

| Arquivo                          | Responsabilidade                                        |
| -------------------------------- | ------------------------------------------------------- |
| `Program.cs`                     | entrada: sobe o servidor local, bandeja, atalho, janela |
| `MainWindow.cs`                  | janela única com WebView2 e tela de erro com instruções |
| `AgentServer.cs`                 | servidor WebSocket loopback, pareamento e protocolo     |
| `InputExecutor.cs`               | mouse e teclado via SendInput, liberação total          |
| `KeyMap.cs`                      | teclas físicas do navegador para virtual keys           |
| `HotkeyWindow.cs`                | atalho global de interrupção                            |
| `AgentConfig.cs`                 | leitura do `appsettings.json`                           |
| `build-portable.ps1`             | publicação self-contained, assinatura e ZIP             |
| `build-portable.workflow.yml`    | pipeline de compilação no GitHub Actions                |
