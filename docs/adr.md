# ADR 001: Escolha do Dapper para Acesso a Dados

## Contexto
O sistema TicketPrime necessita de um backend seguro, performático e resistente a SQL Injection. A equipe avaliou diferentes abordagens de acesso a dados no ecossistema .NET, considerando principalmente o Entity Framework (EF) Core (Code-First) e o Dapper.

O EF Core oferece alto nível de abstração, mapeamento objeto-relacional (ORM) e geração automática de consultas, porém introduz overhead de performance e reduz o controle direto sobre as queries SQL. O Dapper, por outro lado, é um micro‑ORM que mapeia resultados de consultas SQL manualmente escritas para objetos, mantendo a simplicidade e a velocidade.

Dado que o contrato do cliente exige segurança máxima (zero SQL Injection) e a capacidade de escrever consultas otimizadas com joins explícitos, a equipe precisava de uma solução que garantisse:
- Controle total sobre as sentenças SQL.
- Uso obrigatório de parâmetros (`@`) para prevenir injeção.
- Desempenho próximo do bare‑metal em operações de alta frequência (venda de ingressos).

## Decisão
Decidimos adotar o **Dapper** como camada de acesso a dados para toda a aplicação, em detrimento do Entity Framework Core.

A decisão se baseia nos seguintes critérios:
1. **Segurança**: Com o Dapper, todas as consultas são escritas manualmente, o que obriga o desenvolvedor a utilizar parâmetros de forma explícita. Isso elimina o risco de SQL Injection por concatenação de strings, alinhando‑se à regra “Zero SQL Injection” do contrato.
2. **Performance**: O Dapper é consistentemente mais rápido que o EF Core em cenários de leitura intensiva (como listagem de eventos e consulta de reservas), pois não possui o overhead de tracking, geração de SQL dinâmico e materialização complexa.
3. **Transparência**: A equipe pode inspecionar e ajustar cada query, garantindo que joins e filtros sejam os mais eficientes para o modelo de dados.
4. **Compatibilidade com o requisito de JOIN explícito**: O endpoint `GET /api/reservas/{cpf}` exibe o nome do evento, o que demanda um `INNER JOIN` entre `Reservas` e `Eventos`. Com o Dapper, o join é escrito de forma clara e otimizada, enquanto no EF Core a geração automática poderia produzir sub‑consultas desnecessárias.

A Minimal API do .NET 9 será utilizada para expor os endpoints, e o Dapper fará a ponte entre os parâmetros das rotas e o banco de dados SQL Server.

## Consequências

### Prós:
- **Segurança reforçada**: Todas as queries usam parâmetros (`@`), impossibilitando SQL Injection por concatenação.
- **Performance otimizada**: Latência reduzida em operações de leitura/escrita, crucial para picos de venda de ingressos.
- **Controle total sobre o SQL**: A equipe pode escrever consultas específicas para cada regra de negócio (ex.: contar reservas por evento, validar existência de CPF).
- **Simplicidade de configuração**: O Dapper não exige configurações complexas de mapeamento, reduzindo a curva de aprendizado.
- **Facilidade de depuração**: Como cada query é explícita, é fácil rastrear erros e analisar planos de execução no SQL Server.

### Contras:
- **Mais código manual**: O desenvolvedor precisa escrever cada consulta SQL, aumentando o volume de código em comparação com o EF Core.
- **Manutenção do SQL**: Alterações no esquema do banco exigem atualização manual das queries nos arquivos .cs.
- **Risco de erro humano**: Se o desenvolvedor esquecer de usar um parâmetro e concatenar strings, a segurança ficará comprometida – por isso a adoção de revisões de código e testes automatizados é obrigatória.
- **Falta de migrações automáticas**: Diferente do EF Core, o Dapper não oferece ferramentas de migração de esquema; as mudanças na estrutura das tabelas devem ser gerenciadas via scripts SQL na pasta `/db`.

### Trade‑offs aceitos
A equipe aceita o trade‑off “mais código manual” em troca de segurança e performance, pois o domínio do projeto (venda de ingressos) é crítico e não tolera vulnerabilidades ou lentidão. A manutenção do SQL será mitigada pela documentação rigorosa dos scripts e pela criação de testes de integração que validam cada query.

A decisão também impacta a contratação: novos membros da equipe devem ter familiaridade com SQL e Dapper, em vez de apenas EF Core.

---

*Este ADR foi aprovado pela equipe de desenvolvimento em 20/04/2026.*