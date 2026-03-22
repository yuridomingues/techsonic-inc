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
- Como comprador, quero visualizar os eventos disponíveis, para escolher qual ingresso desejo comprar.

**Critério de Aceitação:**
- Dado que existem eventos cadastrados com ingressos em estoque, quando o comprador acessar a lista de eventos, então o sistema deve exibir o nome, a data e a quantidade disponível de cada evento.
  
---

### HU03 – Compra de Ingressos
- Como comprador, quero comprar um ingresso para um evento informando meu CPF, para garantir minha participação.
  
**Critério de Aceitação:**
- Dado que o evento possui ingressos disponíveis e o CPF do comprador ainda não realizou uma compra para este evento, quando o comprador finalizar a compra, então o sistema deve registrar a venda, reduzir o estoque do evento e retornar sucesso.
 
