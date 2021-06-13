﻿using MixItUp.Base.Model;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.Twitch;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using StreamingClient.Base.Util;
using StreamingClient.Base.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Twitch.Base.Clients;
using Twitch.Base.Models.Clients.Chat;
using Twitch.Base.Models.NewAPI.Bits;
using Twitch.Base.Models.NewAPI.Chat;
using Twitch.Base.Models.NewAPI.Users;
using Twitch.Base.Models.V5.Emotes;
using TwitchNewAPI = Twitch.Base.Models.NewAPI;

namespace MixItUp.Base.Services.Twitch
{
    public class BetterTTVEmoteModel
    {
        public string id { get; set; }
        public string channel { get; set; }
        public string code { get; set; }
        public string imageType { get; set; }

        public string url { get { return string.Format("https://cdn.betterttv.net/emote/{0}/1x", this.id); } }
    }

    public class FrankerFaceZEmoteModel
    {
        public string id { get; set; }
        public string name { get; set; }
        public JObject urls { get; set; }

        public string url
        {
            get
            {
                if (this.urls != null && this.urls.Count > 0)
                {
                    return "https:" + (this.urls.ContainsKey("2") ? this.urls["2"].ToString() : this.urls[this.urls.GetKeys().First()].ToString());
                }
                return string.Empty;
            }
        }
    }

    public class TwitchChatService : StreamingPlatformServiceBase
    {
        private static List<string> ExcludedDiagnosticPacketLogging = new List<string>() { "PING", ChatMessagePacketModel.CommandID, ChatUserJoinPacketModel.CommandID, ChatUserLeavePacketModel.CommandID };

        private const string HostChatMessageRegexPattern = "^\\w+ is now hosting you.$";

        private const string RaidUserNoticeMessageTypeID = "raid";
        private const string SubMysteryGiftUserNoticeMessageTypeID = "submysterygift";
        private const string SubGiftPaidUpgradeUserNoticeMessageTypeID = "giftpaidupgrade";

        private const int MaxMessageLength = 400;

        public IDictionary<string, EmoteModel> Emotes { get { return this.emotes; } }
        private Dictionary<string, EmoteModel> emotes = new Dictionary<string, EmoteModel>();

        public IDictionary<string, BetterTTVEmoteModel> BetterTTVEmotes { get { return this.betterTTVEmotes; } }
        private Dictionary<string, BetterTTVEmoteModel> betterTTVEmotes = new Dictionary<string, BetterTTVEmoteModel>();

        public IDictionary<string, FrankerFaceZEmoteModel> FrankerFaceZEmotes { get { return this.frankerFaceZEmotes; } }
        private Dictionary<string, FrankerFaceZEmoteModel> frankerFaceZEmotes = new Dictionary<string, FrankerFaceZEmoteModel>();

        public IDictionary<string, ChatBadgeSetModel> ChatBadges { get { return this.chatBadges; } }
        private Dictionary<string, ChatBadgeSetModel> chatBadges = new Dictionary<string, ChatBadgeSetModel>();

        public IEnumerable<TwitchBitsCheermoteViewModel> BitsCheermotes { get { return this.bitsCheermotes.ToList(); } }
        private List<TwitchBitsCheermoteViewModel> bitsCheermotes = new List<TwitchBitsCheermoteViewModel>();

        private ChatClient userClient;
        private ChatClient botClient;

        private CancellationTokenSource cancellationTokenSource;

        private const int userJoinLeaveEventsTotalToProcess = 25;
        private SemaphoreSlim userJoinLeaveEventsSemaphore = new SemaphoreSlim(1);
        private HashSet<string> userJoinEvents = new HashSet<string>();
        private HashSet<string> userLeaveEvents = new HashSet<string>();

        private List<string> initialUserLogins = new List<string>();

        private SemaphoreSlim messageSemaphore = new SemaphoreSlim(1);
        private SemaphoreSlim whisperSemaphore = new SemaphoreSlim(1);

