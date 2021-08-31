﻿using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Twitch.Base.Models.NewAPI.Ads;
using Twitch.Base.Models.NewAPI.ChannelPoints;
using Twitch.Base.Models.NewAPI.Clips;
using Twitch.Base.Models.NewAPI.Polls;
using Twitch.Base.Models.NewAPI.Predictions;
using Twitch.Base.Models.NewAPI.Streams;

namespace MixItUp.Base.Model.Actions
{
    public enum TwitchActionType
    {
        Host,
        Raid,
        VIPUser,
        UnVIPUser,
        RunAd,
        Clip,
        StreamMarker,
        UpdateChannelPointReward,
        CreatePoll,
        CreatePrediction
    }

    [DataContract]
    public class TwitchActionModel : ActionModelBase
    {
        public const string ClipURLSpecialIdentifier = "clipurl";
        public const string StreamMarkerURLSpecialIdentifier = "streammarkerurl";
        public const string PollChoiceSpecialIdentifier = "pollchoice";
        public const string PredictionOutcomeSpecialIdentifier = "predictionoutcome";

        public const int StreamMarkerMaxDescriptionLength = 140;

        private const string StartinCommercialBreakMessage = "Starting commercial break.";

        public static readonly IEnumerable<int> SupportedAdLengths = new List<int>() { 30, 60, 90, 120, 150, 180 };

        public static TwitchActionModel CreateUserAction(TwitchActionType type, string username)
        {
            TwitchActionModel action = new TwitchActionModel(type);
            action.Username = username;
            return action;
        }

        public static TwitchActionModel CreateAdAction(int adLength)
        {
            TwitchActionModel action = new TwitchActionModel(TwitchActionType.RunAd);
            action.AdLength = adLength;
            return action;
        }

        public static TwitchActionModel CreateClipAction(bool includeDelay, bool showInfoInChat)
        {
            TwitchActionModel action = new TwitchActionModel(TwitchActionType.Clip);
            action.ClipIncludeDelay = includeDelay;
            action.ShowInfoInChat = showInfoInChat;
            return action;
        }

        public static TwitchActionModel CreateStreamMarkerAction(string description, bool showInfoInChat)
        {
            TwitchActionModel actionModel = new TwitchActionModel(TwitchActionType.StreamMarker);
            actionModel.StreamMarkerDescription = description;
            actionModel.ShowInfoInChat = showInfoInChat;
            return actionModel;
        }

        public static TwitchActionModel CreateUpdateChannelPointReward(Guid id, bool state, string cost, bool updateCooldownsAndLimits, string maxPerStream, string maxPerUser, string globalCooldown)
        {
            TwitchActionModel action = new TwitchActionModel(TwitchActionType.UpdateChannelPointReward);
            action.ChannelPointRewardID = id;
            action.ChannelPointRewardState = state;
            action.ChannelPointRewardCostString = cost;
            action.ChannelPointRewardUpdateCooldownsAndLimits = updateCooldownsAndLimits;
            action.ChannelPointRewardMaxPerStreamString = maxPerStream;
            action.ChannelPointRewardMaxPerUserString = maxPerUser;
            action.ChannelPointRewardGlobalCooldownString = globalCooldown;
            return action;
        }

        public static TwitchActionModel CreatePollAction(string title, int duration, int channelPointCost, int bitCost, IEnumerable<string> choices, IEnumerable<ActionModelBase> actions)
        {
            TwitchActionModel action = new TwitchActionModel(TwitchActionType.CreatePoll);
            action.PollTitle = title;
            action.PollDurationSeconds = duration;
            action.PollChannelPointsCost = channelPointCost;
            action.PollBitsCost = bitCost;
            action.PollChoices = new List<string>(choices);
            action.Actions = new List<ActionModelBase>(actions);
            return action;
        }

        public static TwitchActionModel CreatePredictionAction(string title, int duration, IEnumerable<string> outcomes, IEnumerable<ActionModelBase> actions)
        {
            TwitchActionModel action = new TwitchActionModel(TwitchActionType.CreatePrediction);
            action.PredictionTitle = title;
            action.PredictionDurationSeconds = duration;
            action.PredictionOutcomes = new List<string>(outcomes);
            action.Actions = new List<ActionModelBase>(actions);
            return action;
        }

