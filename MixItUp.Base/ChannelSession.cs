﻿using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Commands.Games;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Services.Glimesh;
using MixItUp.Base.Services.Trovo;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Services.YouTube;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using StreamingClient.Base.Model.OAuth;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchNewAPI = Twitch.Base.Models.NewAPI;
using TwitchV5API = Twitch.Base.Models.V5;

namespace MixItUp.Base
{
    public static class ChannelSession
    {
        public static ApplicationSettingsV2Model AppSettings { get; private set; }
        public static SettingsV3Model Settings { get; private set; }

        private static CancellationTokenSource sessionBackgroundCancellationTokenSource = new CancellationTokenSource();
        private static int sessionBackgroundTimer = 0;

        public static bool IsDebug()
        {
            #if DEBUG
                return true;
            #else
                return false;
            #endif
        }

        public static bool IsElevated { get; set; }

        public static async Task Initialize(ServicesManagerBase serviceHandler)
        {
            ServiceManager.Add(new SecretsService());

            ServiceManager.Add(new CommandService());
            ServiceManager.Add(new SettingsService());
            ServiceManager.Add(new MixItUpService());
            ServiceManager.Add(new WebhookService(MixItUp.Base.Services.MixItUpService.MixItUpAPIEndpoint, "https://mixitupapi.azurewebsites.net/webhookhub"));
            ServiceManager.Add(new UserService());
            ServiceManager.Add(new ChatService());
            ServiceManager.Add(new EventService());
            ServiceManager.Add(new AlertsService());
            ServiceManager.Add(new StatisticsService());
            ServiceManager.Add(new ModerationService());
            ServiceManager.Add(new TimerService());
            ServiceManager.Add(new GameQueueService());
            ServiceManager.Add(new GiveawayService());
            ServiceManager.Add(new SerialService());
            ServiceManager.Add(new OverlayService());

            ServiceManager.Add(new StreamlabsOBSService());
            ServiceManager.Add(new XSplitService("http://localhost:8211/"));

            ServiceManager.Add(new StreamJarService());
            ServiceManager.Add(new StreamlootsService());
            ServiceManager.Add(new JustGivingService());
            ServiceManager.Add(new TiltifyService());
            ServiceManager.Add(new ExtraLifeService());
            ServiceManager.Add(new IFTTTService());
            ServiceManager.Add(new PatreonService());
            ServiceManager.Add(new DiscordService());
            ServiceManager.Add(new TwitterService());

            ServiceManager.Add(new TwitchSessionService());
            ServiceManager.Add(new TwitchStatusService());

            ServiceManager.Add(new YouTubeSessionService());

            ServiceManager.Add(new GlimeshSessionService());

            ServiceManager.Add(new TrovoSessionService());

            try
            {
                Type mixItUpSecretsType = Type.GetType("MixItUp.Base.MixItUpSecrets");
                if (mixItUpSecretsType != null)
                {
                    ServiceManager.Add((SecretsService)Activator.CreateInstance(mixItUpSecretsType));
                }
            }
            catch (Exception ex) { Logger.Log(ex); }

            ServiceManager.Get<SettingsService>().Initialize();

            ChannelSession.AppSettings = await ApplicationSettingsV2Model.Load();
        }

        public static async Task Close()
        {
            foreach (IExternalService service in ServiceManager.GetAll<IExternalService>())
            {
                await service.Disconnect();
            }

            if (ChannelSession.Settings != null)
            {
                foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.Platforms)
                {
                    if (ChannelSession.Settings.StreamingPlatformAuthentications.ContainsKey(platform) && ChannelSession.Settings.StreamingPlatformAuthentications[platform].GetStreamingPlatformSessionService().IsConnected)
                    {
                        await ChannelSession.Settings.StreamingPlatformAuthentications[platform].GetStreamingPlatformSessionService().CloseUser();
                        await ChannelSession.Settings.StreamingPlatformAuthentications[platform].GetStreamingPlatformSessionService().CloseBot();
                    }
                }
            }
        }

        public static async Task SaveSettings()
        {
            await ServiceManager.Get<SettingsService>().Save(ChannelSession.Settings);
        }

