namespace MeatSpeak.Server.Data;

using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Configuration;

public class MeatSpeakDbContext : DbContext
{
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<ChannelOverrideEntity> ChannelOverrides => Set<ChannelOverrideEntity>();
    public DbSet<ServerBanEntity> ServerBans => Set<ServerBanEntity>();
    public DbSet<AuditLogEntity> AuditLog => Set<AuditLogEntity>();
    public DbSet<ChannelEntity> Channels => Set<ChannelEntity>();
    public DbSet<TopicHistoryEntity> TopicHistory => Set<TopicHistoryEntity>();
    public DbSet<UserHistoryEntity> UserHistory => Set<UserHistoryEntity>();
    public DbSet<ChatLogEntity> ChatLogs => Set<ChatLogEntity>();

    public MeatSpeakDbContext(DbContextOptions<MeatSpeakDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RoleEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ChannelOverrideEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ServerBanEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ChannelEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TopicHistoryEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserHistoryEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ChatLogEntityConfiguration());
    }
}
