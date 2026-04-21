# 🎟️ Tech Sonic Inc.

## 👥 Grupo


|Aluno(a)|Matricula|
|---|---|
|Yuri Domingues|06010142|
|João Victor Andrade|06009925|
|Giulia Massafra|06005334|
|Julia Scarpi|06006846|
|Leonardo Otoline|06010109|

## 🎯 Funcionalidades Implementadas (Dashboard Admin)

- **Admin Dashboard**: Página `/admin` com métricas em tempo real (ingressos vendidos, receita total, eventos ativos, total de usuários).
- **Criação Completa de Eventos**: Formulário com todos os campos obrigatórios (nome, tipo, capacidade, data, preço, descrição, local, imagem) e validação frontend/backend.
- **Geração Automática de Setores**: Ao criar um evento, opção de gerar setores A, B, C com variação de preço (+20% / -20%).
- **Lista de Eventos com Ações**: Tabela exibindo eventos cadastrados, status (ativo, cancelado, encerrado) e botões para editar/cancelar.
- **Cancelamento de Eventos (Admin)**: Endpoint que atualiza status do evento, cancela reservas associadas, reembolsa pagamentos e gera cupons de 10% de desconto.
- **Validação Robusta**: Regras de negócio (capacidade > 0, preço > 0, data futura) aplicadas tanto no frontend (MudBlazor) quanto no backend (`ValidacoesEntrada`).
- **Upload de Imagem (Placeholder)**: Campo para URL de banner; preparado para integração com `MudFileUpload`.
- **Animações e UX Aprimorada**:
  - Transições de página (fade‑in)
  - Efeitos hover em tabelas e cartões
  - Skeletons durante carregamento das métricas
  - Animações CSS para pulse (assentos) e checkmark (sucesso)
- **Integração com SignalR**: Hub `SeatHub` implementado para bloqueio em tempo real de assentos (backend). Frontend preparado para consumir o hub.
- **Segurança**: Rotas `/admin` protegidas por flag `IsAdmin` no banco de dados; autenticação JWT.
- **Cadastro Fortalecido**: CPF validado com dígitos verificadores e bloqueio de sequências inválidas, nome com nome e sobrenome, detecção de erros comuns em e-mail e senha forte com maiúscula, minúscula, caractere especial e variedade numérica.
- **Validação por E-mail**: Novas contas ficam pendentes até a confirmação de um código de 6 dígitos enviado por e-mail.

---

## 📌 Contexto do Sistema

O *Tech Sonic Inc.* é um sistema de venda de ingressos para eventos desenvolvido com foco em *performance, segurança e escalabilidade*.

A plataforma permite:

- Cadastro de eventos  
- Cadastro de cupons de desconto  
- Cadastro de usuários com CPF único  

Tudo isso garantindo o controle eficiente de estoque e o cumprimento das regras de negócio.

---

## 🚀 Como Executar o Projeto

### Preparar o banco SQL Server (Docker)
1. Inicie o Docker Desktop e aguarde ficar em estado Running.
2. Suba o SQL Server local, crie o banco `TicketPrime` e aplique o schema automaticamente:
```powershell
.\bin\start-local-sql.ps1
```

O script usa o [docker-compose.yml](docker-compose.yml), sobe o servico `sqlserver`, aguarda o login do `sa` responder em `127.0.0.1:1433`, cria o banco se necessario e aplica `db/schemaTicket.sql` apenas na primeira inicializacao.

Para parar o banco local depois:

```powershell
.\bin\stop-local-sql.ps1
```

### Restaurar dependências
```bash
dotnet restore src/techsonic-inc.csproj
dotnet restore tests/techsonic-inc.Tests/techsonic-inc.Tests.csproj
dotnet restore frontend/TicketPrime.Web/TicketPrime.Web.csproj
```

### Executar a API (AV1)
```bash
dotnet run --project src/techsonic-inc.csproj
```

Observação: no Windows, a connection string do projeto deve usar `Server=127.0.0.1,1433` em vez de `localhost` ou `127.0.0.1` sem porta, para forçar a conexão TCP com o SQL Server exposto pelo container Docker e evitar a resolução de instância local via Named Pipes.

### Validação de e-mail no ambiente local
- Se `Email:Smtp:Host` estiver configurado em `src/appsettings.json`, o sistema envia o código de validação usando SMTP.
- Sem SMTP configurado, o sistema grava o e-mail em `src/logs/emails`, o que permite testar o fluxo localmente sem depender de um provedor externo.

### Usar Mailtrap Sandbox
- O Mailtrap Sandbox e ideal para testar o fluxo de e-mail sem enviar mensagens para a caixa real do usuario final.
- Importante: no Sandbox, o codigo de validacao aparece dentro da inbox do Mailtrap, nao no Gmail/Outlook real do usuario.
- Crie uma conta gratis no Mailtrap e abra uma inbox de `Email Sandbox`.
- Copie as credenciais SMTP da inbox: `host`, `port`, `username` e `password`.
- Configure o projeto executando o script abaixo no PowerShell:

```powershell
.\bin\configure-mailtrap.ps1
```

- O script vai pedir:
  - `From address`
  - `SMTP host`
  - `SMTP port`
  - `SMTP user`
  - `SMTP password`

- Depois reinicie a API:

```bash
dotnet run --project src/techsonic-inc.csproj
```

- Faça um cadastro de teste e consulte o codigo dentro da inbox do Mailtrap.
- Se voce quiser entrega real para o e-mail do usuario final, use um provedor de envio real em vez do Sandbox.

### Executar os testes
```bash
dotnet test tests/techsonic-inc.Tests/techsonic-inc.Tests.csproj
```

Observacao: a suite inclui testes de integracao HTTP com banco SQL Server real temporario. Para executar esses testes de fato, prepare o banco local antes com `./bin/start-local-sql.ps1`. Se o SQL local em `127.0.0.1` nao estiver acessivel, os testes sao descobertos normalmente, mas aparecem como `skipped` em vez de falhar a suite inteira.

### Executar o frontend (opcional)
```bash
dotnet run --project frontend/TicketPrime.Web/TicketPrime.Web.csproj
```

---

*Tech Sonic Inc.*
