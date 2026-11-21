# Como compilar o ConectaRemote Portable no GitHub Actions

Passo a passo, do zero até o download liberado no site. Nada aqui afirma que o aplicativo está
pronto: ele só é considerado pronto depois da compilação, do download e do teste em duas máquinas
Windows.

---

## 1. Enviar o código para o GitHub

Se o app já está sincronizado com um repositório, pule para o passo 2.

1. No GitHub, clique em **New repository**.
2. Dê um nome (ex.: `conecta-remote`), escolha **Private** e clique em **Create repository**.
3. Envie o código do app para esse repositório. O pipeline espera a pasta `src/agent-windows` na
   raiz do repositório, exatamente como ela está aqui.

---

## 2. Criar o arquivo do pipeline

1. No repositório, clique em **Add file** → **Create new file**.
2. No nome do arquivo, digite:

   ```
   .github/workflows/build-portable.yml
   ```

   As barras criam as pastas automaticamente.
3. Abra o arquivo `src/agent-windows/build-portable.workflow.yml` deste projeto, copie **todo** o
   conteúdo e cole no arquivo novo do GitHub.
4. Clique em **Commit changes** → **Commit changes**.

O arquivo precisa ficar exatamente em `.github/workflows/build-portable.yml`. Em outra pasta o
GitHub não executa.

---

## 3. (Opcional) Configurar a assinatura digital

Sem isso o pacote é gerado, mas o Windows mostra o aviso do SmartScreen na primeira execução.

1. No repositório, clique em **Settings** → **Secrets and variables** → **Actions**.
2. Clique em **New repository secret**.
3. Nome: `WINDOWS_SIGNING_CERT_BASE64`
   Valor: o seu certificado `.pfx` convertido em base64.
4. Clique em **Add secret**.
5. Repita com o nome `WINDOWS_SIGNING_CERT_PASSWORD` e a senha do certificado.

Se não tiver certificado, não crie nada: o pipeline gera o pacote sem assinatura e avisa.

---

## 4. Iniciar a compilação

A compilação que publica release só roda com uma tag.

1. No seu computador, dentro da pasta do repositório:

   ```bash
   git tag portable-v1.0.0
   git push origin portable-v1.0.0
   ```

   Para uma versão nova, use `portable-v1.0.1`, `portable-v1.0.2` e assim por diante.

2. No GitHub, abra a aba **Actions** e clique em **Build ConectaRemote Portable** (o que tem a tag
   `portable-v1.0.0`).
3. Acompanhe as etapas. A última, **Publicar release**, só aparece quando a execução vem de uma tag.

Para gerar apenas um teste, sem release, use **Run workflow** na aba Actions. Nesse caso o ZIP fica
como artefato da execução, não vira release.

---

## 5. O que o pipeline verifica antes de publicar

O arquivo do pipeline já faz essas checagens e falha se algo faltar:

| Etapa | O que confere |
| --- | --- |
| Instalar o .NET 8 | SDK 8.0.x disponível no runner |
| Compilar o pacote portátil | publica self-contained, arquivo único, win-x64 |
| Conferir o conteúdo do pacote | `ConectaRemotePortable.exe` e `appsettings.json` dentro do ZIP |
| Guardar o pacote como artefato | ZIP, `checksums.txt` e `version.txt` |
| Publicar release | os três arquivos anexados ao release da tag |

O ZIP publicado contém:

- `ConectaRemotePortable.exe` (self-contained, com o runtime do .NET embutido)
- `appsettings.json` (URL do app, porta local 8765 e origens permitidas)
- `README.md` (instruções do pacote)

---

## 6. Ligar o download no site

O site não confia em link digitado à mão: ele consulta o release e só libera o botão quando existem
o ZIP, o `checksums.txt` e um SHA-256 válido.

1. Copie o endereço do repositório no formato `usuario/repositorio`
   (aparece na barra do navegador, em `github.com/usuario/repositorio`).
2. Peça ao assistente para preencher `PORTABLE_REPO` em `src/lib/portable-release.js` com esse
   valor.
