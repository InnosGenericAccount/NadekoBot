using Discord;
using Discord.WebSocket;
using NadekoBot.Core.Services.Database.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services
{
    class ChannelService : INService
    {
        public readonly ConcurrentDictionary<ulong, ChannelSettings> GuildConfigsCache;
        private readonly DiscordSocketClient _client;
        private IReadOnlyCollection<SocketGuild> _guilds;

        const bool Enabled = true;   //TODO pull these out into properties loaded on runtime
        const int maxNumberOfMessages = 10;
        const int Delay = 1; //in minutes
        const int removalFactor = 5; // Delat * removalFactor
        const String rolename = "Peon";

        public ChannelService(DiscordSocketClient client, NadekoBot bot)
        {
            _client = client;
            _guilds = _client.Guilds;

            /*
            GuildConfigsCache = new ConcurrentDictionary<ulong, ChannelSettings>(
            bot.AllGuildConfigs
                    .ToDictionary(g => g.GuildId, ChannelSettings.Create));
                    */
            if (Enabled)
            {
                Start(new CancellationToken());
            }

        }


        private async Task Start(CancellationToken token = default(CancellationToken))
        {
            while (!token.IsCancellationRequested)
            {
                foreach (SocketGuild guild in _guilds)
                {
                    foreach (SocketTextChannel channel in guild.TextChannels)
                    {
                        await this.OpenChannel(channel);
                        await this.CloseChannel(channel);
                    }
                }
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(Delay), token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private Task OpenChannel(SocketTextChannel s)
        {
            var _ = Task.Run(async () =>
            {
                var role = s.Guild.Roles.FirstOrDefault(x => x.Name.Equals(rolename));
                var messages = await s.GetMessagesAsync(maxNumberOfMessages).Flatten();
                var oldestMessage = messages.Last().CreatedAt;

                var tenMinAgo = DateTimeOffset.Now.Subtract(new TimeSpan(0, Delay, 0)).LocalDateTime;
                if (oldestMessage > tenMinAgo && messages.Count() == maxNumberOfMessages)
                {
                    ITextChannel overflowChannel = s.Guild.TextChannels.FirstOrDefault(x => GetName(x.Name).Equals(GetName(s.Name)) && ((OverwritePermissions)x.GetPermissionOverwrite(role)).ReadMessages == PermValue.Deny);
                    OverwritePermissions perms = ((OverwritePermissions)overflowChannel.GetPermissionOverwrite(role));
                    perms = perms.Modify(null, null, null, PermValue.Allow, PermValue.Allow, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
                    await overflowChannel.AddPermissionOverwriteAsync(role, perms);

                    await overflowChannel.SendMessageAsync("This overflow channel was opened due to strain on channel " + s.Name + ". Please continue your conversation here instead."); //TODO  pull these out into localizations
                    await s.SendMessageAsync("Due to strain on this channel, we have opened a new overflow channel: " + overflowChannel.Name + ". Please spread the conversations over the multiple channels");
                }
            });
            return Task.CompletedTask;
        }

        private Task CloseChannel(SocketTextChannel s)
        {
            var _ = Task.Run(async () =>
            {
                var role = s.Guild.Roles.FirstOrDefault(x => x.Name.Equals(rolename));
                var messages = await s.GetMessagesAsync(maxNumberOfMessages).Flatten();
                var latestMessage = messages.First().CreatedAt.LocalDateTime;

                var tenMinAgo = DateTimeOffset.Now.Subtract(new TimeSpan(0, Delay * removalFactor, 0)).LocalDateTime;
                OverwritePermissions perms = ((OverwritePermissions)s.GetPermissionOverwrite(role));
                if (latestMessage < tenMinAgo && GetNumber(s.Name) != 1 && perms.ReadMessages.Equals(PermValue.Allow))
                {
                    await s.SendMessageAsync("Due to inactivity, this channel will close for now. Please move to another channel");
                    perms = perms.Modify(null, null, null, null, PermValue.Deny, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
                    await s.AddPermissionOverwriteAsync(role, perms);
                    Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1));
                        perms = perms.Modify(null, null, null, PermValue.Deny, PermValue.Deny, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
                        await s.AddPermissionOverwriteAsync(role, perms);
                    });
                }
            });
            return Task.CompletedTask;
        }

        private String GetName(String channelName)
        {
            char end = channelName.ElementAt(channelName.Count() - 1);
            return Char.IsNumber(end) ? channelName.Substring(0, channelName.Count() - 1) : channelName;
        }

        private int GetNumber(String channelName)
        {
            char end = channelName.ElementAt(channelName.Count() - 1);
            return Char.IsNumber(end) ? Int32.Parse(channelName.Substring(channelName.Count() - 1)) : 1;
        }



        public class ChannelSettings
        {
            //        public int maxNumberOfMessages { get; set; }
            //      public int AllotedTimeBeforeCountReset { get; set; }
            //    public int AllotedTimeBeforeChannelRemoval { get; set; }

            public static ChannelSettings Create(GuildConfig g) => new ChannelSettings()
            {
                //        maxNumberOfMessages = g.maxNumberOfMessages,
                //       AllotedTimeBeforeCountReset = g.AllotedTimeBeforeCountReset,
                //     AllotedTimeBeforeChannelRemoval = g.AllotedTimeBeforeChannelRemoval
            };
        }

    }
}