        public static UserViewModel GetCurrentUser()
        {
            // TO-DO: Update UserViewModel so that all platform accounts are combined into the same UserViewModel

            UserViewModel user = null;

            if (ServiceManager.Get<TwitchSessionService>().IsConnected)
            {
                user = ServiceManager.Get<UserService>().GetUserByPlatformID(StreamingPlatformTypeEnum.Twitch, ServiceManager.Get<TwitchSessionService>().UserNewAPI.id);
                if (user == null)
                {
                    user = new UserViewModel(ServiceManager.Get<TwitchSessionService>().UserNewAPI);
                }
            }
            else if (ServiceManager.Get<YouTubeSessionService>().IsConnected)
            {
                user = ServiceManager.Get<UserService>().GetUserByPlatformID(StreamingPlatformTypeEnum.YouTube, ServiceManager.Get<YouTubeSessionService>().Channel.Id);
                if (user == null)
                {
                    user = new UserViewModel(ServiceManager.Get<YouTubeSessionService>().Channel);
                }
            }
            else if (ServiceManager.Get<GlimeshSessionService>().IsConnected)
            {
                user = ServiceManager.Get<UserService>().GetUserByPlatformID(StreamingPlatformTypeEnum.Glimesh, ServiceManager.Get<GlimeshSessionService>().User.id);
                if (user == null)
                {
                    user = new UserViewModel(ServiceManager.Get<GlimeshSessionService>().User);
                }
            }
            else if (ServiceManager.Get<TrovoSessionService>().IsConnected)
            {
                user = ServiceManager.Get<UserService>().GetUserByPlatformID(StreamingPlatformTypeEnum.Trovo, ServiceManager.Get<TrovoSessionService>().User.userId);
                if (user == null)
                {
                    user = new UserViewModel(ServiceManager.Get<TrovoSessionService>().User);
                }
            }

            return user;
        }

        public static void DisconnectionOccurred(string serviceName)
        {
            Logger.Log(serviceName + " Service disconnection occurred");
            GlobalEvents.ServiceDisconnect(serviceName);
        }

        public static void ReconnectionOccurred(string serviceName)
        {
            Logger.Log(serviceName + " Service reconnection successful");
            GlobalEvents.ServiceReconnect(serviceName);
        }

        public static async Task<Result> Connect(SettingsV3Model settings)
        {
            Result result = new Result();
            foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.Platforms)
            {
                if (settings.StreamingPlatformAuthentications.ContainsKey(platform) && settings.StreamingPlatformAuthentications[platform].IsEnabled)
                {
                    result.Combine(await settings.StreamingPlatformAuthentications[platform].GetStreamingPlatformSessionService().Connect(settings));
                }
            }

