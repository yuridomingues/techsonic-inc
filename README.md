# 🎟️ Tech Sonic Inc.

## 👥 Grupo


|Aluno(a)|Matricula|
|---|---|
|Yuri Domingues|06010142|
|João Victor Andrade|06009925|
|Giulia Massafra|06005334|
|Julia Scarpi|06006846|
|Leonardo Otoline|06010109|

## 🎯 Funcionalidades Implementadas (além do núcleo AV1 em `docs/requisitos.md`)

- **Admin Dashboard**: Página `/admin` com métricas consolidadas via API (ingressos vendidos, receita, eventos ativos, usuários); use **Atualizar** para recarregar os dados.
- **Criação Completa de Eventos**: Formulário com campos principais (nome, tipo, capacidade, data, preço, descrição, local, URL de banner) e validação frontend/backend.
- **Geração automática de setores e assentos**: Opção ao criar evento; o backend cria setores padrão **Premium / Vip / Regular** (preços derivados do preço padrão do evento) e o mapa de assentos associado.
- **Lista de Eventos com ações (admin)**: Tabela com status (ativo, cancelado, encerrado); admin pode **cancelar** evento na lista e **criar** novo pelo botão **Novo evento** (não há edição inline de evento na tabela).
- **Cancelamento de Eventos (Admin)**: Atualiza o evento, cancela reservas vinculadas, marca pagamentos **aprovados** como **estornado** no banco e gera cupom de 10% por reserva afetada *(fluxo em dados; não integra gateway de cartão)*.
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
2. Suba o SQL Server local, crie o banco `TicketPrime` e aplique o schema automaticamente (PowerShell, na raiz do repositório):
```powershell
.\scripts\start-local-sql.ps1
```

O script usa o [docker-compose.yml](docker-compose.yml), sobe o serviço `sqlserver`, aguarda o login do `sa` responder em `127.0.0.1:1433`, cria o banco se necessário e aplica `db/schemaTicket.sql`. Se o schema já tiver sido aplicado antes, o `sqlcmd` pode acusar objetos já existentes; nesse caso use `docker compose down -v` e rode o script de novo para um banco limpo.

Para parar o banco local depois:

```powershell
.\scripts\stop-local-sql.ps1
```

### Restaurar dependências
```bash
dotnet restore src/techsonic-inc.csproj
dotnet restore tests/techsonic-inc.Tests/techsonic-inc.Tests.csproj
dotnet restore frontend/TicketPrime.Web/TicketPrime.Web.csproj
```

### Executar a API + interface (recomendado)
O projeto **`src`** referencia o Blazor e **hospeda a Minimal API e o frontend no mesmo endereço** (mesma origem → `/api/*` e a SPA funcionam juntos).

```bash
dotnet run --project src/techsonic-inc.csproj
```

Abra no navegador a URL que o console mostrar. Com o perfil padrão do repositório, costuma ser **`https://localhost:7148`** (e **`http://localhost:5148`**). **Não use só** `dotnet run` no `frontend/TicketPrime.Web` para testar eventos/login: nesse modo o `HttpClient` chama o próprio host do dev server do WASM, **onde não existe** a API, e a lista de eventos falha com “Não foi possível carregar eventos”.

### Executar só a API (sem abrir o Blazor pelo `src`)
Se precisar subir apenas o host web da API (por exemplo para testar com Postman), ainda assim use o mesmo projeto `src` — é ele que contém `Program.cs` e as rotas `/api/...`.

Observação: no Windows, a connection string do projeto deve usar `Server=127.0.0.1,1433` em vez de `localhost` ou `127.0.0.1` sem porta, para forçar a conexão TCP com o SQL Server exposto pelo container Docker e evitar a resolução de instância local via Named Pipes.

**E-mail / SMTP (opcional, não faz parte da AV1 do `projeto.pdf`):** o enunciado da AV1 só exige API + banco + testes; não pede envio de e-mail. O código atual, porém, inclui fluxo de cadastro com **código de verificação**: se `Email:Smtp:Host` em `src/appsettings.json` estiver **vazio**, a API grava a mensagem em **`src/logs/emails`** (modo pickup) e o fluxo continua testável **sem** Mailtrap nem SMTP. Só configure SMTP se quiser receber o código num provedor real ou num sandbox (ex.: Mailtrap); nesse caso você pode editar o `appsettings.json` ou usar o script auxiliar `.\scripts\configure-mailtrap.ps1`.

### Executar os testes
```bash
dotnet test tests/techsonic-inc.Tests/techsonic-inc.Tests.csproj
```

Observação: a suite inclui testes de integração HTTP com banco SQL Server real temporário. Para executá-los de fato, deixe o SQL acessível em `127.0.0.1:1433` (por exemplo com `.\scripts\start-local-sql.ps1` antes do `dotnet test`). Se o SQL não estiver disponível, os testes de integração aparecem como `skipped` em vez de falhar a suite inteira.

### Executar só o projeto Blazor isolado (avançado / depuração)
```bash
dotnet run --project frontend/TicketPrime.Web/TicketPrime.Web.csproj
```

Nesse modo a API **não** roda no mesmo host; a tela de eventos **não** conseguirá carregar dados até você apontar o `HttpClient` para outra URL (não configurado por padrão). Para uso normal do grupo, prefira **`dotnet run --project src/techsonic-inc.csproj`** acima.

---

*Tech Sonic Inc.*
