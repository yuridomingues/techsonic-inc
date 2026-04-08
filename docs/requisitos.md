# Requisitos do Sistema - Tech Sonic Inc.

## Visão Geral
O sistema Tech Sonic Inc. tem como objetivo gerenciar a venda de ingressos para eventos de forma segura, eficiente e escalável, garantindo a integridade dos dados e proteção contra SQL Injection.

---

## Histórias de Usuário

### HU01 – Cadastro de Evento
- Como operador do sistema, quero cadastrar um novo evento com nome, data e capacidade, para que ele fique disponível para venda.
  
**Critério de Aceitação:**
- Dado que o operador inseriu um nome válido, uma data futura e uma capacidade positiva, quando ele confirmar o cadastro, então o sistema deve persistir os dados no banco usando Dapper e retornar o status 201 (Created).
---

### HU02 – Listagem de Eventos
- Como comprador, quero visualizar os eventos cadastrados, para entender a agenda de eventos.

**Critério de Aceitação:**
- Dado que existem eventos cadastrados, quando o comprador acessar a lista de eventos, então o sistema deve exibir nome, capacidade, data e preço.
  
---

### HU03 – Cadastro de Cupom
- Como operador do sistema, quero cadastrar um cupom de desconto, para campanhas promocionais.
  
**Critério de Aceitação:**
 - Dado que o operador informou código e percentual válidos, quando confirmar o cadastro, então o sistema deve persistir os dados e retornar 201.

---

### HU04 – Cadastro de Usuário
- Como operador do sistema, quero cadastrar usuários pelo CPF, para habilitar futuras transações.

**Critério de Aceitação:**
- Dado que o CPF não está duplicado e os campos obrigatórios foram informados, quando confirmar o cadastro, então o sistema deve persistir os dados e retornar 201.
 
