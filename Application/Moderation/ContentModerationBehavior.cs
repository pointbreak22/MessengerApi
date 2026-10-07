using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Application.Moderation
{
    /// <summary>
    /// Проверяет тексты любой IModeratedRequest-команды до её обработчика, так что
    /// каждое действие (отправка, редактирование, смена ника, название группы)
    /// проходит через модерацию одним и тем же путём — и по REST, и через хаб.
    /// </summary>
    public sealed class ContentModerationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly IModerationService _moderation;

        public ContentModerationBehavior(IModerationService moderation) => _moderation = moderation;

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (request is IModeratedRequest moderated)
                await _moderation.EnsureCleanAsync(moderated.ModerationActorId, moderated.GetModeratedTexts(), cancellationToken);

            return await next();
        }
    }
}
