# ADR-0001: Clean Architecture + DDD

## Status

Accepted

## Context

O TimeTrack é um sistema distribuído com múltiplos componentes:
- **Agent**: Windows Service para rastreamento de tempo
- **Desktop Host**: Container WebView2 para UI React
- **Backend**: API ASP.NET Core em cloud
- **UI**: React com Zustand

Precisávamos de uma arquitetura que:
- Mantivesse o código organizado e testável
- Permitisse evolução independente de cada componente
- Facilitasse a troca de implementações (ex: banco de dados)
- Suportasse regras de negócio complexas (tracking, focus mode, policies)

## Decision

Adotamos **Clean Architecture** combinada com **Domain-Driven Design (DDD)**:

```
┌─────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                        │
│         (React UI / WebView2 / ASP.NET Controllers)         │
├─────────────────────────────────────────────────────────────┤
│                   APPLICATION LAYER                          │
│              (Use Cases / Application Services)              │
├─────────────────────────────────────────────────────────────┤
│                     DOMAIN LAYER                             │
│       (Entities / Value Objects / Aggregates / Rules)        │
├─────────────────────────────────────────────────────────────┤
│                  INFRASTRUCTURE LAYER                        │
│    (Adapters / Repositories / External Services / DB)        │
└─────────────────────────────────────────────────────────────┘
```

### Estrutura de Projetos

**Agent:**
- `TimeTrack.Agent.Domain` - Entidades, Value Objects, regras de negócio
- `TimeTrack.Agent.Application` - Use Cases, interfaces de infraestrutura
- `TimeTrack.Agent.Infrastructure` - Implementações (SQLite, Windows APIs)
- `TimeTrack.AgentService` - Windows Service host

**Backend:**
- `TimeTrack.Backend.Domain` - Entidades, Value Objects
- `TimeTrack.Backend.Application` - Use Cases, MediatR handlers
- `TimeTrack.Backend.Infrastructure` - EF Core, PostgreSQL, serviços externos
- `TimeTrack.Api` - Controllers, middleware

### Princípios Aplicados

- **Dependency Inversion**: Camadas internas não dependem de externas
- **Interface Segregation**: Interfaces específicas para cada necessidade
- **Single Responsibility**: Cada classe tem uma única responsabilidade

## Consequences

### Positivas

- **Testabilidade**: Regras de negócio isoladas e fáceis de testar
- **Manutenibilidade**: Mudanças em infraestrutura não afetam domínio
- **Flexibilidade**: Possível trocar SQLite por outro banco no Agent
- **Clareza**: Limites bem definidos entre componentes

### Negativas

- **Complexidade inicial**: Mais projetos e abstrações
- **Curva de aprendizado**: Desenvolvedores precisam entender DDD
- **Boilerplate**: Mais código para criar use cases e interfaces

### Mitigações

- Templates e exemplos de código para novos use cases
- Documentação clara da arquitetura
- Code reviews focados em manter limites arquiteturais

## References

- [Clean Architecture - Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Domain-Driven Design - Eric Evans](https://www.domainlanguage.com/ddd/)
