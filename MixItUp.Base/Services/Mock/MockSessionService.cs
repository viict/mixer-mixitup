﻿using MixItUp.Base.Model;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Util;
using StreamingClient.Base.Model.OAuth;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Twitch.Base.Models.NewAPI.Users;

namespace MixItUp.Base.Services.Mock
{
    public class MockSessionService : TwitchSessionService, IStreamingPlatformSessionService
    {
        public static UserModel user = new UserModel()
        {
            id = "1",
            login = "testuser",
            display_name = "TestUser",
            broadcaster_type = "partner",
            profile_image_url = "https://github.com/SaviorXTanren/mixer-mixitup/raw/master/Branding/MixItUp-Logo-Base-WhiteXS.png",
        };

        public static UserModel bot = new UserModel()
        {
            id = "2",
            login = "testbot",
            display_name = "TestBot",
            broadcaster_type = "partner",
            profile_image_url = "https://github.com/SaviorXTanren/mixer-mixitup/raw/master/Branding/MixItUp-Logo-Base-WhiteXS.png",
        };

        public new MockPlatformService UserConnection { get; private set; }
        public new MockPlatformService BotConnection { get; private set; }

        public new bool IsConnected { get { return this.UserConnection != null; } }
        public new bool IsBotConnected { get { return this.BotConnection != null; } }

        public new string UserID { get { return user.id; } }
        public new string Username { get { return user.login; } }
        public new string BotID { get { return bot.id; } }
        public new string Botname { get { return bot.login; } }
        public new string ChannelID { get { return user.id; } }
        public new string ChannelLink { get { return "https://mixitupapp.com"; } }

        public new StreamingPlatformAccountModel UserAccount
        {
            get
            {
                return new StreamingPlatformAccountModel()
                {
                    ID = this.UserID,
                    Username = this.Username,
                    AvatarURL = user.profile_image_url,
                };
            }
        }
        public new StreamingPlatformAccountModel BotAccount
        {
            get
            {
                return new StreamingPlatformAccountModel()
                {
                    ID = this.BotID,
                    Username = this.Botname,
                    AvatarURL = bot.profile_image_url,
                };
            }
        }

        public new bool IsLive { get { return true; } }

        public new async Task<Result> ConnectUser()
        {
            Result<MockPlatformService> result = await MockPlatformService.ConnectUser();
            if (result.Success)
            {
                this.UserConnection = result.Value;
            }
            return result;
        }

        public new async Task<Result> ConnectBot()
        {
            Result<MockPlatformService> result = await MockPlatformService.ConnectBot();
            if (result.Success)
            {
                this.BotConnection = result.Value;
            }
            return result;
        }

        public new async Task<Result> Connect(SettingsV3Model settings)
        {
            if (settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].IsEnabled)
            {
                Result userResult = null;

                Result<MockPlatformService> mockResult = await MockPlatformService.Connect(settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].UserOAuthToken);
                if (mockResult.Success)
                {
                    this.UserConnection = mockResult.Value;
                    userResult = mockResult;
                }
                else
                {
                    userResult = await this.ConnectUser();
                }

                if (userResult.Success)
                {
                    if (settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].BotOAuthToken != null)
                    {
                        mockResult = await MockPlatformService.Connect(settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].BotOAuthToken);
                        if (mockResult.Success)
                        {
                            this.BotConnection = mockResult.Value;
                        }
                        else
                        {
                            return new Result(success: true, message: MixItUp.Base.Resources.ErrorHeader);
                        }
                    }
                }
                else
                {
                    settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].ClearUserData();
                    return userResult;
                }

                return userResult;
            }
            return new Result();
        }

        public new async Task DisconnectUser(SettingsV3Model settings)
        {
            await this.DisconnectBot(settings);

            await ServiceManager.Get<MockChatService>().DisconnectUser();

            this.UserConnection = null;

            if (settings.StreamingPlatformAuthentications.TryGetValue(StreamingPlatformTypeEnum.Twitch, out var streamingPlatform))
            {
                streamingPlatform.ClearUserData();
            }
        }

        public new async Task DisconnectBot(SettingsV3Model settings)
        {
            await ServiceManager.Get<MockChatService>().DisconnectBot();

            this.BotConnection = null;

            if (settings.StreamingPlatformAuthentications.TryGetValue(StreamingPlatformTypeEnum.Twitch, out var streamingPlatform))
            {
                streamingPlatform.ClearBotData();
            }
        }

        public new async Task<Result> InitializeUser(SettingsV3Model settings)
        {
            if (this.UserConnection != null)
            {
                try
                {
                    List<Task<Result>> platformServiceTasks = new List<Task<Result>>();
                    platformServiceTasks.Add(ServiceManager.Get<MockChatService>().ConnectUser());

                    await Task.WhenAll(platformServiceTasks);

                    if (platformServiceTasks.Any(c => !c.Result.Success))
                    {
                        string errors = string.Join(Environment.NewLine, platformServiceTasks.Where(c => !c.Result.Success).Select(c => c.Result.Message));
                        return new Result(MixItUp.Base.Resources.GlimeshFailedToConnectHeader + Environment.NewLine + Environment.NewLine + errors);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    return new Result(MixItUp.Base.Resources.ErrorHeader +
                        Environment.NewLine + Environment.NewLine + MixItUp.Base.Resources.ErrorHeader + ex.Message);
                }
            }
            return new Result();
        }

        public new async Task<Result> InitializeBot(SettingsV3Model settings)
        {
            if (this.BotConnection != null)
            {
                Result result = await ServiceManager.Get<MockChatService>().ConnectBot();
                if (!result.Success)
                {
                    return result;
                }
            }
            return new Result();
        }

        public new async Task CloseUser()
        {
            await ServiceManager.Get<MockChatService>().DisconnectUser();
        }

        public new async Task CloseBot()
        {
            await ServiceManager.Get<MockChatService>().DisconnectBot();
        }

        public new void SaveSettings(SettingsV3Model settings)
        {
            if (this.UserConnection != null)
            {
                if (!settings.StreamingPlatformAuthentications.ContainsKey(StreamingPlatformTypeEnum.Twitch))
                {
                    settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch] = new StreamingPlatformAuthenticationSettingsModel(StreamingPlatformTypeEnum.Twitch);
                }

                settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].UserOAuthToken = new OAuthTokenModel();
                settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].UserID = this.UserID;
                settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].ChannelID = this.ChannelID;

                if (this.BotConnection != null)
                {
                    settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].BotOAuthToken = new OAuthTokenModel();
                    settings.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].BotID = this.BotID;
                }
            }
        }

        public new Task RefreshUser()
        {
            return Task.CompletedTask;
        }

        public new Task RefreshChannel()
        {
            return Task.CompletedTask;
        }

        public new Task<string> GetTitle()
        {
            return Task.FromResult("Test Title");
        }

        public new Task<bool> SetTitle(string title) { return Task.FromResult(false); }

        public new Task<string> GetGame()
        {
            return Task.FromResult("Test Game");
        }

        public new Task<bool> SetGame(string gameName) { return Task.FromResult(false); }
    }
}