        public TwitchChatService() { }

        public bool IsUserConnected { get { return this.userClient != null && this.userClient.IsOpen(); } }
        public bool IsBotConnected { get { return this.botClient != null && this.botClient.IsOpen(); } }

        public override string Name { get { return "Twitch Chat"; } }

        public async Task<Result> ConnectUser()
        {
            if (ServiceManager.Get<TwitchSessionService>().UserConnection != null)
            {
                return await this.AttemptConnect((Func<Task<Result>>)(async () =>
                {
                    try
                    {
                        this.cancellationTokenSource = new CancellationTokenSource();

                        this.userClient = new ChatClient(ServiceManager.Get<TwitchSessionService>().UserConnection.Connection);

                        if (ChannelSession.AppSettings.DiagnosticLogging)
                        {
                            this.userClient.OnSentOccurred += Client_OnSentOccurred;
                        }

                        this.initialUserLogins.Clear();

                        this.userClient.OnPacketReceived += Client_OnPacketReceived;
                        this.userClient.OnDisconnectOccurred += UserClient_OnDisconnectOccurred;
                        this.userClient.OnPingReceived += UserClient_OnPingReceived;
                        this.userClient.OnUserJoinReceived += UserClient_OnUserJoinReceived;
                        this.userClient.OnUserLeaveReceived += UserClient_OnUserLeaveReceived;
                        this.userClient.OnUserStateReceived += UserClient_OnUserStateReceived;
                        this.userClient.OnUserNoticeReceived += UserClient_OnUserNoticeReceived;
                        this.userClient.OnChatClearReceived += UserClient_OnChatClearReceived;
                        this.userClient.OnMessageReceived += UserClient_OnMessageReceived;

                        this.userClient.OnUserListReceived += UserClient_OnUserListReceived;
                        await this.userClient.Connect();

                        await Task.Delay(1000);

                        await this.userClient.AddCommandsCapability();
                        await this.userClient.AddTagsCapability();
                        await this.userClient.AddMembershipCapability();

                        await Task.Delay(1000);

                        await this.userClient.Join(ServiceManager.Get<TwitchSessionService>().UserNewAPI);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        AsyncRunner.RunAsyncBackground(this.ChatterJoinLeaveBackground, this.cancellationTokenSource.Token, 2500);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                        await Task.Delay(3000);

                        return new Result();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        return new Result(ex);
                    }
                }));
            }
            return new Result(Resources.TwitchConnectionFailed);
        }

