using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Application.CQRS.Users.DTOs;
using Application.Moderation;

namespace Application.CQRS.Users.Commands
{
    public class EnsureUserCommandHandler : IRequestHandler<EnsureUserCommand, UserDto>
    {
        private readonly IUserRepository _users;
        private readonly IContentFilter? _filter;
        private readonly IModerationService? _moderation;

        public EnsureUserCommandHandler(IUserRepository users, IContentFilter? filter = null, IModerationService? moderation = null)
        {
            _users = users;
            _filter = filter;
            _moderation = moderation;
        }

        public async Task<UserDto> Handle(EnsureUserCommand request, CancellationToken cancellationToken)
        {
            var desiredRole = request.IsSuperAdmin ? UserRole.SuperAdmin : UserRole.User;

            var user = await _users.GetByIdAsync(request.Id);
            if (user != null)
            {
                // Роль и email переприменяются при каждом входе — это и есть механизм
                // назначения суперадмина: достаточно указать почту в конфигурации и
                // войти, править БД руками не нужно. Обратная сторона симметрична:
                // убранная из конфигурации почта теряет права при следующем входе.
                var changed = false;

                if (user.Role != desiredRole)
                {
                    user.SetRole(desiredRole);
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(request.Email) && user.Email != request.Email)
                {
                    user.SetEmail(request.Email);
                    changed = true;
                }

                if (changed) await _users.UpdateAsync(user);
                await ModerateUserNameAsync(user, cancellationToken);
                return UserDto.FromEntity(user);
            }

            user = User.Create(request.Id, request.UserName, avatarUrl: null, email: request.Email, role: desiredRole);
            await _users.AddAsync(user);
            await ModerateUserNameAsync(user, cancellationToken);
            return UserDto.FromEntity(user);
        }

        // Ник приходит из токена при регистрации (или остался с тех пор, как
        // фильтра ещё не было). Вход из-за этого не блокируем: ник заменяется на
        // приличный вариант, а нарушение засчитывается как обычно — с письмом.
        private async Task ModerateUserNameAsync(User user, CancellationToken cancellationToken)
        {
            if (_filter == null || _moderation == null) return;

            var check = _filter.Check(user.UserName);
            if (check.IsClean) return;

            var original = user.UserName;
            var replacement = check.Suggestion;
            if (string.IsNullOrWhiteSpace(replacement) || !_filter.Check(replacement).IsClean)
                replacement = $"User-{user.Id[..System.Math.Min(8, user.Id.Length)]}";

            user.UpdateProfile(replacement);
            // Сохраняем сразу, не полагаясь на PenalizeAsync: для уже забаненного
            // он в БД не ходит, а переименование нужно в любом случае.
            await _users.UpdateAsync(user);
            await _moderation.PenalizeAsync(user, ModerationTarget.UserName, "вход с таким ником (ник заменён на «" + replacement + "»)", original, check, cancellationToken);
        }
    }
}
