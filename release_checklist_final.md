# Checklist de Entrega Final – TicketPrime

Este documento atesta que todos os itens obrigatórios das AV1 e AV2 foram implementados e validados.

## AV1 – Fundação e Cadastros Básicos (10 pontos)

| Item | Descrição | Status |
|------|-----------|--------|
| 1 | Histórias de Usuário no formato exato (Como [ator], Quero [ação], Para [motivo]) | [x] Concluído |
| 2 | Critérios BDD no formato exato (Dado que… Quando… Então…) | [x] Concluído |
| 3 | README executável com comandos de terminal em blocos de código | [x] Concluído |
| 4 | Script do banco (`/db/schemaTicket.sql`) com as 4 tabelas exigidas | [x] Concluído |
| 5 | Contrato da API com os 4 endpoints mapeados (POST/GET eventos, POST cupons, POST usuarios) | [x] Concluído |
| 6 | Fail‑Fast: validações que retornam Status Codes explícitos (400, 404, etc.) | [x] Concluído |
| 7 | Segurança no Dapper: todas as consultas usam parâmetros `@` | [x] Concluído |
| 8 | Zero SQL Injection: nenhuma concatenação ou interpolação nas queries | [x] Concluído |
| 9 | Infraestrutura de testes xUnit com métodos `[Fact]`/`[Theory]` | [x] Concluído |
| 10 | Testes com oráculo: todas as asserções possuem `Assert.` | [x] Concluído |

## AV2 – Coração do Sistema e Blindagem (10 pontos)

| Item | Descrição | Status |
|------|-----------|--------|
| 1 | ADR com os títulos exatos (## Contexto, ## Decisão, ## Consequências) | [x] Concluído |
| 2 | Trade‑offs no ADR (listas de Prós: e Contras:) | [x] Concluído |
| 3 | Matriz de Riscos em `/docs/operacao.md` com colunas Risco, Probabilidade, Impacto, Ação | [x] Concluído |
| 4 | Gatilho de Risco: coluna extra “Gatilho” na matriz de riscos | [x] Concluído |
| 5 | Métrica Operacional com campos Fórmula:, Fonte de Dados:, Frequência: | [x] Concluído |
| 6 | Ação da Métrica: campo “Ação se Violado:” definido | [x] Concluído |
| 7 | Objetivo de Serviço (SLO) com porcentagem e janela de tempo | [x] Concluído |
| 8 | Error Budget Policy documentada | [x] Concluído |
| 9 | Segurança de Código (SSDF): nenhuma senha ou string de conexão hardcoded em arquivos .cs | [x] Concluído |
| 10 | Checklist Final (este arquivo) com caixas de seleção marcadas como concluídas | [x] Concluído |

## Verificação Técnica

- [x] O repositório contém as pastas `/docs`, `/db`, `/src`, `/tests` com os arquivos exigidos.
- [x] A API responde aos endpoints AV1 e AV2 conforme especificado.
- [x] Todos os testes unitários passam (14 testes aprovados).
- [x] O banco de dados pode ser criado a partir do script `schemaTicket.sql`.
- [x] A documentação de arquitetura (ADR) e operação (`operacao.md`) está completa e formatada corretamente.

## Assinatura da Equipe

*Yuri Domingues* – 06010142  
*João Victor Andrade* – 06009925  
*Giulia Massafra* – 06005334  
*Julia Scarpi* – 06006846  
*Leonardo Otoline* – 06010109

**Data da entrega:** 20/04/2026

---

*Este checklist é parte integrante da entrega do projeto TicketPrime e atesta que todos os requisitos das AV1 e AV2 foram atendidos.*