        public async Task DisconnectUser()
        {
            try
            {
                if (this.userClient != null)
                {
                    if (ChannelSession.AppSettings.DiagnosticLogging)
                    {
                        this.userClient.OnSentOccurred -= Client_OnSentOccurred;
                    }
                    this.userClient.OnPacketReceived -= Client_OnPacketReceived;
                    this.userClient.OnDisconnectOccurred -= UserClient_OnDisconnectOccurred;
                    this.userClient.OnPingReceived -= UserClient_OnPingReceived;
                    this.userClient.OnUserJoinReceived -= UserClient_OnUserJoinReceived;
                    this.userClient.OnUserLeaveReceived -= UserClient_OnUserLeaveReceived;
                    this.userClient.OnUserStateReceived -= UserClient_OnUserStateReceived;
                    this.userClient.OnUserNoticeReceived -= UserClient_OnUserNoticeReceived;
                    this.userClient.OnChatClearReceived -= UserClient_OnChatClearReceived;
                    this.userClient.OnMessageReceived -= UserClient_OnMessageReceived;

                    await this.userClient.Disconnect();
                }

                if (this.cancellationTokenSource != null)
                {
                    this.cancellationTokenSource.Cancel();
                    this.cancellationTokenSource = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            this.userClient = null;
        }

        public async Task<Result> ConnectBot()
        {
            if (ServiceManager.Get<TwitchSessionService>().UserConnection != null)
            {
                return await this.AttemptConnect((Func<Task<Result>>)(async () =>
                {
                    try
                    {
                        this.cancellationTokenSource = new CancellationTokenSource();

                        this.botClient = new ChatClient(ServiceManager.Get<TwitchSessionService>().BotConnection.Connection);

                        if (ChannelSession.AppSettings.DiagnosticLogging)
                        {
                            this.botClient.OnSentOccurred += Client_OnSentOccurred;
                        }
                        this.botClient.OnDisconnectOccurred += BotClient_OnDisconnectOccurred;
                        this.botClient.OnPingReceived += BotClient_OnPingReceived;

                        await this.botClient.Connect();

                        await Task.Delay(1000);

                        await this.botClient.AddCommandsCapability();
                        await this.botClient.AddTagsCapability();
                        await this.botClient.AddMembershipCapability();

                        await Task.Delay(1000);

                        await this.botClient.Join(ServiceManager.Get<TwitchSessionService>().UserNewAPI);

                        await Task.Delay(3000);

                        return new Result();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        return new Result(ex);
                    }
                }));
            }
            return new Result(Resources.TwitchConnectionFailed);
        }

        public async Task DisconnectBot()
        {
            try
            {
                if (this.botClient != null)
                {
                    if (ChannelSession.AppSettings.DiagnosticLogging)
                    {
                        this.botClient.OnSentOccurred -= Client_OnSentOccurred;
                    }
                    this.botClient.OnDisconnectOccurred -= BotClient_OnDisconnectOccurred;
                    this.botClient.OnPingReceived -= BotClient_OnPingReceived;

                    await this.botClient.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            this.botClient = null;
        }

        public async Task Initialize()
        {
            List<Task> initializationTasks = new List<Task>();

            initializationTasks.Add(Task.Run(async() =>
            {
                foreach (EmoteModel emote in await ServiceManager.Get<TwitchSessionService>().UserConnection.GetEmotesForUserV5(ServiceManager.Get<TwitchSessionService>().UserV5))
                {
                    this.emotes[emote.code] = emote;
                }
            }));

            Task<IEnumerable<ChatBadgeSetModel>> globalChatBadgesTask = ServiceManager.Get<TwitchSessionService>().UserConnection.GetGlobalChatBadges();
            initializationTasks.Add(globalChatBadgesTask);
            Task<IEnumerable<ChatBadgeSetModel>> channelChatBadgesTask = ServiceManager.Get<TwitchSessionService>().UserConnection.GetChannelChatBadges(ServiceManager.Get<TwitchSessionService>().UserNewAPI);
            initializationTasks.Add(channelChatBadgesTask);

            if (ChannelSession.Settings.ShowBetterTTVEmotes)
            {
                initializationTasks.Add(this.DownloadBetterTTVEmotes());
                initializationTasks.Add(this.DownloadBetterTTVEmotes(ServiceManager.Get<TwitchSessionService>().UserNewAPI.id));
            }

            if (ChannelSession.Settings.ShowFrankerFaceZEmotes)
            {
                initializationTasks.Add(this.DownloadFrankerFaceZEmotes());
                initializationTasks.Add(this.DownloadFrankerFaceZEmotes(ServiceManager.Get<TwitchSessionService>().UserNewAPI.login));
            }

            Task<IEnumerable<BitsCheermoteModel>> cheermotesTask = ServiceManager.Get<TwitchSessionService>().UserConnection.GetBitsCheermotes(ServiceManager.Get<TwitchSessionService>().UserNewAPI);
            initializationTasks.Add(cheermotesTask);

            await Task.WhenAll(initializationTasks);

            foreach (ChatBadgeSetModel badgeSet in globalChatBadgesTask.Result)
            {
                this.chatBadges[badgeSet.id] = badgeSet;
            }

            foreach (ChatBadgeSetModel badgeSet in channelChatBadgesTask.Result)
            {
                this.chatBadges[badgeSet.id] = badgeSet;
            }

            List<TwitchBitsCheermoteViewModel> cheermotes = new List<TwitchBitsCheermoteViewModel>();
            foreach (BitsCheermoteModel bitsCheermote in cheermotesTask.Result)
            {
                if (bitsCheermote.tiers.Any(t => t.can_cheer))
                {
                    this.bitsCheermotes.Add(new TwitchBitsCheermoteViewModel(bitsCheermote));
                }
            }

            await this.userJoinLeaveEventsSemaphore.WaitAndRelease(() =>
            {
                foreach (string user in this.initialUserLogins)
                {
                    this.userJoinEvents.Add(user);
                }
                return Task.FromResult(0);
            });
            this.initialUserLogins.Clear();
        }

        public async Task SendMessage(string message, bool sendAsStreamer = false)
        {
            await this.messageSemaphore.WaitAndRelease(async () =>
            {
                ChatClient client = this.GetChatClient(sendAsStreamer);
                if (client != null)
                {
                    string subMessage = null;
                    do
                    {
                        message = ChatService.SplitLargeMessage(message, MaxMessageLength, out subMessage);
                        await client.SendMessage(ServiceManager.Get<TwitchSessionService>().UserNewAPI, message);
                        message = subMessage;
                        await Task.Delay(500);
                    }
                    while (!string.IsNullOrEmpty(message));
                }
            });
        }

        public async Task SendWhisperMessage(UserViewModel user, string message, bool sendAsStreamer = false)
        {
            await this.messageSemaphore.WaitAndRelease(async () =>
            {
                ChatClient client = this.GetChatClient(sendAsStreamer);
                if (client != null)
                {
                    string subMessage = null;
                    do
                    {
                        message = ChatService.SplitLargeMessage(message, MaxMessageLength, out subMessage);
                        await client.SendWhisperMessage(ServiceManager.Get<TwitchSessionService>().UserNewAPI, user.GetTwitchNewAPIUserModel(), message);
                        message = subMessage;
                        await Task.Delay(500);
                    }
                    while (!string.IsNullOrEmpty(message));
                }
            });
        }

        public async Task DeleteMessage(ChatMessageViewModel message)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.DeleteMessage(ServiceManager.Get<TwitchSessionService>().UserNewAPI, message.ID);
                }
            });
        }

