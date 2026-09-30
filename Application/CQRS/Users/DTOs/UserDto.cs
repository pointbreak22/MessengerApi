using System;
using Domain.Entities;
using Domain.Enums;

namespace Application.CQRS.Users.DTOs
{
    // Role добавлена, чтобы клиент знал, показывать ли вход в админку; настоящая
    // проверка прав всё равно на сервере (политика "SuperAdmin"). Email нужен для
    // GET /users/me — пользователь видит собственный логин.
    public record UserDto(string Id, string UserName, string? AvatarUrl, bool IsOnline, DateTime LastSeenAt, string? Email, UserRole Role)
    {
        public static UserDto FromEntity(User user) =>
            new(user.Id, user.UserName, user.AvatarUrl, user.IsOnline, user.LastSeenAt, user.Email, user.Role);
    }
}
