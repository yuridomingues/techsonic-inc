# Planejamento de Operação e Qualidade

## 1. Matriz de Riscos
| Risco | Probabilidade | Impacto | Gatilho (Trigger) | Ação de Mitigação |
| :--- | :--- | :--- | :--- | :--- |
| SQL Injection | Baixa | Altíssimo | Tentativa de input com caracteres suspeitos | Uso obrigatório de Dapper com parâmetros @ |
| Esgotamento de Ingressos | Alta | Médio | Quantidade de ingressos chegar a 0 | Bloquear novas tentativas de compra para o evento |

## 2. Métricas Operacionais
- **Métrica:** Tempo de resposta da API na compra.
- **Fórmula:** (Soma do tempo das requisições) / (Total de requisições).
- **Fonte de Dados:** Logs da aplicação.
- **Frequência:** Diária.
- **Ação se violado:** Otimizar as consultas SQL manuais no Dapper.

## 3. Objetivo de Serviço (SLO)
- **SLO:** 99.5% de disponibilidade em uma janela de 24 horas.

## 4. Error Budget Policy
Se o orçamento de erro (0.5%) for ultrapassado, a equipe deve suspender o desenvolvimento de novas funcionalidades e focar exclusivamente na estabilidade e correção de bugs do motor de vendas.
