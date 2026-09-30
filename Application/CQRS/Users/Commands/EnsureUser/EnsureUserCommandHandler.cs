using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Application.CQRS.Users.DTOs;

namespace Application.CQRS.Users.Commands
{
    public class EnsureUserCommandHandler : IRequestHandler<EnsureUserCommand, UserDto>
    {
        private readonly IUserRepository _users;

        public EnsureUserCommandHandler(IUserRepository users)
        {
            _users = users;
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
                return UserDto.FromEntity(user);
            }

            user = User.Create(request.Id, request.UserName, avatarUrl: null, email: request.Email, role: desiredRole);
            await _users.AddAsync(user);
            return UserDto.FromEntity(user);
        }
    }
}
