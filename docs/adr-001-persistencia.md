# ADR 001: Escolha da Tecnologia de Persistência

## Contexto
O sistema precisa de alta performance e proteção contra SQL Injection, evitando a complexidade de ORMs pesados.

## Decisão
Utilizaremos *Dapper* com consultas parametrizadas e scripts SQL manuais em vez de Entity Framework.

## Consequências
- *Prós:* Performance superior e controlo total sobre o SQL executado.
- *Contras:* Necessidade de escrever e manter scripts de base de dados manualmente.
- *Segurança:* Uso obrigatório de parâmetros (@) para evitar SQL Injection.
