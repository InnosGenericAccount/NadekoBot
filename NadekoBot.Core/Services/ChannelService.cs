using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using NadekoBot.Common.Attributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Core.Services
{
    class ChannelService : INService
    {
        private readonly DiscordSocketClient _client;
        private Dictionary<ulong, ChannelStats> channels = new Dictionary<ulong, ChannelStats>();
        private IReadOnlyCollection<SocketGuild> _guilds;

        class ChannelStats
        {
            private DateTimeOffset time = new DateTimeOffset();
            private int count = 0;

            public ChannelStats(DateTimeOffset time, int count)
            {
                this.Time = time;
                this.Count = count;
            }

            public DateTimeOffset Time { get => time; set => time = value; }
            public int Count { get => count; set => count = value; }
        }

        public ChannelService(DiscordSocketClient client, NadekoBot bot)
        {
            _client = client;
            _client.MessageReceived += CreateChannel;
            _guilds = _client.Guilds;
            foreach (SocketGuildChannel sgc in client.Guilds.ElementAt(0).Channels)
            {
                if (!(sgc is SocketTextChannel))
                {
                    continue;
                }

                channels.Add(sgc.Id, new ChannelStats(sgc.CreatedAt, 0));
            }

        }

        private Task CreateChannel(SocketMessage message)
        {
            String name = GetChannel(message.Channel.Name);
            ChannelStats channel = channels.GetValueOrDefault(message.Channel.Id);
            channel.Count++;
            channel.Time = message.CreatedAt;
            int nextNumber = getNextNumber(message.Channel.Name);

            if (channel.Count > 10)
            {
                var _ = Task.Run(async () =>
                {
                    RestTextChannel newc = await _client.Guilds.ElementAt(0).CreateTextChannelAsync(name + nextNumber).ConfigureAwait(false);
                    channels.Add(newc.Id, new ChannelStats(message.CreatedAt, 0));
                    await newc.SendMessageAsync("This channel was created due to strain on channel "+ message.Channel.Name +". Please continue your conversation here instead. Praise Aqua!");
                    await message.Channel.SendMessageAsync("Due to strain on this channel, we have created a new channel: " + newc.Name + ". Please spread the conversations over multiple channels");
                });
                channel.Count = 0;
            }

            return Task.CompletedTask;
        }

        private Task RemoveChannel(SocketMessage message)
        {
            var _ = Task.Run(async () =>
            {
                int number = 1;
                ulong key = 0;
                foreach (SocketTextChannel stc in _guilds.ElementAt(0).TextChannels)
                {
                    var anHourAgo = DateTimeOffset.Now.Subtract(new TimeSpan(0, 5, 0)).LocalDateTime;
                    if (channels.GetValueOrDefault(stc.Id).Count == 0 && channels.GetValueOrDefault(stc.Id).Time < anHourAgo)
                    {
                        number = GetNumber(stc.Name);
                        key = stc.Id;
                        break;
                    }
                }
                if (number != 1 && key != 0)
                {
                    await _client.Guilds.ElementAt(0).GetChannel(key).DeleteAsync();
                    channels.Remove(key);
                }
            });

            return Task.CompletedTask;
        }

        private String GetChannel(String channelName)
        { 
            char end = channelName.ElementAt(channelName.Count() - 1);
            return Char.IsNumber(end) ? channelName.Substring(0, channelName.Count() - 1) : channelName;
        }

        private int GetNumber(String channelName)
        {
            char end = channelName.ElementAt(channelName.Count() - 1);
            return Char.IsNumber(end) ? Int32.Parse(channelName.Substring(channelName.Count() - 1)) : 1;
        }

        private int getNextNumber(String channelName)
        {
            var guild = _guilds.ElementAt(0);
            var channels = guild.TextChannels;

            var name = GetChannel(channelName);

            List <int> numbers = new List<int>();
            foreach (SocketTextChannel cl in channels)
            {
                if (cl.Name.Equals(name))
                {
                    numbers.Add(1);
                }
                else if (cl.Name.StartsWith(name))
                {
                    numbers.Add(GetNumber(cl.Name));
                }
            }
            numbers.Sort();

            int nextNumber = 1;
            for (int i = 0; i < numbers.Count; i++)
            {
                if (numbers[i] > nextNumber)
                {
                    return nextNumber;
                }
                nextNumber++;
            }
            return nextNumber;
        }
    }
}
