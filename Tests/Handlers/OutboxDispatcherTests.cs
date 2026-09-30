using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using Domain.Repositories;
using WebAPI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using WebAPI.Hubs;

namespace Tests.Handlers
{
    public class OutboxDispatcherTests
    {
        [Fact]
        public async Task Dispatcher_Sends_Pending_Outbox_Then_Marks_Sent()
        {
            var chatId = Guid.NewGuid();
            var outboxItem = Domain.Entities.OutboxMessage.Create("MessageCreated", System.Text.Json.JsonSerializer.Serialize(new { MessageId = Guid.NewGuid(), ChatId = chatId, SenderId = "u", Text = "hi" }));

            var repoMock = new Mock<IOutboxRepository>();
            repoMock.Setup(r => r.GetPendingAsync(It.IsAny<int>())).ReturnsAsync(new List<Domain.Entities.OutboxMessage> { outboxItem });
            repoMock.Setup(r => r.MarkSentAsync(It.IsAny<Domain.Entities.OutboxMessage>())).Returns(Task.CompletedTask).Verifiable();

            // Members drive who NewMessage gets pushed to now (Clients.Users), not the
            // ad-hoc JoinChatRoom group — see OutboxDispatcher for why.
            var chat = Domain.Entities.Chat.CreateDirect();
            chat.Members.Add(Domain.Entities.ChatMember.Create(chat.Id, "u"));
            chat.Members.Add(Domain.Entities.ChatMember.Create(chat.Id, "u2"));
            var chatRepoMock = new Mock<IChatRepository>();
            chatRepoMock.Setup(c => c.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(chat);

            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IOutboxRepository))).Returns(repoMock.Object);
            serviceProviderMock.Setup(sp => sp.GetService(typeof(IChatRepository))).Returns(chatRepoMock.Object);

            var scopeMock = new Mock<IServiceScope>();
            scopeMock.SetupGet(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            var clientsMock = new Mock<IHubClients>();
            var clientProxy = new Mock<IClientProxy>();
            clientProxy.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxy.Object);
            clientsMock.Setup(c => c.Users(It.IsAny<IReadOnlyList<string>>())).Returns(clientProxy.Object);

            var hubContextMock = new Mock<IHubContext<ChatHub>>();
            hubContextMock.SetupGet(h => h.Clients).Returns(clientsMock.Object);

            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<OutboxDispatcher>>();

            // A fresh, un-signalled OutboxSignal: the dispatcher does one pass
            // before it ever waits, so this test exercises the same path as
            // before and simply falls through to the poll timeout afterwards.
            var dispatcher = new OutboxDispatcher(scopeFactoryMock.Object, hubContextMock.Object, loggerMock.Object, new OutboxSignal());

            using var cts = new CancellationTokenSource(1000);
            await dispatcher.StartAsync(cts.Token);
            await Task.Delay(200, CancellationToken.None);
            await dispatcher.StopAsync(CancellationToken.None);

            repoMock.Verify(r => r.MarkSentAsync(It.IsAny<Domain.Entities.OutboxMessage>()), Times.AtLeastOnce);
            clientProxy.Verify(c => c.SendCoreAsync("NewMessage", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }
    }
}
