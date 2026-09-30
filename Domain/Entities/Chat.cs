using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Chat
    {
        public Guid Id { get; private set; }
        public string? Name { get; private set; } // Имя нужно только для групповых чатов
        public string? AvatarUrl { get; private set; } // Тоже только для групп
        public bool IsGroup { get; private set; }
        public bool IsPublic { get; private set; }
        public string? OwnerId { get; private set; }
        public DateTime CreatedAt { get; private set; }

        // Навигационное свойство для связи "Многие ко многим" через промежуточную сущность
        public ICollection<ChatMember> Members { get; private set; } = new List<ChatMember>();

        private Chat() { }

        public static Chat CreateDirect()
        {
            return new Chat { Id = Guid.NewGuid(), IsGroup = false, CreatedAt = DateTime.UtcNow };
        }

        public static Chat CreateGroup(string name, string ownerId, bool isPublic = false)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Название группы обязательно.");
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("OwnerId is required.");
            return new Chat { Id = Guid.NewGuid(), Name = name, IsGroup = true, CreatedAt = DateTime.UtcNow, OwnerId = ownerId, IsPublic = isPublic };
        }

        // Any member can rename/re-avatar a group, not just the owner — same
        // authorization the caller (RenameChatCommandHandler etc.) checks via
        // IChatRepository.IsMemberAsync before calling these.
        public void Rename(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Название группы обязательно.");
            Name = name;
        }

        public void SetAvatarUrl(string? avatarUrl)
        {
            AvatarUrl = avatarUrl;
        }

        // Публичность задавалась только при создании группы; админке нужно уметь
        // скрыть или, наоборот, открыть уже существующую.
        public void SetPublic(bool isPublic)
        {
            IsPublic = isPublic;
        }
    }

    public class ChatMember
    {
        public Guid ChatId { get; private set; }
        public string UserId { get; private set; }
        public DateTime JoinedAt { get; private set; }
        public DateTime? LastReadAt { get; private set; }

        private ChatMember() { }

        public static ChatMember Create(Guid chatId, string userId)
        {
            return new ChatMember { ChatId = chatId, UserId = userId, JoinedAt = DateTime.UtcNow };
        }

        public void MarkRead()
        {
            LastReadAt = DateTime.UtcNow;
        }
    }
}
