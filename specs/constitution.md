# Constituição do projeto

## Stack
- .NET 10, C#, Windows Program
- Persistência em banco local Sqlite
- Metodologia DDD
- Testes Unitário

## Arquitetura
- Somente as services devem fazer alterações ou leituras no banco
- Regra de negócio sempre nas services
- Toda model deve ter sua própria service
- Qualquer edição do modelo deve ser fechado e usado somente com DDD, respeitando Open-Closed principle

## Qualidade
- Toda regra de negócio precisa ter teste unitário.
- Qualquer erro deve ser tratado e exibido de forma amigável ao cliente, porém registrando logs de falha em arquivo sem informações sensíveis.

## Convenções
- Nomes de classe e arquivos em inglês, mensagens de erros e logs em pt-BR
- Commits sempre em pt-BR no formato "tipo de demanda - descrição - requisito"

## Governança
- Se o plano contradiz a constituição, quem muda é o plano
- Qualquer alteração no plano deve ter commit separado para auditoria