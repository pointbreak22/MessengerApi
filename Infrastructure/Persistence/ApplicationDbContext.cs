using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Chat> Chats => Set<Chat>();
        public DbSet<ChatMember> ChatMembers => Set<ChatMember>();
        public DbSet<Friendship> Friendships => Set<Friendship>();
        public DbSet<Message> Messages => Set<Message>(); // Из прошлого шага
        public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();
        public DbSet<Domain.Entities.OutboxMessage> OutboxMessages => Set<Domain.Entities.OutboxMessage>();
        public DbSet<ModerationViolation> ModerationViolations => Set<ModerationViolation>();
        public DbSet<IpBan> IpBans => Set<IpBan>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка составного ключа для участников чата (Чат + Юзер)
            modelBuilder.Entity<ChatMember>()
                .HasKey(cm => new { cm.ChatId, cm.UserId });

            // Индекс на статус дружбы для быстрого поиска списка друзей
            modelBuilder.Entity<Friendship>()
                .HasIndex(f => new { f.UserId, f.FriendId, f.Status });

            // One reaction per user per message — enforced at the DB level too.
            modelBuilder.Entity<MessageReaction>()
                .HasIndex(r => new { r.MessageId, r.UserId })
                .IsUnique();

            // История нарушений в админке читается по пользователю, новые сверху.
            modelBuilder.Entity<ModerationViolation>(e =>
            {
                e.Property(v => v.Content).HasMaxLength(ModerationViolation.MaxContentLength);
                e.Property(v => v.MatchedWords).HasMaxLength(ModerationViolation.MaxMatchedWordsLength);
                e.HasIndex(v => new { v.UserId, v.CreatedAt });
                e.Property(v => v.IpAddress).HasMaxLength(IpBan.MaxIpLength);
                // Автобан IP: сколько разных аккаунтов нарушали с этого адреса.
                e.HasIndex(v => new { v.IpAddress, v.CreatedAt });
            });

            modelBuilder.Entity<User>()
                .Property(u => u.LastIpAddress).HasMaxLength(IpBan.MaxIpLength);

            modelBuilder.Entity<IpBan>(e =>
            {
                e.Property(b => b.IpAddress).HasMaxLength(IpBan.MaxIpLength);
                e.Property(b => b.Reason).HasMaxLength(IpBan.MaxReasonLength);
                e.HasIndex(b => b.IpAddress);
            });
        }
    }
}
