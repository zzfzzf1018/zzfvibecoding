---
name: domain-modeling-workflow
description: 'Use when modeling business domains such as healthcare, finance, manufacturing, inventory, orders, planning, logistics, or clinical workflows. Covers entities, value objects, DTOs, domain services, boundaries, data flow, invariants, ubiquitous language, and avoiding anemic or over-engineered models.'
argument-hint: 'Domain, use cases, key entities, workflows, and persistence/API constraints'
---
# Domain Modeling Workflow

## When to Use
- User asks to design or refactor business/domain models.
- Requirements involve real-world concepts, workflows, rules, calculations, or constraints.
- There is confusion between entity, value object, DTO, database record, and API contract.

## Modeling Goals
- Make domain concepts explicit and testable.
- Keep rules close to the data they govern when practical.
- Separate domain models from transport, persistence, and UI concerns.
- Avoid both anemic models and premature enterprise patterns.

## Discovery Questions
Ask only what is needed:
- What are the primary workflows or use cases?
- Which concepts have identity over time?
- Which values are immutable descriptions?
- What invariants must always hold?
- What data comes from external systems?
- What must be persisted or exposed through APIs?

## Classifying Concepts
- Entity: has stable identity and lifecycle, such as Patient, Order, Account, Plan.
- Value object: defined by its values, usually immutable, such as Money, Dose, Address, TimeRange.
- DTO: transport shape for API, storage, interop, or messaging; should not own core behavior.
- Domain service: operation that belongs to the domain but not naturally to one entity.
- Repository/gateway: persistence or external access boundary, not core domain logic.

## Boundaries
- Identify bounded contexts when the same word means different things in different workflows.
- Keep integration models separate from internal domain models when external data is messy.
- Use mapping layers at boundaries instead of leaking transport concerns into domain code.

## Invariants and Validation
- Put always-true rules inside constructors, factories, or methods that mutate state.
- Use explicit result/error types when validation failures are expected user input outcomes.
- Keep cross-aggregate or external consistency rules in application/domain services.
- Avoid duplicating validation rules across UI, API, and domain without a clear source of truth.

## Data Flow
For each workflow, outline:
1. Input DTO or command.
2. Mapping or loading required domain objects.
3. Domain operation.
4. Domain result or event.
5. Mapping to output DTO or persistence.

## Avoiding Anemic Models
- If a rule uses only one entity's data, consider making it a method on that entity.
- If a calculation uses several value objects, consider a domain service or value object method.
- Do not put all rules in application services by default.

## Avoiding Over-Modeling
- Do not introduce aggregates, events, repositories, or factories before they solve a real problem.
- For CRUD-heavy systems, simple models plus clear validation may be enough.
- Keep names understandable to domain experts.

## Output Expectations
Provide:
- Proposed model types and responsibilities.
- Boundaries and DTO/domain separation.
- Important invariants.
- Main workflow data flow.
- Test cases for domain rules.
