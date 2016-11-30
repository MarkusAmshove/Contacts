﻿using ContactsBot.Modules;
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

namespace ContactsBot
{
    class Program
    {
        static void Main(string[] args) => new Program().RunBot().GetAwaiter().GetResult();

        DiscordSocketClient _client;
        DependencyMap _map;
        BotConfiguration _config;
        CommandHandler _handler;

        public async Task RunBot()
        {
            if (!File.Exists("config.json"))
            {
                Console.Write("Please enter a bot token: ");
                string token = Console.ReadLine();
                _config = BotConfiguration.CreateBotConfigWithToken("config.json", token);
            }
            else
            {
                _config = BotConfiguration.ProcessBotConfig("config.json");
#if DEV
                if (string.IsNullOrWhiteSpace(_config.DevToken))
                {
                    Console.Write("Please enter a dev token: ");
                    string token = Console.ReadLine();
                    _config.DevToken = token;
                    _config.SaveBotConfig("config.json");
#endif
                }
            }

            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                AudioMode = Discord.Audio.AudioMode.Disabled,
                LogLevel = Discord.LogSeverity.Debug
            });

            // Create the dependency map and inject the client and config into it
            _map = new DependencyMap();
            _map.Add(_client);
            _map.Add(_config);

            _handler = new CommandHandler();
            await _handler.Install(_map);

            AddAction<AntiAdvertisement>(_map);
            AddAction<Unflip>(_map);
            
            _client.Log += _client_Log; // console info

#if DEV
            await _client.LoginAsync(Discord.TokenType.Bot, _config.DevToken);
#else
            await _client.LoginAsync(Discord.TokenType.Bot, _config.Token);
#endif
            await _client.ConnectAsync();

            await _client.SetGame("Helping you C#");

            await Task.Delay(-1);
        }

        private Task _client_Log(Discord.LogMessage arg)
        {
            switch (arg.Severity)
            {
                case Discord.LogSeverity.Error: Console.ForegroundColor = ConsoleColor.Red; break;
                case Discord.LogSeverity.Warning: Console.ForegroundColor = ConsoleColor.Yellow; break;
            }
            Console.WriteLine(arg.ToString());
            Console.ForegroundColor = ConsoleColor.Gray;
            return Task.CompletedTask;
        }

        private void AddAction<T>(IDependencyMap map, bool autoEnable = true) where T : IMessageAction, new()
        {
            T action = new T();
            action.Install(map);
            if (autoEnable) action.Enable();
            Global.MessageActions.Add(action.GetType().Name, action);
        }
    }
}