        [DataMember]
        public TwitchActionType ActionType { get; set; }
        [DataMember]
        public bool ShowInfoInChat { get; set; }

        [DataMember]
        public string Username { get; set; }

        [DataMember]
        public int AdLength { get; set; } = 60;

        [DataMember]
        public bool ClipIncludeDelay { get; set; }

        [DataMember]
        public string StreamMarkerDescription { get; set; }

        [DataMember]
        public Guid ChannelPointRewardID { get; set; }
        [DataMember]
        public bool ChannelPointRewardState { get; set; }
        [DataMember]
        public string ChannelPointRewardCostString { get; set; }
        [DataMember]
        public bool ChannelPointRewardUpdateCooldownsAndLimits { get; set; }
        [DataMember]
        public string ChannelPointRewardMaxPerStreamString { get; set; }
        [DataMember]
        public string ChannelPointRewardMaxPerUserString { get; set; }
        [DataMember]
        public string ChannelPointRewardGlobalCooldownString { get; set; }
        [Obsolete]
        [DataMember]
        public int ChannelPointRewardCost { get; set; } = -1;
        [Obsolete]
        [DataMember]
        public int ChannelPointRewardMaxPerStream { get; set; } = -1;
        [Obsolete]
        [DataMember]
        public int ChannelPointRewardMaxPerUser { get; set; } = -1;
        [Obsolete]
        [DataMember]
        public int ChannelPointRewardGlobalCooldown { get; set; } = -1;

        [DataMember]
        public string PollTitle { get; set; }
        [DataMember]
        public int PollDurationSeconds { get; set; }
        [DataMember]
        public int PollChannelPointsCost { get; set; }
        [DataMember]
        public int PollBitsCost { get; set; }
        [DataMember]
        public List<string> PollChoices { get; set; } = new List<string>();

        [DataMember]
        public string PredictionTitle { get; set; }
        [DataMember]
        public int PredictionDurationSeconds { get; set; }
        [DataMember]
        public List<string> PredictionOutcomes { get; set; } = new List<string>();

        [DataMember]
        public List<ActionModelBase> Actions { get; set; } = new List<ActionModelBase>();

        private TwitchActionModel(TwitchActionType type)
            : base(ActionTypeEnum.Twitch)
        {
            this.ActionType = type;
        }

#pragma warning disable CS0612 // Type or member is obsolete
        internal TwitchActionModel(MixItUp.Base.Actions.StreamingPlatformAction action)
            : base(ActionTypeEnum.Twitch)
        {
            if (action.ActionType == Base.Actions.StreamingPlatformActionType.Host)
            {
                this.ActionType = TwitchActionType.Host;
                this.Username = action.HostChannelName;
            }
            else if (action.ActionType == Base.Actions.StreamingPlatformActionType.Raid)
            {
                this.ActionType = TwitchActionType.Raid;
                this.Username = action.HostChannelName;
            }
            else if (action.ActionType == Base.Actions.StreamingPlatformActionType.RunAd)
            {
                this.ActionType = TwitchActionType.RunAd;
                this.AdLength = action.AdLength;
            }
        }

        internal TwitchActionModel(MixItUp.Base.Actions.ClipsAction action)
            : base(ActionTypeEnum.Twitch)
        {
            this.ActionType = TwitchActionType.Clip;
            this.ClipIncludeDelay = action.IncludeDelay;
            this.ShowInfoInChat = action.ShowClipInfoInChat;
        }

        internal TwitchActionModel(MixItUp.Base.Actions.ModerationAction action)
            : base(ActionTypeEnum.Twitch)
        {
            if (action.ModerationType == Base.Actions.ModerationActionTypeEnum.VIPUser)
            {
                this.ActionType = TwitchActionType.VIPUser;
                this.Username = action.UserName;
            }
            else if (action.ModerationType == Base.Actions.ModerationActionTypeEnum.UnVIPUser)
            {
                this.ActionType = TwitchActionType.UnVIPUser;
                this.Username = action.UserName;
            }
        }
#pragma warning restore CS0612 // Type or member is obsolete