3. Abra a página **Baixar para Windows** no site e confirme:
   - o selo muda para **Pacote disponível**;
   - aparecem versão, tamanho, data e o SHA-256;
   - o botão baixa o ZIP do release.

Enquanto o release não existir, a página mostra **Em compilação** e nenhum arquivo é entregue.

---

## 7. Conferir o hash do arquivo baixado

No Windows, com o ZIP baixado:

```powershell
Get-FileHash .\ConectaRemotePortable-win-x64.zip -Algorithm SHA256
```

O valor precisa ser igual ao SHA-256 mostrado na página de download.

---

## 8. Teste real em duas máquinas Windows

Use duas máquinas diferentes, ambas com Windows 10 ou 11. Chame de **A** o computador que recebe a
assistência (abre o aplicativo) e **B** o do técnico (usa o navegador).

### Em A (computador assistido)

1. Extraia o ZIP em uma pasta e abra `ConectaRemotePortable.exe`.
2. Entre com e-mail e senha. O login do Google não funciona dentro da janela do aplicativo: se
   tentar, o app abre o site no navegador e explica o caminho que funciona.
3. Confirme que a janela mostra a mesma conta do site, com os mesmos códigos e sessões.
4. Vá em **Compartilhar** e gere o código de 9 dígitos.
5. Na sala de assistência, clique em **Conectar agente Windows** e confirme que o agente aparece
   como conectado (ele escuta só `127.0.0.1:8765`).
6. Quando a solicitação de B chegar, **autorize**.

### Em B (técnico)

1. Abra o site no navegador e entre com outra conta.
2. Vá em **Conectar**, digite o código de 9 dígitos e envie a solicitação.
3. Quando A autorizar, escolha a tela em A e confirme que o vídeo aparece.
4. Teste o chat.

### Controle de mouse e teclado

1. Em A, na sala, clique em **Ativar controle remoto** e confirme a permissão.
2. Em B, clique sobre a tela compartilhada para assumir o mouse.
3. Teste: mover o cursor, clicar, abrir o menu Iniciar, digitar em um campo de texto, rolar a página.
4. Em A, pressione `Ctrl + Alt + Shift + F12` para interromper: o controle deve parar na hora e
   todas as teclas e botões presos devem ser soltos.
5. Reative, encerre a sessão por B e confirme que o controle para e que o histórico registra a
   sessão como encerrada.

### Checklist de aprovação

- [ ] ZIP baixado pelo botão do site, com hash igual ao exibido
- [ ] Aplicativo abre sem instalar e sem pedir administrador
- [ ] Login por e-mail e senha funciona dentro do aplicativo
- [ ] Código gerado no aplicativo conecta com a versão Web
- [ ] Vídeo, chat e autorização funcionam
- [ ] Controle de mouse e teclado funciona só depois da autorização
- [ ] Atalho de emergência interrompe imediatamente
- [ ] Sessão encerrada aparece no histórico

Só depois desse checklist o pacote pode ser anunciado como pronto.

---

## 9. Login dentro do aplicativo portátil

| Situação | O que acontece |
| --- | --- |
| E-mail e senha | Funciona dentro da janela do aplicativo, inclusive código de verificação e MFA |
| Login pelo Google, Microsoft, Apple ou Facebook | O provedor recusa navegador embarcado; o app cancela a navegação, abre o site no navegador padrão e explica que a sessão fica lá |
| Conta criada pelo Google sem senha | Defina uma senha em **Esqueci minha senha** no site e entre com e-mail e senha no aplicativo |
| Links externos | Abrem no navegador padrão, sem sair do app |
| Sessão entre execuções | Fica salva na pasta local do aplicativo, então o login persiste no mesmo computador |

---

## 10. Compilar no seu Windows, sem GitHub

```powershell
cd src\agent-windows
pwsh ./build-portable.ps1 -Version 1.0.0
```

Saída em `src\agent-windows\dist\`. Para assinar:

```powershell
pwsh ./build-portable.ps1 -Version 1.0.0 -CertificatePfx C:\cert.pfx -CertificatePassword "senha"
```
