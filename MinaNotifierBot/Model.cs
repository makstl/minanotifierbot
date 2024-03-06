using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MinaNotifierBot;

namespace Model;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
public class BotDbContext : DbContext
{
    public DbSet<User> User { get; set; }
    public DbSet<UserAddress> UserAddress { get; set; }
    public DbSet<UserMessage> UserMessage { get; set; }
    public DbSet<UserMessageQueue> UserMessageQueue { get; set; }
    public DbSet<LastBlock> LastBlock { get; set; }
    public DbSet<PublicAddress> PublicAddress { get; set; }
    public DbSet<Release> Release { get; set; }
    public DbSet<Coinbase> Coinbase { get; set; }

    public string DbPath { get; }

	public BotDbContext()
	{
        var folder = AppDomain.CurrentDomain.BaseDirectory;
        DbPath = System.IO.Path.Join(folder, "Data", "minabot.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
        modelBuilder.Entity<UserAddress>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();
        modelBuilder.Entity<UserMessage>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();
        modelBuilder.Entity<UserMessageQueue>()
            .Property(e => e.Id)
            .ValueGeneratedOnAdd();
        modelBuilder.Entity<LastBlock>()
            .HasKey(e => e.Height);
        modelBuilder.Entity<PublicAddress>()
            .HasIndex(u => u.Address)
            .IsUnique();
        modelBuilder.Entity<Release>()
            .HasKey(e => e.Id);
        modelBuilder.Entity<Coinbase>()
            .HasKey(e => e.Id);
    }
}


public class User
{
    public long Id { get; set; }
    public string Title { get; set; }
    public string Firstname { get; set; }
    public string Lastname { get; set; }
    public string Username { get; set; }
    public DateTime CreateDate { get; set; }
    public int WhaleAlertThreshold { get; set; }
    public bool Inactive { get; set; }
    public int Explorer { get; set; }
    public bool HideHashTags { get; set; }
    public UserState UserState { get; set; }
    public bool ReleaseNotify { get; set; } = false;
    public int PinnedMessageId { get; set; }
}

public enum UserState
{
    Default = 0,
    Support = 1
}

public class UserAddress
{
    public int Id { get; set; }
    public User User { get; set; }
    public long UserId { get; set; }
    public string Address { get; set; }
    public string Title { get; set; }
    public bool NotifyTransactions { get; set; }
    public bool NotifyDelegations { get; set; }

    public string Link => $"<a href='{Explorer.FromId(User.Explorer).account(Address)}'>{Title}</a>";
    public string HashTag()
    {
        if (String.IsNullOrEmpty(Title))
            return " #" + Address.ShortAddr().Replace("…", "").ToLower();
        var ht = System.Text.RegularExpressions.Regex.Replace(Title.ToLower(), "[^a-zа-я0-9]", "");
        if (ht != "")
            return " #" + ht;
        else
            return " #" + Address.ShortAddr().Replace("…", "").ToLower();
    }
    public bool IsDelegate;
}

public class UserMessage
{
    public long Id { get; set; }
    public User User { get; set; }
    public long UserId { get; set; }
    public DateTime CreateDate { get; set; }
    public string? Text { get; set; }
    public string? CallbackQueryData { get; set; }
    public string? Keyboard { get; set; }
    public bool FromUser { get; set; }
    public int? TelegramMessageId { get; set; }
}

public class UserMessageQueue
{
    public long Id { get; set; }
    public User User { get; set; }
    public long UserId { get; set; }
    public DateTime CreateDate { get; set; }
    public string Text { get; set; }
    public string? Keyboard { get; set; }
}

public class LastBlock
{
	public int Height { get; set; }
    public DateTime ProcessedDate { get; set; }
    public int MessageId { get; set; }
    public string MessageText => $"Last block {Height} processed {ProcessedDate}";
}

public class PublicAddress
{
    public int Id { get; set; }
    public string Address { get; set; }
    public string Title { get; set; }
}

public class Release
{
    public int Id { get; set; }
    public string Tag { get; set; }
    public string Url { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? AnnounceUrl { get; set; }
    public DateTime ReleasedAt { get; set; }
}

public class Coinbase
{
    public int Id { get; set; }
    public string CreatorAccount { get; set; }
    public string CoinbaseReceiverAccount { get; set; }
}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
