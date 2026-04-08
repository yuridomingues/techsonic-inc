# 🎟️ Tech Sonic Inc.

## 👥 Grupo


|Aluno(a)|Matricula|
|---|---|
|Yuri Domingues|06010142|
|João Victor Andrade|06009925|
|Giulia Massafra|06005334|
|Julia Scarpi|06006846|
|Leonardo Otoline|06010109|

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
dotnet restore
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