            if (result.Success)
            {
                ChannelSession.Settings = settings;
            }
            return result;
        }

        public static async Task<Result> InitializeSession()
        {
            if (ChannelSession.Settings == null)
            {
                return new Result("No settings file has been loaded");
            }

            try
            {
                await ServiceManager.Get<SettingsService>().Initialize(ChannelSession.Settings);

                await ServiceManager.Get<ITelemetryService>().Connect();
                ServiceManager.Get<ITelemetryService>().SetUserID(ChannelSession.Settings.TelemetryUserID);

                Result result = new Result();
                foreach (IStreamingPlatformSessionService streamingPlatformSessionService in ServiceManager.GetAll<IStreamingPlatformSessionService>())
                {
                    if (streamingPlatformSessionService.IsConnected)
                    {
                        result.Combine(await streamingPlatformSessionService.InitializeUser(ChannelSession.Settings));
                        result.Combine(await streamingPlatformSessionService.InitializeBot(ChannelSession.Settings));
                    }
                }

                if (!result.Success)
                {
                    return result;
                }

                foreach (IStreamingPlatformSessionService streamingPlatformSessionService in ServiceManager.GetAll<IStreamingPlatformSessionService>())
                {
                    if (streamingPlatformSessionService.IsConnected)
                    {
                        streamingPlatformSessionService.SaveSettings(ChannelSession.Settings);
                    }
                }

                foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.Platforms)
                {
                    if (ChannelSession.Settings.StreamingPlatformAuthentications.ContainsKey(platform) && ChannelSession.Settings.StreamingPlatformAuthentications[platform].GetStreamingPlatformSessionService().IsConnected)
                    {
                        ChannelSession.Settings.StreamingPlatformAuthentications[platform].GetStreamingPlatformSessionService().SaveSettings(ChannelSession.Settings);
                    }
                }

                foreach (SettingsV3Model setting in await ServiceManager.Get<SettingsService>().GetAllSettings())
                {
                    if (ChannelSession.Settings.ID != setting.ID)
                    {
                        foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.Platforms)
                        {
                            if (ChannelSession.Settings.StreamingPlatformAuthentications.ContainsKey(platform) && setting.StreamingPlatformAuthentications.ContainsKey(platform))
                            {
                                if (string.Equals(ChannelSession.Settings.StreamingPlatformAuthentications[platform].ChannelID, setting.StreamingPlatformAuthentications[platform].ChannelID))
                                {
                                    return new Result($"There already exists settings with the same account for {platform}. Please sign in with a different account or re-launch Mix It Up to select those settings from the drop-down.");
                                }
                            }
                        }
                    }
                }

                if (ServiceManager.Get<TwitchSessionService>().IsConnected)
                {
                    ChannelSession.Settings.Name = ServiceManager.Get<TwitchSessionService>()?.UserNewAPI?.display_name;
                }
                else if (ServiceManager.Get<YouTubeSessionService>().IsConnected)
                {
                    ChannelSession.Settings.Name = ServiceManager.Get<YouTubeSessionService>()?.Channel?.Snippet?.Title;
                }
                else if (ServiceManager.Get<GlimeshSessionService>().IsConnected)
                {
                    ChannelSession.Settings.Name = ServiceManager.Get<GlimeshSessionService>()?.User?.displayname;
                }
                else if (ServiceManager.Get<TrovoSessionService>().IsConnected)
                {
                    ChannelSession.Settings.Name = ServiceManager.Get<TrovoSessionService>()?.User?.nickName;
                }
                else
                {
                    ChannelSession.Settings.Name = "Test";
                }

                await ServiceManager.Get<ChatService>().Initialize();
                await ServiceManager.Get<EventService>().Initialize();

                // Connect External Services
                Dictionary<IExternalService, OAuthTokenModel> externalServiceToConnect = new Dictionary<IExternalService, OAuthTokenModel>();
                if (ChannelSession.Settings.StreamlabsOAuthToken != null) { externalServiceToConnect[ServiceManager.Get<StreamlabsService>()] = ChannelSession.Settings.StreamlabsOAuthToken; }
                if (ChannelSession.Settings.StreamElementsOAuthToken != null) { externalServiceToConnect[ServiceManager.Get<StreamElementsService>()] = ChannelSession.Settings.StreamElementsOAuthToken; }
                if (ChannelSession.Settings.StreamJarOAuthToken != null) { externalServiceToConnect[ServiceManager.Get<StreamJarService>()] = ChannelSession.Settings.StreamJarOAuthToken; }
                if (ChannelSession.Settings.TipeeeStreamOAuthToken != null) { externalServiceToConnect[ServiceManager.Get<TipeeeStreamService>()] = ChannelSession.Settings.TipeeeStreamOAuthToken; }
                if (ChannelSession.Settings.TreatStreamOAuthToken != null) { externalServiceToConnect[ServiceManager.Get<TreatStreamService>()] = ChannelSession.Settings.TreatStreamOAuthToken; }
                if (ChannelSession.Settings.StreamlootsOAuthToken != null) { externalServiceToConnect[ServiceManager.Get<StreamlootsService>()] = ChannelSession.Settings.StreamlootsOAuthToken; }
                if (ChannelSession.Settings.TiltifyOAuthToken != null) { externalServiceToConnect[ServiceManager.Get<TiltifyService>()] = ChannelSession.Settings.TiltifyOAuthToken; }
                if (ChannelSession.Settings.JustGivingOAuthToken != null) { externalServiceToConnect[ServiceManager.Get<JustGivingService>()] = ChannelSession.Settings.JustGivingOAuthToken; }
                if (ChannelSession.Settings.IFTTTOAuthToken != null) { externalServiceToConnect[ServiceManager.Get<IFTTTService>()] = ChannelSession.Settings.IFTTTOAuthToken; }
                if (ChannelSession.Settings.ExtraLifeTeamID > 0) { externalServiceToConnect[ServiceManager.Get<ExtraLifeService>()] = new OAuthTokenModel(); }
                if (ChannelSession.Settings.PatreonOAuthToken != null) { externalServiceToConnect[ServiceManager.Get<PatreonService>()] = ChannelSession.Settings.PatreonOAuthToken; }
                if (ChannelSession.Settings.DiscordOAuthToken != null) { externalServiceToConnect[ServiceManager.Get<DiscordService>()] = ChannelSession.Settings.DiscordOAuthToken; }
                if (ChannelSession.Settings.TwitterOAuthToken != null) { externalServiceToConnect[ServiceManager.Get<TwitterService>()] = ChannelSession.Settings.TwitterOAuthToken; }
                if (ServiceManager.Get<IOBSStudioService>().IsEnabled) { externalServiceToConnect[ServiceManager.Get<IOBSStudioService>()] = null; }
                if (ServiceManager.Get<StreamlabsOBSService>().IsEnabled) { externalServiceToConnect[ServiceManager.Get<StreamlabsOBSService>()] = null; }
                if (ServiceManager.Get<XSplitService>().IsEnabled) { externalServiceToConnect[ServiceManager.Get<XSplitService>()] = null; }
                if (!string.IsNullOrEmpty(ChannelSession.Settings.OvrStreamServerIP)) { externalServiceToConnect[ServiceManager.Get<IOvrStreamService>()] = null; }
                if (ChannelSession.Settings.EnableOverlay) { externalServiceToConnect[ServiceManager.Get<OverlayService>()] = null; }
                if (ChannelSession.Settings.EnableDeveloperAPI) { externalServiceToConnect[ServiceManager.Get<IDeveloperAPIService>()] = null; }

                if (externalServiceToConnect.Count > 0)
                {
                    Dictionary<IExternalService, Task<Result>> externalServiceTasks = new Dictionary<IExternalService, Task<Result>>();
                    foreach (var kvp in externalServiceToConnect)
                    {
                        Logger.Log(LogLevel.Debug, "Trying automatic OAuth service connection: " + kvp.Key.Name);

                        try
                        {
                            if (kvp.Key is IOAuthExternalService && kvp.Value != null)
                            {
                                externalServiceTasks[kvp.Key] = ((IOAuthExternalService)kvp.Key).Connect(kvp.Value);
                            }
                            else
                            {
                                externalServiceTasks[kvp.Key] = kvp.Key.Connect();
                            }
                        }
                        catch (Exception sex)
                        {
                            Logger.Log(LogLevel.Error, "Error in external service initial connection: " + kvp.Key.Name);
                            Logger.Log(sex);
                        }
                    }

                    try
                    {
                        await Task.WhenAll(externalServiceTasks.Values);
                    }
                    catch (Exception sex)
                    {
                        Logger.Log(LogLevel.Error, "Error in batch external service connection");
                        Logger.Log(sex);
                    }

                    List<IExternalService> failedServices = new List<IExternalService>();
                    foreach (var kvp in externalServiceTasks)
                    {
                        try
                        {
                            if (kvp.Value.Result != null && !kvp.Value.Result.Success && kvp.Key is IOAuthExternalService)
                            {
                                Logger.Log(LogLevel.Debug, "Automatic OAuth token connection failed, trying manual connection: " + kvp.Key.Name);
                                result = await kvp.Key.Connect();
                                if (!result.Success)
                                {
                                    failedServices.Add(kvp.Key);
                                }
                            }
                        }
                        catch (Exception sex)
                        {
                            Logger.Log(LogLevel.Error, "Error in external service failed re-connection: " + kvp.Key.Name);
                            Logger.Log(sex);
                            failedServices.Add(kvp.Key);
                        }
                    }

                    if (failedServices.Count > 0)
                    {
                        Logger.Log(LogLevel.Debug, "Connection failed for services: " + string.Join(", ", failedServices.Select(s => s.Name)));

                        StringBuilder message = new StringBuilder();
                        message.AppendLine("The following services could not be connected:");
                        message.AppendLine();
                        foreach (IExternalService service in failedServices)
                        {
                            message.AppendLine(" - " + service.Name);
                        }
                        message.AppendLine();
                        message.Append("Please go to the Services page to reconnect them manually.");
                        await DialogHelper.ShowMessage(message.ToString());
                    }
                }

                    try
                    {
                        await ChannelSession.Services.WebhookService.Connect();

                        foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
                        {
                            if (currency.ShouldBeReset())
                            {
                                await currency.Reset();
                            }
                        }

                        if (ChannelSession.Settings.ModerationResetStrikesOnLaunch)
                        {
                            foreach (UserDataModel userData in ChannelSession.Settings.UserData.Values)
                            {
                                if (userData.ModerationStrikes > 0)
                                {
                                    userData.ModerationStrikes = 0;
                                    ChannelSession.Settings.UserData.ManualValueChanged(userData.ID);
                                }
                            }
                        }

                        await ChannelSession.Services.Command.Initialize();
                        await ChannelSession.Services.Timers.Initialize();
                        await ChannelSession.Services.Moderation.Initialize();

                        IEnumerable<TwitchV5API.Users.UserModel> channelEditors = await ChannelSession.TwitchUserConnection.GetV5APIChannelEditors(ChannelSession.TwitchChannelV5);
                        if (channelEditors != null)
                        {
                            foreach (TwitchV5API.Users.UserModel channelEditor in channelEditors)
                            {
                                ChannelSession.TwitchChannelEditorsV5.Add(channelEditor.id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        Logger.Log(LogLevel.Error, "Streamer Services - " + JSONSerializerHelper.SerializeToString(ex));
                        await DialogHelper.ShowMessage(Resources.FailedToInitializeStreamerBasedServices +
                            Environment.NewLine + Environment.NewLine + Resources.ErrorDetailsHeader + " " + ex.Message);
                        return false;
                    }

                await ServiceManager.Get<TimerService>().Initialize();
                await ServiceManager.Get<ModerationService>().Initialize();
                ServiceManager.Get<StatisticsService>().Initialize();

                ServiceManager.Get<IInputService>().HotKeyPressed += InputService_HotKeyPressed;

                foreach (RedemptionStoreProductModel product in ChannelSession.Settings.RedemptionStoreProducts.Values)
                {
                    product.ReplenishAmount();
                }

                foreach (RedemptionStorePurchaseModel purchase in ChannelSession.Settings.RedemptionStorePurchases.ToList())
                {
                    if (purchase.State != RedemptionStorePurchaseRedemptionState.ManualRedeemNeeded)
                    {
                        ChannelSession.Settings.RedemptionStorePurchases.Remove(purchase);
                    }
                }

                await ChannelSession.SaveSettings();
                await ServiceManager.Get<SettingsService>().SaveLocalBackup(ChannelSession.Settings);
                await ServiceManager.Get<SettingsService>().PerformAutomaticBackupIfApplicable(ChannelSession.Settings);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                AsyncRunner.RunAsyncBackground(SessionBackgroundTask, sessionBackgroundCancellationTokenSource.Token, 60000);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                ServiceManager.Get<ITelemetryService>().TrackLogin(ChannelSession.Settings.TelemetryUserID, ServiceManager.Get<TwitchSessionService>().UserNewAPI?.broadcaster_type);

                return new Result();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.Log(LogLevel.Error, "Session Initialization - " + JSONSerializerHelper.SerializeToString(ex));
                return new Result("Failed to get channel information. If this continues, please visit the Mix It Up Discord for assistance." +
                        Environment.NewLine + Environment.NewLine + "Error Details: " + ex.Message);
            }
        }

        public static void DisconnectionOccurred(string serviceName)
        {
            Logger.Log(LogLevel.Error, serviceName + " Service disconnection occurred");
            GlobalEvents.ServiceDisconnect(serviceName);
        }

        public static void ReconnectionOccurred(string serviceName)
        {
            Logger.Log(LogLevel.Error, serviceName + " Service reconnection successful");
            GlobalEvents.ServiceReconnect(serviceName);
        }

        private static async Task SessionBackgroundTask(CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                sessionBackgroundTimer++;

                foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.Platforms)
                {
                    if (ChannelSession.Settings.StreamingPlatformAuthentications.ContainsKey(platform) && ChannelSession.Settings.StreamingPlatformAuthentications[platform].GetStreamingPlatformSessionService().IsConnected)
                    {
                        await ChannelSession.Settings.StreamingPlatformAuthentications[platform].GetStreamingPlatformSessionService().RefreshUser();
                        await ChannelSession.Settings.StreamingPlatformAuthentications[platform].GetStreamingPlatformSessionService().RefreshChannel();
                    }
                }

                if (sessionBackgroundTimer >= 5)
                {
                    await ChannelSession.SaveSettings();
                    sessionBackgroundTimer = 0;

                    if (ServiceManager.Get<TwitchSessionService>().IsConnected && ServiceManager.Get<TwitchSessionService>().StreamIsLive)
                    {
                        try
                        {
                            string type = null;
                            if (ServiceManager.Get<TwitchSessionService>().UserNewAPI.IsPartner())
                            {
                                type = "Partner";
                            }
                            else if (ServiceManager.Get<TwitchSessionService>().UserNewAPI.IsAffiliate())
                            {
                                type = "Affiliate";
                            }
                            ServiceManager.Get<ITelemetryService>().TrackChannelMetrics(type, ServiceManager.Get<TwitchSessionService>().StreamV5.viewers, ServiceManager.Get<ChatService>().AllUsers.Count,
                                ServiceManager.Get<TwitchSessionService>().StreamV5.game, ServiceManager.Get<TwitchSessionService>().ChannelV5.views, ServiceManager.Get<TwitchSessionService>().ChannelV5.followers);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(ex);
                        }
                    }
                }
            }
        }

        private static async void InputService_HotKeyPressed(object sender, HotKey hotKey)
        {
            if (ChannelSession.Settings.HotKeys.ContainsKey(hotKey.ToString()))
            {
                HotKeyConfiguration hotKeyConfiguration = ChannelSession.Settings.HotKeys[hotKey.ToString()];
                await ServiceManager.Get<CommandService>().Queue(hotKeyConfiguration.CommandID);
            }
        }
    }
}