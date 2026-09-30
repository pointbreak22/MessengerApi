Read-models and CQRS projections

This repo includes a plan for read-models (projections) to support fast queries.

Suggestions:
- Create a separate schema (or table set) for read-models, e.g. ChatMessagesProjection with fields: ChatId, MessageId, SenderId, Text, CreatedAt, IsRead.
- Update projections from domain events (Outbox or domain events) in a dedicated handler/service that listens to events and updates projections.
- Projections should be optimized for queries used by the frontend (pagination by createdAt, search by user, etc.).

Implementation stub:
- Add Infrastructure/Projections and Application/Projections handlers.
- Ensure projections are updated in the same transaction as publishing events or via outbox + projection worker.