        private TwitchActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            if (ServiceManager.Get<TwitchSessionService>().IsConnected)
            {
                if (this.ActionType == TwitchActionType.Host)
                {
                    string channelName = await this.ReplaceStringWithSpecialModifiers(this.Username, parameters);
                    await ServiceManager.Get<ChatService>().SendMessage("/host @" + channelName, sendAsStreamer: true, platform: StreamingPlatformTypeEnum.Twitch);
                }
                else if (this.ActionType == TwitchActionType.Raid)
                {
                    string channelName = await this.ReplaceStringWithSpecialModifiers(this.Username, parameters);
                    await ServiceManager.Get<ChatService>().SendMessage("/raid @" + channelName, sendAsStreamer: true, platform: StreamingPlatformTypeEnum.Twitch);
                }
                else if (this.ActionType == TwitchActionType.RunAd)
                {
                    AdResponseModel response = await ServiceManager.Get<TwitchSessionService>().UserConnection.RunAd(ServiceManager.Get<TwitchSessionService>().UserNewAPI, this.AdLength);
                    if (response == null)
                    {
                        await ServiceManager.Get<ChatService>().SendMessage("ERROR: We were unable to run an ad, please try again later");
                    }
                    else if (!string.IsNullOrEmpty(response.message) && !response.message.Contains(StartinCommercialBreakMessage, System.StringComparison.OrdinalIgnoreCase))
                    {
                        await ServiceManager.Get<ChatService>().SendMessage("ERROR: " + response.message);
                    }
                }
                else if (this.ActionType == TwitchActionType.VIPUser || this.ActionType == TwitchActionType.UnVIPUser)
                {
                    string targetUsername = null;
                    if (!string.IsNullOrEmpty(this.Username))
                    {
                        targetUsername = await this.ReplaceStringWithSpecialModifiers(this.Username, parameters);
                    }
                    else
                    {
                        targetUsername = parameters.User.Username;
                    }

                    if (!string.IsNullOrEmpty(targetUsername))
                    {
                        if (this.ActionType == TwitchActionType.VIPUser)
                        {
                            await ServiceManager.Get<ChatService>().SendMessage("/vip @" + targetUsername, sendAsStreamer: true, platform: StreamingPlatformTypeEnum.Twitch);
                        }
                        else if (this.ActionType == TwitchActionType.UnVIPUser)
                        {
                            await ServiceManager.Get<ChatService>().SendMessage("/unvip @" + targetUsername, sendAsStreamer: true, platform: StreamingPlatformTypeEnum.Twitch);
                        }
                    }
                }
                else if (this.ActionType == TwitchActionType.Clip)
                {
                    ClipCreationModel clipCreation = await ServiceManager.Get<TwitchSessionService>().UserConnection.CreateClip(ServiceManager.Get<TwitchSessionService>().UserNewAPI, this.ClipIncludeDelay);
                    if (clipCreation != null)
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            await Task.Delay(5000);

                            ClipModel clip = await ServiceManager.Get<TwitchSessionService>().UserConnection.GetClip(clipCreation);
                            if (clip != null && !string.IsNullOrEmpty(clip.url))
                            {
                                if (this.ShowInfoInChat)
                                {
                                    await ServiceManager.Get<ChatService>().SendMessage(string.Format(MixItUp.Base.Resources.ClipCreatedMessage, clip.url), parameters.Platform);
                                }
                                parameters.SpecialIdentifiers[ClipURLSpecialIdentifier] = clip.url;

                                GlobalEvents.TwitchClipCreated(clip);
                                return;
                            }
                        }
                    }
                    await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.ClipCreationFailed);
                }
                else if (this.ActionType == TwitchActionType.StreamMarker)
                {
                    string description = await this.ReplaceStringWithSpecialModifiers(this.StreamMarkerDescription, parameters);
                    if (!string.IsNullOrEmpty(description) && description.Length > TwitchActionModel.StreamMarkerMaxDescriptionLength)
                    {
                        await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.StreamMarkerDescriptionMustBe140CharactersOrLess);
                        return;
                    }

                    CreatedStreamMarkerModel streamMarker = await ServiceManager.Get<TwitchSessionService>().UserConnection.CreateStreamMarker(ServiceManager.Get<TwitchSessionService>().UserNewAPI, description);
                    if (streamMarker != null)
                    {
                        if (this.ShowInfoInChat)
                        {
                            await ServiceManager.Get<ChatService>().SendMessage(string.Format(MixItUp.Base.Resources.StreamMarkerCreatedMessage, streamMarker.URL), parameters.Platform);
                        }
                        parameters.SpecialIdentifiers[StreamMarkerURLSpecialIdentifier] = streamMarker.URL;
                        return;
                    }
                    await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.StreamMarkerCreationFailed, parameters.Platform);
                }
            }
            else if (this.ActionType == TwitchActionType.UpdateChannelPointReward)
            {
                JObject jobj = new JObject()
                {
                    { "is_enabled", this.ChannelPointRewardState },
                };

#pragma warning disable CS0612 // Type or member is obsolete
                if (this.ChannelPointRewardCost >= 0)
                {
                    this.ChannelPointRewardCostString = this.ChannelPointRewardCost.ToString();
                    this.ChannelPointRewardMaxPerStreamString = this.ChannelPointRewardMaxPerStream.ToString();
                    this.ChannelPointRewardMaxPerUserString = this.ChannelPointRewardMaxPerUser.ToString();
                    this.ChannelPointRewardGlobalCooldownString = this.ChannelPointRewardGlobalCooldown.ToString();

                    this.ChannelPointRewardCost = -1;
                    this.ChannelPointRewardMaxPerStream = -1;
                    this.ChannelPointRewardMaxPerUser = -1;
                    this.ChannelPointRewardGlobalCooldown = -1;
                }
#pragma warning restore CS0612 // Type or member is obsolete

                int.TryParse(await this.ReplaceStringWithSpecialModifiers(this.ChannelPointRewardCostString, parameters), out int cost);
                if (cost > 0) { jobj["cost"] = cost; }

                if (this.ChannelPointRewardUpdateCooldownsAndLimits)
                {
                    int.TryParse(await this.ReplaceStringWithSpecialModifiers(this.ChannelPointRewardMaxPerStreamString, parameters), out int maxPerStream);
                    if (maxPerStream > 0)
                    {
                        jobj["max_per_stream_setting"] = new JObject()
                        {
                            { "is_enabled", true },
                            { "max_per_stream", maxPerStream}
                        };
                    }
                    else
                    {
                        jobj["max_per_stream_setting"] = new JObject()
                        {
                            { "is_enabled", false },
                        };
                    }

                    int.TryParse(await this.ReplaceStringWithSpecialModifiers(this.ChannelPointRewardMaxPerUserString, parameters), out int maxPerUser);
                    if (maxPerUser > 0)
                    {
                        jobj["max_per_user_per_stream_setting"] = new JObject()
                        {
                            { "is_enabled", true },
                            { "max_per_user_per_stream", maxPerUser }
                        };
                    }
                    else
                    {
                        jobj["max_per_user_per_stream_setting"] = new JObject()
                        {
                            { "is_enabled", false },
                        };
                    }

                    int.TryParse(await this.ReplaceStringWithSpecialModifiers(this.ChannelPointRewardGlobalCooldownString, parameters), out int globalCooldown);
                    if (globalCooldown > 0)
                    {
                        jobj["global_cooldown_setting"] = new JObject()
                        {
                            { "is_enabled", true },
                            { "global_cooldown_seconds", globalCooldown * 60 }
                        };
                    }
                    else
                    {
                        jobj["global_cooldown_setting"] = new JObject()
                        {
                            { "is_enabled", false },
                        };
                    }
                }

                CustomChannelPointRewardModel reward = await ServiceManager.Get<TwitchSessionService>().UserConnection.UpdateCustomChannelPointReward(ServiceManager.Get<TwitchSessionService>().UserNewAPI, this.ChannelPointRewardID, jobj);
                if (reward == null)
                {
                    await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.TwitchActionChannelPointRewardCouldNotBeUpdated, StreamingPlatformTypeEnum.Twitch);
                }
            }
            else if (this.ActionType == TwitchActionType.CreatePoll)
            {
                List<CreatePollChoiceModel> choices = new List<CreatePollChoiceModel>();
                foreach (string choice in this.PollChoices)
                {
                    choices.Add(new CreatePollChoiceModel()
                    {
                        title = await this.ReplaceStringWithSpecialModifiers(choice, parameters)
                    });
                }

                PollModel poll = await ServiceManager.Get<TwitchSessionService>().UserConnection.CreatePoll(new CreatePollModel()
                {
                    broadcaster_id = ServiceManager.Get<TwitchSessionService>().UserNewAPI.id,
                    title = await this.ReplaceStringWithSpecialModifiers(this.PollTitle, parameters),
                    duration = this.PollDurationSeconds,
                    channel_points_voting_enabled = this.PollChannelPointsCost > 0,
                    channel_points_per_vote = this.PollChannelPointsCost,
                    bits_voting_enabled = this.PollBitsCost > 0,
                    bits_per_vote = this.PollBitsCost,
                    choices = choices
                });

                if (poll == null)
                {
                    await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.TwitchPollFailedToCreate);
                    return;
                }

                if (this.Actions.Count > 0)
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
                    {
                        await Task.Delay(1000 * (this.PollDurationSeconds + 2));

                        for (int i = 0; i < 5; i++)
                        {
                            PollModel results = await ServiceManager.Get<TwitchSessionService>().UserConnection.GetPoll(ServiceManager.Get<TwitchSessionService>().UserNewAPI, poll.id);
                            if (results != null)
                            {
                                if (string.Equals(results.status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                                {
                                    int maxVotes = results.choices.Max(c => c.votes);
                                    IEnumerable<PollChoiceModel> winningChoices = results.choices.Where(c => c.votes == maxVotes);
                                    parameters.SpecialIdentifiers[PollChoiceSpecialIdentifier] = string.Join(" & ", winningChoices.Select(c => c.title));

                                    await ServiceManager.Get<CommandService>().RunDirectly(new CommandInstanceModel(this.Actions, parameters));
                                    return;
                                }
                                else if (!string.Equals(results.status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                                {
                                    return;
                                }
                            }

                            await Task.Delay(2000);
                        }

                        await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.TwitchPollFailedToGetResults);
                    }, new CancellationToken());
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
            else if (this.ActionType == TwitchActionType.CreatePrediction)
            {
                List<CreatePredictionOutcomeModel> outcomes = new List<CreatePredictionOutcomeModel>();
                foreach (string outcome in this.PredictionOutcomes)
                {
                    outcomes.Add(new CreatePredictionOutcomeModel()
                    {
                        title = await this.ReplaceStringWithSpecialModifiers(outcome, parameters)
                    });
                }

                PredictionModel prediction = await ServiceManager.Get<TwitchSessionService>().UserConnection.CreatePrediction(new CreatePredictionModel()
                {
                    broadcaster_id = ServiceManager.Get<TwitchSessionService>().UserNewAPI.id,
                    title = await this.ReplaceStringWithSpecialModifiers(this.PredictionTitle, parameters),
                    prediction_window = this.PredictionDurationSeconds,
                    outcomes = outcomes
                });

                if (prediction == null)
                {
                    await ServiceManager.Get<ChatService>().SendMessage(MixItUp.Base.Resources.TwitchPredictionFailedToCreate);
                    return;
                }

                if (this.Actions.Count > 0)
                {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
                    {
                        await Task.Delay(1000 * this.PredictionDurationSeconds);

                        while (true)
                        {
                            await Task.Delay(10000);

                            PredictionModel results = await ServiceManager.Get<TwitchSessionService>().UserConnection.GetPrediction(ServiceManager.Get<TwitchSessionService>().UserNewAPI, prediction.id);
                            if (results != null)
                            {
                                if (string.Equals(results.status, "RESOLVED", StringComparison.OrdinalIgnoreCase))
                                {
                                    PredictionOutcomeModel outcome = results.outcomes.FirstOrDefault(o => string.Equals(o.id, results.winning_outcome_id));

                                    parameters.SpecialIdentifiers[PredictionOutcomeSpecialIdentifier] = outcome?.title;

                                    await ServiceManager.Get<CommandService>().RunDirectly(new CommandInstanceModel(this.Actions, parameters));
                                    return;
                                }
                                else if (string.Equals(results.status, "CANCELED", StringComparison.OrdinalIgnoreCase))
                                {
                                    return;
                                }
                            }
                        }
                    }, new CancellationToken());
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
        }
    }
}