        public async Task ClearMessages()
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.ClearChat(ServiceManager.Get<TwitchSessionService>().UserNewAPI);
                }
            });
        }

        public async Task ModUser(UserViewModel user)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.ModUser(ServiceManager.Get<TwitchSessionService>().UserNewAPI, user.GetTwitchNewAPIUserModel());
                }
            });
        }

        public async Task UnmodUser(UserViewModel user)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.UnmodUser(ServiceManager.Get<TwitchSessionService>().UserNewAPI, user.GetTwitchNewAPIUserModel());
                }
            });
        }

        public async Task TimeoutUser(UserViewModel user, int lengthInSeconds)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.TimeoutUser(ServiceManager.Get<TwitchSessionService>().UserNewAPI, user.GetTwitchNewAPIUserModel(), lengthInSeconds);
                }
            });
        }

        public async Task BanUser(UserViewModel user)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.BanUser(ServiceManager.Get<TwitchSessionService>().UserNewAPI, user.GetTwitchNewAPIUserModel());
                }
            });
        }

        public async Task UnbanUser(UserViewModel user)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.UnbanUser(ServiceManager.Get<TwitchSessionService>().UserNewAPI, user.GetTwitchNewAPIUserModel());
                }
            });
        }

        public async Task RunCommercial(int lengthInSeconds)
        {
            await AsyncRunner.RunAsync(async () =>
            {
                if (this.userClient != null)
                {
                    await this.userClient.RunCommercial(ServiceManager.Get<TwitchSessionService>().UserNewAPI, lengthInSeconds);
                }
            });
        }

        private ChatClient GetChatClient(bool sendAsStreamer = false) { return (this.botClient != null && !sendAsStreamer) ? this.botClient : this.userClient; }

        private async Task ChatterJoinLeaveBackground(CancellationToken cancellationToken)
        {
            List<string> joinsToProcess = new List<string>();
            await this.userJoinLeaveEventsSemaphore.WaitAndRelease(() =>
            {
                for (int i = 0; i < userJoinLeaveEventsTotalToProcess && i < this.userJoinEvents.Count(); i++)
                {
                    string username = this.userJoinEvents.First();
                    joinsToProcess.Add(username);
                    this.userJoinEvents.Remove(username);
                }
                return Task.FromResult(0);
            });

            if (joinsToProcess.Count > 0)
            {
                List<UserViewModel> processedUsers = new List<UserViewModel>();
                foreach (string username in joinsToProcess)
                {
                    UserViewModel user = await ChannelSession.Services.User.GetUserFullSearch(StreamingPlatformTypeEnum.Twitch, userID: null, username);
                    if (user != null)
                    {
                        await ChannelSession.Services.User.AddOrUpdateActiveUser(user);
                        processedUsers.Add(user);
                    }
                }
                this.OnUsersJoinOccurred(this, processedUsers);
            }

            List<string> leavesToProcess = new List<string>();
            await this.userJoinLeaveEventsSemaphore.WaitAndRelease(() =>
            {
                for (int i = 0; i < userJoinLeaveEventsTotalToProcess && i < this.userLeaveEvents.Count(); i++)
                {
                    string username = this.userLeaveEvents.First();
                    leavesToProcess.Add(username);
                    this.userLeaveEvents.Remove(username);
                }
                return Task.FromResult(0);
            });

            if (leavesToProcess.Count > 0)
            {
                List<UserViewModel> processedUsers = new List<UserViewModel>();
                foreach (string username in leavesToProcess)
                {
                    if (!string.IsNullOrEmpty(username))
                    {
                        UserViewModel user = await ChannelSession.Services.User.RemoveActiveUserByUsername(StreamingPlatformTypeEnum.Twitch, username);
                        if (user != null)
                        {
                            processedUsers.Add(user);
                        }
                    }
                }
                await ServiceManager.Get<ChatService>().UsersLeft(processedUsers);
            }
        }

        private async Task DownloadBetterTTVEmotes(string twitchID = null)
        {
            try
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient())
                {
                    List<BetterTTVEmoteModel> emotes = new List<BetterTTVEmoteModel>();

                    HttpResponseMessage response = await client.GetAsync((!string.IsNullOrEmpty(twitchID)) ? "https://api.betterttv.net/3/cached/users/twitch/" + twitchID : "https://api.betterttv.net/3/cached/emotes/global");
                    if (response.IsSuccessStatusCode)
                    {
                        if (!string.IsNullOrEmpty(twitchID))
                        {
                            JObject jobj = await response.ProcessJObjectResponse();
                            if (jobj != null)
                            {
                                JToken channelEmotes = jobj.SelectToken("channelEmotes");
                                if (channelEmotes != null)
                                {
                                    emotes.AddRange(((JArray)channelEmotes).ToTypedArray<BetterTTVEmoteModel>());
                                }

                                JToken sharedEmotes = jobj.SelectToken("sharedEmotes");
                                if (sharedEmotes != null)
                                {
                                    emotes.AddRange(((JArray)sharedEmotes).ToTypedArray<BetterTTVEmoteModel>());
                                }
                            }
                        }
                        else
                        {
                            emotes.AddRange(await response.ProcessResponse<List<BetterTTVEmoteModel>>());
                        }

                        foreach (BetterTTVEmoteModel emote in emotes)
                        {
                            this.betterTTVEmotes[emote.code] = emote;
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        private async Task DownloadFrankerFaceZEmotes(string channelName = null)
        {
            try
            {
                using (AdvancedHttpClient client = new AdvancedHttpClient())
                {
                    JObject jobj = await client.GetJObjectAsync((!string.IsNullOrEmpty(channelName)) ? "https://api.frankerfacez.com/v1/room/" + channelName : "https://api.frankerfacez.com/v1/set/global");
                    if (jobj != null && jobj.ContainsKey("sets"))
                    {
                        JObject setsJObj = (JObject)jobj["sets"];
                        foreach (var kvp in setsJObj)
                        {
                            JObject setJObj = (JObject)kvp.Value;
                            if (setJObj != null && setJObj.ContainsKey("emoticons"))
                            {
                                JArray emoticonsJArray = (JArray)setJObj["emoticons"];
                                foreach (FrankerFaceZEmoteModel emote in emoticonsJArray.ToTypedArray<FrankerFaceZEmoteModel>())
                                {
                                    this.frankerFaceZEmotes[emote.name] = emote;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.Log(ex); }
        }

        private async void UserClient_OnPingReceived(object sender, EventArgs e)
        {
            try
            {
                Logger.Log(LogLevel.Debug, "Twitch User Client - Ping");
                await this.userClient.Pong();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async void BotClient_OnPingReceived(object sender, EventArgs e)
        {
            try
            {
                Logger.Log(LogLevel.Debug, "Twitch Bot Client - Ping");
                await this.botClient.Pong();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private async void UserClient_OnUserJoinReceived(object sender, ChatUserJoinPacketModel userJoin)
        {
            await this.userJoinLeaveEventsSemaphore.WaitAndRelease(() =>
            {
                if (!string.IsNullOrEmpty(userJoin.UserLogin))
                {
                    this.userJoinEvents.Add(userJoin.UserLogin);
                }
                return Task.FromResult(0);
            });
        }

        private async void UserClient_OnUserLeaveReceived(object sender, ChatUserLeavePacketModel userLeave)
        {
            await this.userJoinLeaveEventsSemaphore.WaitAndRelease(() =>
            {
                if (!string.IsNullOrEmpty(userLeave.UserLogin))
                {
                    this.userLeaveEvents.Add(userLeave.UserLogin);
                }
                return Task.FromResult(0);
            });
        }

        private void UserClient_OnUserStateReceived(object sender, ChatUserStatePacketModel userState)
        {
            UserViewModel user = ChannelSession.Services.User.GetActiveUserByUsername(userState.UserDisplayName, StreamingPlatformTypeEnum.Twitch);
            if (user != null)
            {
                user.SetTwitchChatDetails(userState);
            }
        }

        private async void UserClient_OnUserNoticeReceived(object sender, ChatUserNoticePacketModel userNotice)
        {
            try
            {
                if (RaidUserNoticeMessageTypeID.Equals(userNotice.MessageTypeID))
                {
                    UserViewModel user = ChannelSession.Services.User.GetActiveUserByPlatformID(StreamingPlatformTypeEnum.Twitch, userNotice.UserID.ToString());
                    if (user == null)
                    {
                        user = await UserViewModel.Create(userNotice);
                    }
                    user.SetTwitchChatDetails(userNotice);

                    EventTrigger trigger = new EventTrigger(EventTypeEnum.TwitchChannelRaided, user);
                    if (ServiceManager.Get<EventService>().CanPerformEvent(trigger))
                    {
                        ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestRaidUserData] = user.ID;
                        ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestRaidViewerCountData] = userNotice.RaidViewerCount;

                        foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values.ToList())
                        {
                            currency.AddAmount(user.Data, currency.OnHostBonus);
                        }

                        foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                        {
                            if (user.HasPermissionsTo(streamPass.Permission))
                            {
                                streamPass.AddAmount(user.Data, streamPass.HostBonus);
                            }
                        }

                        GlobalEvents.RaidOccurred(user, userNotice.RaidViewerCount);

                        trigger.SpecialIdentifiers["hostviewercount"] = userNotice.RaidViewerCount.ToString();
                        trigger.SpecialIdentifiers["raidviewercount"] = userNotice.RaidViewerCount.ToString();
                        await ServiceManager.Get<EventService>().PerformEvent(trigger);

                        await ChannelSession.Services.Alerts.AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, user, string.Format("{0} raided with {1} viewers", user.FullDisplayName, userNotice.RaidViewerCount), ChannelSession.Settings.AlertRaidColor));
                    }
                }
                else if (SubMysteryGiftUserNoticeMessageTypeID.Equals(userNotice.MessageTypeID) && userNotice.SubTotalGifted > 0)
                {
                    if (ServiceManager.Has<TwitchEventService>())
                    {
                        UserViewModel gifter = UserViewModel.Create("An Anonymous Gifter");
                        if (!TwitchMassGiftedSubEventModel.IsAnonymousGifter(userNotice))
                        {
                            gifter = await ChannelSession.Services.User.GetUserFullSearch(StreamingPlatformTypeEnum.Twitch, userNotice.UserID.ToString(), userNotice.Login);
                            gifter.SetTwitchChatDetails(userNotice);
                        }
                        await ChannelSession.Services.Events.TwitchEventService.AddMassGiftedSub(new TwitchMassGiftedSubEventModel(userNotice, gifter));
                    }
                }
                else if (SubGiftPaidUpgradeUserNoticeMessageTypeID.Equals(userNotice.MessageTypeID))
                {
                    if (ServiceManager.Has<TwitchEventService>())
                    {
                        UserViewModel user = ChannelSession.Services.User.GetActiveUserByPlatformID(StreamingPlatformTypeEnum.Twitch, userNotice.UserID.ToString());
                        if (user == null)
                        {
                            user = await UserViewModel.Create(userNotice);
                        }
                        user.SetTwitchChatDetails(userNotice);

                        await ChannelSession.Services.Events.TwitchEventService.AddSub(new TwitchSubEventModel(user, userNotice));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ForceLog(LogLevel.Debug, JSONSerializerHelper.SerializeToString(userNotice));
                Logger.Log(ex);
                throw ex;
            }
        }

        private async void UserClient_OnChatClearReceived(object sender, ChatClearChatPacketModel chatClear)
        {
            UserViewModel user = ChannelSession.Services.User.GetActiveUserByPlatformID(StreamingPlatformTypeEnum.Twitch, chatClear.UserID);
            if (user == null)
            {
                user = await UserViewModel.Create(chatClear);
            }

            if (chatClear.IsClear)
            {
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, user, "Chat Cleared", ChannelSession.Settings.AlertModerationColor));
            }
            else if (chatClear.IsTimeout)
            {
                EventTrigger trigger = new EventTrigger(EventTypeEnum.ChatUserTimeout);
                trigger.Arguments.Add("@" + user.Username);
                trigger.TargetUser = user;
                trigger.SpecialIdentifiers["timeoutlength"] = chatClear.BanDuration.ToString();
                await ServiceManager.Get<EventService>().PerformEvent(trigger);

                await ChannelSession.Services.Alerts.AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, user, string.Format("{0} Timed Out for {1} seconds", user.FullDisplayName, chatClear.BanDuration), ChannelSession.Settings.AlertModerationColor));
            }
            else if (chatClear.IsBan)
            {
                EventTrigger trigger = new EventTrigger(EventTypeEnum.ChatUserBan);
                trigger.Arguments.Add("@" + user.Username);
                trigger.TargetUser = user;
                await ServiceManager.Get<EventService>().PerformEvent(trigger);

                await ChannelSession.Services.Alerts.AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, user, string.Format("{0} Banned", user.FullDisplayName), ChannelSession.Settings.AlertModerationColor));

                await ChannelSession.Services.User.RemoveActiveUserByID(user.ID);
            }
        }

        private async void UserClient_OnMessageReceived(object sender, ChatMessagePacketModel message)
        {
            if (message != null && !string.IsNullOrEmpty(message.Message))
            {
                if (!string.IsNullOrEmpty(message.UserLogin) && message.UserLogin.Equals("jtv"))
                {
                    if (Regex.IsMatch(message.Message, TwitchChatService.HostChatMessageRegexPattern))
                    {
                        Logger.Log(LogLevel.Debug, JSONSerializerHelper.SerializeToString(message));

                        string hosterUsername = message.Message.Substring(0, message.Message.IndexOf(' '));
                        UserViewModel user = await ChannelSession.Services.User.GetUserFullSearch(StreamingPlatformTypeEnum.Twitch, userID: null, hosterUsername);
                        if (user != null)
                        {
                            await ChannelSession.Services.User.AddOrUpdateActiveUser(user);

                            EventTrigger trigger = new EventTrigger(EventTypeEnum.TwitchChannelHosted, user);
                            if (ServiceManager.Get<EventService>().CanPerformEvent(trigger))
                            {
                                foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values.ToList())
                                {
                                    currency.AddAmount(user.Data, currency.OnHostBonus);
                                }

                                GlobalEvents.HostOccurred(user);

                                await ServiceManager.Get<EventService>().PerformEvent(trigger);

                                await ChannelSession.Services.Alerts.AddAlert(new AlertChatMessageViewModel(StreamingPlatformTypeEnum.Twitch, user, string.Format("{0} hosted the channel", user.FullDisplayName), ChannelSession.Settings.AlertHostColor));
                            }
                        }
                    }
                }
                else
                {
                    UserViewModel user = await ChannelSession.Services.User.GetUserFullSearch(StreamingPlatformTypeEnum.Twitch, message.UserID, message.UserLogin);
                    this.OnMessageOccurred(this, new TwitchChatMessageViewModel(message, user));
                }
            }
        }

        private void UserClient_OnUserListReceived(object sender, ChatUsersListPacketModel userList)
        {
            this.initialUserLogins.AddRange(userList.UserLogins);
            this.userClient.OnUserListReceived -= UserClient_OnUserListReceived;
        }

        private void Client_OnPacketReceived(object sender, ChatRawPacketModel packet)
        {
            if (!TwitchChatService.ExcludedDiagnosticPacketLogging.Contains(packet.Command))
            {
                if (ChannelSession.AppSettings.DiagnosticLogging)
                {
                    Logger.Log(LogLevel.Debug, string.Format("Twitch Client Packet Received: {0}", JSONSerializerHelper.SerializeToString(packet)));
                }
            }
        }

        private void Client_OnSentOccurred(object sender, string packet)
        {
            Logger.Log(LogLevel.Debug, string.Format("Twitch Chat Packet Sent: {0}", packet));
        }

        private async void UserClient_OnDisconnectOccurred(object sender, WebSocketCloseStatus closeStatus)
        {
            ChannelSession.DisconnectionOccurred("Twitch User Chat");

            Result result;
            await this.DisconnectUser();
            do
            {
                await Task.Delay(2500);

                result = await this.ConnectUser();
            }
            while (!result.Success);

            ChannelSession.ReconnectionOccurred("Twitch User Chat");
        }

        private async void BotClient_OnDisconnectOccurred(object sender, WebSocketCloseStatus closeStatus)
        {
            ChannelSession.DisconnectionOccurred("Twitch Bot Chat");

            Result result;
            await this.DisconnectBot();
            do
            {
                await Task.Delay(2500);

                result = await this.ConnectBot();
            }
            while (!result.Success);

            ChannelSession.ReconnectionOccurred("Twitch Bot Chat");
        }
    }
}