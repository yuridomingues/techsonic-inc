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
2. Suba o SQL Server:
```bash
docker run -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=TechSonicInc@2026 -p 1433:1433 --name ticketprime-sql -d mcr.microsoft.com/mssql/server:2022-latest
```
3. Crie o banco:
```bash
docker exec -i ticketprime-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "TechSonicInc@2026" -C -Q "IF DB_ID('TicketPrime') IS NULL CREATE DATABASE TicketPrime;"
```
4. Aplique o schema:
```bash
docker cp db/schemaTicket.sql ticketprime-sql:/tmp/schemaTicket.sql
docker exec -i ticketprime-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "TechSonicInc@2026" -C -d TicketPrime -i /tmp/schemaTicket.sql
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

### Executar os testes
```bash
dotnet test tests/techsonic-inc.Tests/techsonic-inc.Tests.csproj
```

### Executar o frontend (opcional)
```bash
dotnet run --project frontend/TicketPrime.Web/TicketPrime.Web.csproj
```

---

*Tech Sonic Inc.*
