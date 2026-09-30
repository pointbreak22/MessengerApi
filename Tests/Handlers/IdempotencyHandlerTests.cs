using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Domain.Repositories;
using Application.CQRS.Messages.Commands;
using Domain.Entities;

namespace Tests.Handlers
{
    public class IdempotencyHandlerTests
    {
        [Fact]
        public async Task SendMessageHandler_Returns_Existing_When_IdempotencyFound()
        {
            var existingMessageId = Guid.NewGuid();
            var payload = System.Text.Json.JsonSerializer.Serialize(new { MessageId = existingMessageId, ChatId = Guid.NewGuid(), SenderId = "u", Text = "hi" });
            var outbox = OutboxMessage.Create("MessageCreated", payload, "key-1");

            var outboxMock = new Mock<IOutboxRepository>();
            outboxMock.Setup(r => r.FindByIdempotencyKeyAsync("key-1")).ReturnsAsync(outbox);

            var messageRepoMock = new Mock<Domain.Repositories.IMessageRepository>();

            var handler = new SendMessageCommandHandler(messageRepoMock.Object, outboxMock.Object);

            var result = await handler.Handle(new SendMessageCommand(Guid.NewGuid(), "u", "hi", "key-1"), CancellationToken.None);

            Assert.Equal(existingMessageId, result);
            messageRepoMock.Verify(r => r.AddAsync(It.IsAny<Domain.Entities.Message>()), Times.Never);
        }
    }
}
