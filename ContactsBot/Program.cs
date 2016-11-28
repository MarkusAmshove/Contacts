﻿using Contacts;
using Discord.API;
using Discord.API.Rest;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args) => new Program().RunBot().GetAwaiter().GetResult();

    DiscordSocketClient _client;
    CommandService _commands;
    DependencyMap _map;
    BotConfiguration config;

    public async Task RunBot()
    {
        if (!File.Exists("config.json"))
        {
            Console.Write("Please enter a bot token: ");
            string token = Console.ReadLine();
            config = BotConfiguration.CreateBotConfigWithToken("config.json", token);
        }
        else
            config = BotConfiguration.ProcessBotConfig("config.json");

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            AudioMode = Discord.Audio.AudioMode.Disabled,
            LogLevel = Discord.LogSeverity.Debug
        });
        _commands = new CommandService();
        // Create the dependency map and inject the client and commands service into it
        _map = new DependencyMap();
        _map.Add(_client);
        _map.Add(_commands);

        await ApplyCommands();

        _client.Log += _client_Log; // console info
        _client.MessageReceived += _client_MessageReceived; // filtering and commands

        await _client.LoginAsync(Discord.TokenType.Bot, config.Token);

        await _client.ConnectAsync();
        
        await _client.CurrentUser.ModifyAsync((ModifyCurrentUserParams mod) => 
        {
            mod.Avatar = new Image(new FileStream("Assets/contacts.png", FileMode.Open));
        });

        await _client.SetGame("Helping you C#");

        await Task.Delay(-1);
    }

    private async Task _client_Log(Discord.LogMessage arg)
    {
        Console.WriteLine(arg.ToString());
    }

    private async Task ApplyCommands()
    {
        await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
    } 

    private async Task _client_MessageReceived(SocketMessage msg)
    {
        var message = msg as SocketUserMessage;
        if (message == null) return;

        int argPos = 0;

        if(message.HasCharPrefix(config.PrefixCharacter, ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos))
        {
            var context = new CommandContext(_client, message);

            // Provide the dependency map when executing commands
            var result = await _commands.ExecuteAsync(context, argPos, _map);
            if (!result.IsSuccess)
                await message.Channel.SendMessageAsync(result.ErrorReason);
        }
        else
        {
            var authorAsGuildUser = message.Author as SocketGuildUser;
            if (authorAsGuildUser == null) return;

            var roles = authorAsGuildUser.Guild.Roles;
            string role = roles.First(r => authorAsGuildUser.RoleIds.Any(gr => r.Id == gr)).Name;

            var validRoles = new[] { "Regulars", "Moderators", "Founders" };
            if (!validRoles.Any(v => v == role))
            {
                if (message.Content.Contains("https://discord.gg/"))
                {
                    await message.Channel.DeleteMessagesAsync(new[] { message });
                    var dmChannel = await authorAsGuildUser.CreateDMChannelAsync();
                    await dmChannel.SendMessageAsync("Your Discord invite link was removed. Please ask a staff member or regular to post it.");
                }
            }
        }
    }
}