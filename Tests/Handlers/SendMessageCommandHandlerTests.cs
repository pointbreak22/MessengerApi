using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Domain.Repositories;
using Domain.Entities;
using Application.CQRS.Messages.Commands;

namespace Tests.Handlers
{
    public class SendMessageCommandHandlerTests
    {
        [Fact]
        public async Task Handle_CreatesAndSavesMessage_ReturnsMessageId()
        {
            // Arrange
            var repoMock = new Mock<IMessageRepository>();
            repoMock.Setup(r => r.AddAsync(It.IsAny<Message>())).Returns(Task.CompletedTask)
                .Verifiable();

            var handler = new SendMessageCommandHandler(repoMock.Object);
            var command = new SendMessageCommand(Guid.NewGuid(), "user-1", "hello");

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.IsType<Guid>(result);
            repoMock.Verify(r => r.AddAsync(It.IsAny<Message>()), Times.Once);
        }
    }
}
