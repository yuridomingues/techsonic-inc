# Operação e Monitoramento

## Matriz de Riscos

A tabela abaixo lista os principais riscos operacionais do sistema TicketPrime, com suas probabilidades, impactos, ações de mitigação e gatilhos para ativação.

| Risco | Probabilidade | Impacto | Ação | Gatilho |
|-------|---------------|---------|------|---------|
| Esgotamento da capacidade de um evento (overbooking) | Alta | Crítico | Bloqueio automático de novas reservas quando a capacidade total for atingida (regra R3). | Número de reservas para um EventoId igual à CapacidadeTotal. |
| Fraude de cupons (uso indevido de descontos) | Média | Alto | Validação no endpoint de reserva que o PrecoPadrao ≥ ValorMinimoRegra do cupom (regra R4). | Tentativa de aplicar um cupom cujo ValorMinimoRegra não é satisfeito. |
| Ataque de SQL Injection | Baixa | Crítico | Uso obrigatório de parâmetros (@) no Dapper e revisão de código com análise estática. | Qualquer query concatenada com strings (detectado em revisão). |
| Perda de conexão com o banco de dados | Média | Alto | Configuração de retry com backoff exponencial e fallback para resposta de erro 503. | Timeout ou exceção SqlException nas consultas. |
| Duplicação de reservas por CPF (cambistas) | Alta | Médio | Limite de 2 reservas por CPF por evento (regra R2). | Tentativa de criar uma terceira reserva para o mesmo CPF/EventoId. |
| Venda de ingressos para eventos passados | Baixa | Médio | Validação de data futura no cadastro de eventos e na reserva. | DataEvento anterior à data atual. |
| Exposição de dados sensíveis (CPF, e‑mail) | Média | Alto | Criptografia de dados em repouso (TDE) e uso de HTTPS em todas as comunicações. | Acesso não autorizado detectado via logs de auditoria. |

## Métricas Operacionais

### Disponibilidade do Serviço
- **Fórmula:** `(total_de_requisições_200_ok / total_de_requisições) * 100`
- **Fonte de Dados:** Logs da API (Application Insights ou Serilog).
- **Frequência:** Coleta a cada 5 minutos, agregação horária.
- **Ação se Violado:** Se a disponibilidade cair abaixo de 99,5% por mais de 1 hora, a equipe deve parar todas as atividades de desenvolvimento e investigar a causa raiz, priorizando a restauração do serviço.

### Tempo Médio de Resposta (Response Time)
- **Fórmula:** `soma(tempo_de_resposta) / total_de_requisições`
- **Fonte de Dados:** Middleware de métricas da própria API.
- **Frequência:** Coleta contínua, agregada a cada 10 minutos.
- **Ação se Violado:** Se o p95 ultrapassar 500 ms por mais de 30 minutos, escalar para a equipe de performance para análise de queries e otimização de índices.

### Taxa de Erros 4xx/5xx
- **Fórmula:** `(requisições_com_status_4xx_ou_5xx / total_de_requisições) * 100`
- **Fonte de Dados:** Logs HTTP.
- **Frequência:** A cada 5 minutos.
- **Ação se Violado:** Se a taxa de erro superar 2% por mais de 15 minutos, notificar imediatamente o engenheiro de plantão para verificar regras de validação e integridade dos dados.

## Objetivo de Serviço (SLO)

**SLO:** O sistema TicketPrime manterá uma disponibilidade de 99,5% em uma janela móvel de 30 dias.

Isso significa que o tempo de indisponibilidade tolerável é de **0,5% × 30 dias = 3,6 horas por mês**.

## Error Budget Policy

O *error budget* é a quantidade de falhas que podemos “gastar” sem violar o SLO. Ele é calculado como `1 – SLO`. Com um SLO de 99,5%, o error budget é de 0,5% do tempo total no período.

**Política:** Enquanto o error budget não for esgotado, a equipe pode realizar deploy de novas funcionalidades, experimentos e mudanças de infraestrutura. Quando o error budget for totalmente consumido (ou seja, a disponibilidade cair abaixo de 99,5% no período atual), as seguintes ações serão obrigatórias:

1. **Congelamento de mudanças:** Nenhum deploy novo será autorizado, exceto correções críticas para restaurar a disponibilidade.
2. **Foco total em estabilidade:** A equipe dedicará 100% do tempo à investigação de incidentes, otimização de performance e correção de bugs que impactam a confiabilidade.
3. **Revisão de post‑mortem:** Cada incidente que contribuiu para o esgotamento do budget será documentado em um post‑mortem detalhado, com ações corretivas atribuídas a um responsável e prazo definido.
4. **Retomada de atividades:** Somente após a disponibilidade retornar ao patamar acima do SLO por pelo menos 72 horas consecutivas a equipe poderá retomar o ciclo normal de desenvolvimento.

Esta política garante que a confiabilidade do sistema seja tratada como uma prioridade máxima, alinhada com a expectativa do cliente de um “tanque de guerra” operacional.

---

*Documento mantido pela equipe de operações. Última atualização: 20/04/2026.*