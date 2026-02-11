using System.Text.Json;
using Xunit;
using NSubstitute;
using MeatSpeak.Server.AdminApi.Methods;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;
using MeatSpeak.Server.Permissions;
using Microsoft.Extensions.Hosting;

namespace MeatSpeak.Server.AdminApi.Tests;

public class AdminMethodTests
{
    private readonly IServer _server;

    public AdminMethodTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig
        {
            ServerName = "test.server",
            Motd = new List<string> { "Welcome!" }
        });
        _server.ConnectionCount.Returns(5);
        _server.ChannelCount.Returns(2);
        _server.StartedAt.Returns(DateTimeOffset.UtcNow.AddHours(-1));
    }

    // Server methods

    [Fact]
    public async Task ServerStats_ReturnsStats()
    {
        var method = new ServerStatsMethod(_server);
        var result = await method.ExecuteAsync(null);
        Assert.NotNull(result);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("\"connections\":5", json);
        Assert.Contains("\"channels\":2", json);
    }

    [Fact]
    public async Task ServerMotdGet_ReturnsMotd()
    {
        var method = new ServerMotdGetMethod(_server);
        var result = await method.ExecuteAsync(null);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("Welcome!", json);
    }

    [Fact]
    public async Task ServerMotdSet_UpdatesMotd()
    {
        var method = new ServerMotdSetMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"motd":["Line 1","Line 2"]}""").RootElement;
        await method.ExecuteAsync(paramsJson);
        Assert.Equal(new List<string> { "Line 1", "Line 2" }, _server.Config.Motd);
    }

    [Fact]
    public async Task ServerShutdown_StopsApplication()
    {
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var method = new ServerShutdownMethod(lifetime);
        var result = await method.ExecuteAsync(null);
        lifetime.Received().StopApplication();
    }

    [Fact]
    public async Task ServerOperSet_HashesPassword()
    {
        var method = new ServerOperSetMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"name":"admin","password":"secret"}""").RootElement;
        await method.ExecuteAsync(paramsJson);
        Assert.Equal("admin", _server.Config.OperName);
        Assert.StartsWith("$argon2id$", _server.Config.OperPassword);
    }

    // User methods

    [Fact]
    public async Task UserList_ReturnsUsers()
    {
        var session = Substitute.For<ISession>();
        session.Id.Returns("s1");
        session.Info.Returns(new SessionInfo { Nickname = "TestUser", Account = "acc1" });
        session.State.Returns(SessionState.Registered);
        _server.Sessions.Returns(new Dictionary<string, ISession> { ["s1"] = session });

        var method = new UserListMethod(_server);
        var result = await method.ExecuteAsync(null);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("TestUser", json);
    }

    [Fact]
    public async Task UserInfo_ReturnsDetails()
    {
        var session = Substitute.For<ISession>();
        session.Id.Returns("s1");
        session.Info.Returns(new SessionInfo
        {
            Nickname = "TestUser",
            Username = "test",
            Hostname = "localhost",
            Account = "acc1"
        });
        _server.FindSessionByNick("TestUser").Returns(session);

        var method = new UserInfoMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"nick":"TestUser"}""").RootElement;
        var result = await method.ExecuteAsync(paramsJson);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("TestUser", json);
        Assert.Contains("localhost", json);
    }

    [Fact]
    public async Task UserInfo_NotFound_ReturnsError()
    {
        _server.FindSessionByNick("ghost").Returns((ISession?)null);

        var method = new UserInfoMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"nick":"ghost"}""").RootElement;
        var result = await method.ExecuteAsync(paramsJson);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("user_not_found", json);
    }

    [Fact]
    public async Task UserMessage_SendsNotice()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo { Nickname = "Target" });
        _server.FindSessionByNick("Target").Returns(session);

        var method = new UserMessageMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"nick":"Target","message":"Hello!"}""").RootElement;
        await method.ExecuteAsync(paramsJson);

        await session.Received().SendMessageAsync("test.server", "NOTICE", "Target", "Hello!");
    }

    // Channel methods

    [Fact]
    public async Task ChannelList_ReturnsChannels()
    {
        var channel = Substitute.For<IChannel>();
        channel.Name.Returns("#test");
        channel.Members.Returns(new Dictionary<string, ChannelMembership>());
        channel.Topic.Returns("Test topic");
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelListMethod(_server);
        var result = await method.ExecuteAsync(null);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("#test", json);
    }

    [Fact]
    public async Task ChannelCreate_CreatesChannel()
    {
        var channel = Substitute.For<IChannel>();
        _server.GetOrCreateChannel("#new").Returns(channel);

        var method = new ChannelCreateMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#new","topic":"New channel"}""").RootElement;
        await method.ExecuteAsync(paramsJson);

        _server.Received().GetOrCreateChannel("#new");
        channel.Received().Topic = "New channel";
    }

    [Fact]
    public async Task ChannelMode_SetsModesCorrectly()
    {
        var modes = new HashSet<char>();
        var channel = Substitute.For<IChannel>();
        channel.Modes.Returns(modes);
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelModeMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test","modes":"+nt"}""").RootElement;
        await method.ExecuteAsync(paramsJson);

        Assert.Contains('n', modes);
        Assert.Contains('t', modes);
    }

    // Ban methods

    [Fact]
    public async Task BanList_ReturnsBans()
    {
        var repo = Substitute.For<IBanRepository>();
        repo.GetAllActiveAsync(Arg.Any<CancellationToken>()).Returns(new List<ServerBanEntity>
        {
            new() { Id = Guid.NewGuid(), Mask = "*!*@bad.host", SetBy = "admin" }
        });

        var method = new BanListMethod(repo);
        var result = await method.ExecuteAsync(null);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("bad.host", json);
    }

    [Fact]
    public async Task BanAdd_CreatesBan()
    {
        var repo = Substitute.For<IBanRepository>();
        var method = new BanAddMethod(repo);
        var paramsJson = JsonDocument.Parse("""{"mask":"*!*@evil.net","reason":"spam","duration":3600}""").RootElement;
        await method.ExecuteAsync(paramsJson);

        await repo.Received().AddAsync(Arg.Is<ServerBanEntity>(b =>
            b.Mask == "*!*@evil.net" && b.Reason == "spam" && b.ExpiresAt.HasValue), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BanRemove_RemovesBan()
    {
        var repo = Substitute.For<IBanRepository>();
        var id = Guid.NewGuid();
        var method = new BanRemoveMethod(repo);
        var paramsJson = JsonDocument.Parse($"{{\"id\":\"{id}\"}}").RootElement;
        await method.ExecuteAsync(paramsJson);

        await repo.Received().RemoveAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BanCheck_ReturnsStatus()
    {
        var repo = Substitute.For<IBanRepository>();
        repo.IsBannedAsync("*!*@bad.host", Arg.Any<CancellationToken>()).Returns(true);

        var method = new BanCheckMethod(repo);
        var paramsJson = JsonDocument.Parse("""{"mask":"*!*@bad.host"}""").RootElement;
        var result = await method.ExecuteAsync(paramsJson);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("true", json);
    }

    // Role methods

    [Fact]
    public async Task RoleList_ReturnsRoles()
    {
        var perms = Substitute.For<IPermissionService>();
        perms.GetAllRolesAsync(Arg.Any<CancellationToken>()).Returns(new List<Role>
        {
            new(Guid.NewGuid(), "Admin", 100, ServerPermission.All, ChannelPermission.All)
        });

        var method = new RoleListMethod(perms);
        var result = await method.ExecuteAsync(null);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("Admin", json);
    }

    [Fact]
    public async Task RoleCreate_CreatesRole()
    {
        var perms = Substitute.For<IPermissionService>();
        perms.CreateRoleAsync("Mod", 50, (ServerPermission)1, (ChannelPermission)1, Arg.Any<CancellationToken>())
            .Returns(new Role(Guid.NewGuid(), "Mod", 50, (ServerPermission)1, (ChannelPermission)1));

        var method = new RoleCreateMethod(perms);
        var paramsJson = JsonDocument.Parse("""{"name":"Mod","position":50,"server_permissions":1,"channel_permissions":1}""").RootElement;
        await method.ExecuteAsync(paramsJson);

        await perms.Received().CreateRoleAsync("Mod", 50, (ServerPermission)1, (ChannelPermission)1, Arg.Any<CancellationToken>());
    }

    // Enhanced channel.create tests

    [Fact]
    public async Task ChannelCreate_WithModes_AppliesModes()
    {
        var modes = new HashSet<char> { 'n', 't' };
        var channel = Substitute.For<IChannel>();
        channel.Modes.Returns(modes);
        _server.GetOrCreateChannel("#new").Returns(channel);

        var method = new ChannelCreateMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#new","modes":"+imV"}""").RootElement;
        await method.ExecuteAsync(paramsJson);

        Assert.Contains('i', modes);
        Assert.Contains('m', modes);
        Assert.Contains('V', modes);
    }

    [Fact]
    public async Task ChannelCreate_WithKey_SetsKey()
    {
        var modes = new HashSet<char> { 'n', 't' };
        var channel = Substitute.For<IChannel>();
        channel.Modes.Returns(modes);
        _server.GetOrCreateChannel("#new").Returns(channel);

        var method = new ChannelCreateMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#new","key":"secret"}""").RootElement;
        await method.ExecuteAsync(paramsJson);

        channel.Received().Key = "secret";
        Assert.Contains('k', modes);
    }

    [Fact]
    public async Task ChannelCreate_WithUserLimit_SetsLimit()
    {
        var modes = new HashSet<char> { 'n', 't' };
        var channel = Substitute.For<IChannel>();
        channel.Modes.Returns(modes);
        _server.GetOrCreateChannel("#new").Returns(channel);

        var method = new ChannelCreateMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#new","user_limit":50}""").RootElement;
        await method.ExecuteAsync(paramsJson);

        channel.Received().UserLimit = 50;
        Assert.Contains('l', modes);
    }

    // Channel ban admin API tests

    [Fact]
    public async Task ChannelBans_ReturnsBanList()
    {
        var channel = Substitute.For<IChannel>();
        channel.Name.Returns("#test");
        channel.Bans.Returns(new List<BanEntry>
        {
            new("*!*@bad.host", "admin", DateTimeOffset.UtcNow)
        });
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelBansMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test"}""").RootElement;
        var result = await method.ExecuteAsync(paramsJson);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("bad.host", json);
    }

    [Fact]
    public async Task ChannelBansAdd_AddsBan()
    {
        var channel = Substitute.For<IChannel>();
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelBansAddMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test","mask":"*!*@evil.host","set_by":"admin"}""").RootElement;
        await method.ExecuteAsync(paramsJson);

        channel.Received().AddBan(Arg.Is<BanEntry>(b => b.Mask == "*!*@evil.host" && b.SetBy == "admin"));
    }

    [Fact]
    public async Task ChannelBansRemove_RemovesBan()
    {
        var channel = Substitute.For<IChannel>();
        channel.RemoveBan("*!*@evil.host").Returns(true);
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelBansRemoveMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test","mask":"*!*@evil.host"}""").RootElement;
        var result = await method.ExecuteAsync(paramsJson);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("ok", json);
    }

    [Fact]
    public async Task ChannelBansRemove_NotFound_ReturnsNotFound()
    {
        var channel = Substitute.For<IChannel>();
        channel.RemoveBan("*!*@missing.host").Returns(false);
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelBansRemoveMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test","mask":"*!*@missing.host"}""").RootElement;
        var result = await method.ExecuteAsync(paramsJson);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("not_found", json);
    }

    // Channel exception admin API tests

    [Fact]
    public async Task ChannelExcepts_ReturnsExceptList()
    {
        var channel = Substitute.For<IChannel>();
        channel.Name.Returns("#test");
        channel.Excepts.Returns(new List<BanEntry>
        {
            new("*!friend@bad.host", "admin", DateTimeOffset.UtcNow)
        });
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelExceptsMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test"}""").RootElement;
        var result = await method.ExecuteAsync(paramsJson);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("friend@bad.host", json);
    }

    [Fact]
    public async Task ChannelExceptsAdd_AddsExcept()
    {
        var channel = Substitute.For<IChannel>();
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelExceptsAddMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test","mask":"*!friend@bad.host"}""").RootElement;
        await method.ExecuteAsync(paramsJson);

        channel.Received().AddExcept(Arg.Is<BanEntry>(e => e.Mask == "*!friend@bad.host"));
    }

    [Fact]
    public async Task ChannelExceptsRemove_RemovesExcept()
    {
        var channel = Substitute.For<IChannel>();
        channel.RemoveExcept("*!friend@bad.host").Returns(true);
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelExceptsRemoveMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test","mask":"*!friend@bad.host"}""").RootElement;
        var result = await method.ExecuteAsync(paramsJson);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("ok", json);
    }

    // Channel member mode admin API tests

    [Fact]
    public async Task ChannelMemberMode_SetsVoice()
    {
        var membership = new ChannelMembership { Nickname = "user1" };
        var channel = Substitute.For<IChannel>();
        channel.GetMember("user1").Returns(membership);
        channel.Members.Returns(new Dictionary<string, ChannelMembership> { ["user1"] = membership });
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelMemberModeMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test","nick":"user1","modes":"+v"}""").RootElement;
        var result = await method.ExecuteAsync(paramsJson);

        Assert.True(membership.HasVoice);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("ok", json);
    }

    [Fact]
    public async Task ChannelMemberMode_SetsOperator()
    {
        var membership = new ChannelMembership { Nickname = "user1" };
        var channel = Substitute.For<IChannel>();
        channel.GetMember("user1").Returns(membership);
        channel.Members.Returns(new Dictionary<string, ChannelMembership> { ["user1"] = membership });
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelMemberModeMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test","nick":"user1","modes":"+o"}""").RootElement;
        var result = await method.ExecuteAsync(paramsJson);

        Assert.True(membership.IsOperator);
    }

    [Fact]
    public async Task ChannelMemberMode_RemovesVoice()
    {
        var membership = new ChannelMembership { Nickname = "user1", HasVoice = true };
        var channel = Substitute.For<IChannel>();
        channel.GetMember("user1").Returns(membership);
        channel.Members.Returns(new Dictionary<string, ChannelMembership> { ["user1"] = membership });
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelMemberModeMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test","nick":"user1","modes":"-v"}""").RootElement;
        await method.ExecuteAsync(paramsJson);

        Assert.False(membership.HasVoice);
    }

    [Fact]
    public async Task ChannelMemberMode_UserNotInChannel_ReturnsError()
    {
        var channel = Substitute.For<IChannel>();
        channel.GetMember("ghost").Returns((ChannelMembership?)null);
        _server.Channels.Returns(new Dictionary<string, IChannel> { ["#test"] = channel });

        var method = new ChannelMemberModeMethod(_server);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test","nick":"ghost","modes":"+v"}""").RootElement;
        var result = await method.ExecuteAsync(paramsJson);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("user_not_in_channel", json);
    }

    // Channel permission admin API tests

    [Fact]
    public async Task ChannelPermissions_ReturnsOverrides()
    {
        var perms = Substitute.For<IPermissionService>();
        var roleId = Guid.NewGuid();
        perms.GetChannelOverridesAsync("#test", Arg.Any<CancellationToken>()).Returns(new List<ChannelOverride>
        {
            new(roleId, "#test", ChannelPermission.SendMessages, ChannelPermission.None)
        });
        perms.GetAllRolesAsync(Arg.Any<CancellationToken>()).Returns(new List<Role>
        {
            new(roleId, "Mod", 50, ServerPermission.None, ChannelPermission.None)
        });

        var method = new ChannelPermissionsMethod(perms);
        var paramsJson = JsonDocument.Parse("""{"channel":"#test"}""").RootElement;
        var result = await method.ExecuteAsync(paramsJson);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("Mod", json);
    }

    [Fact]
    public async Task ChannelPermissionsSet_SetsOverride()
    {
        var perms = Substitute.For<IPermissionService>();
        var roleId = Guid.NewGuid();

        var method = new ChannelPermissionsSetMethod(perms);
        var paramsJson = JsonDocument.Parse($"{{\"channel\":\"#test\",\"role_id\":\"{roleId}\",\"allow\":3,\"deny\":0}}").RootElement;
        await method.ExecuteAsync(paramsJson);

        await perms.Received().SetChannelOverrideAsync(
            Arg.Is<ChannelOverride>(o => o.RoleId == roleId && o.ChannelName == "#test" && o.Allow == (ChannelPermission)3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChannelPermissionsDelete_DeletesOverride()
    {
        var perms = Substitute.For<IPermissionService>();
        var roleId = Guid.NewGuid();

        var method = new ChannelPermissionsDeleteMethod(perms);
        var paramsJson = JsonDocument.Parse($"{{\"channel\":\"#test\",\"role_id\":\"{roleId}\"}}").RootElement;
        await method.ExecuteAsync(paramsJson);

        await perms.Received().DeleteChannelOverrideAsync(roleId, "#test", Arg.Any<CancellationToken>());
    }

    // Audit methods

    [Fact]
    public async Task AuditQuery_ReturnsEntries()
    {
        var repo = Substitute.For<IAuditLogRepository>();
        repo.QueryAsync(null, null, 50, 0, Arg.Any<CancellationToken>()).Returns(new List<AuditLogEntity>
        {
            new() { Action = "ban.add", Actor = "admin-api", Target = "*!*@bad", Timestamp = DateTimeOffset.UtcNow }
        });

        var method = new AuditQueryMethod(repo);
        var result = await method.ExecuteAsync(null);
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("ban.add", json);
    }
}
