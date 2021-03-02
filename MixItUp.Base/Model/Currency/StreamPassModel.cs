﻿using MixItUp.Base.Commands;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Currency
{
    public class StreamPassModel : IEquatable<StreamPassModel>
    {
        [DataMember]
        public Guid ID { get; set; }
        [DataMember]
        public string Name { get; set; }        
        [DataMember]
        public string SpecialIdentifier { get; set; }
        [DataMember]
        public UserRoleEnum Permission { get; set; }
        [DataMember]
        public int MaxLevel { get; set; }
        [DataMember]
        public int PointsForLevelUp { get; set; }
        [DataMember]
        public double SubMultiplier { get; set; }

        [DataMember]
        public DateTimeOffset StartDate { get; set; }
        [DataMember]
        public DateTimeOffset EndDate { get; set; }

        [DataMember]
        public int ViewingRateAmount { get; set; }
        [DataMember]
        public int ViewingRateMinutes { get; set; }
        [DataMember]
        public int MinimumActiveRate { get; set; }
        [DataMember]
        public int FollowBonus { get; set; }
        [DataMember]
        public int HostBonus { get; set; }
        [DataMember]
        public int SubscribeBonus { get; set; }
        [DataMember]
        public double DonationBonus { get; set; }
        [DataMember]
        public double BitsBonus { get; set; }

        [DataMember]
        public Guid DefaultLevelUpCommandID { get; set; }
        [DataMember]
        public Dictionary<int, Guid> CustomLevelUpCommands { get; set; } = new Dictionary<int, Guid>();

        public StreamPassModel()
        {
            this.ID = Guid.NewGuid();
        }

        public StreamPassModel(StreamPassModel copy)
            : this()
        {
            this.Name = copy.Name + " COPY";
            this.SpecialIdentifier = SpecialIdentifierStringBuilder.ConvertToSpecialIdentifier(this.Name, maxLength: 15);
            this.Permission = copy.Permission;
            this.MaxLevel = copy.MaxLevel;
            this.PointsForLevelUp = copy.PointsForLevelUp;
            this.SubMultiplier = copy.SubMultiplier;

            this.StartDate = copy.StartDate;
            this.EndDate = copy.EndDate;

            this.ViewingRateAmount = copy.ViewingRateAmount;
            this.ViewingRateMinutes = copy.ViewingRateMinutes;
            this.MinimumActiveRate = copy.MinimumActiveRate;
            this.FollowBonus = copy.FollowBonus;
            this.HostBonus = copy.HostBonus;
            this.SubscribeBonus = copy.SubscribeBonus;
            this.DonationBonus = copy.DonationBonus;
            this.BitsBonus = copy.BitsBonus;

            this.DefaultLevelUpCommandID = this.DuplicateCommand(copy.DefaultLevelUpCommandID);
            this.CustomLevelUpCommands.Clear();
            foreach (var kvp in copy.CustomLevelUpCommands)
            {
                this.CustomLevelUpCommands[kvp.Key] = this.DuplicateCommand(kvp.Value);
            }
        }

        [JsonIgnore]
        public int MaxPoints { get { return this.MaxLevel * this.PointsForLevelUp; } }

        [JsonIgnore]
        public string DateRangeString { get { return string.Format("{0} - {1}", this.StartDate.ToFriendlyDateString(), this.EndDate.ToFriendlyDateString()); } }

        [JsonIgnore]
        public string UserAmountSpecialIdentifier { get { return string.Format("{0}{1}", SpecialIdentifierStringBuilder.UserSpecialIdentifierHeader, this.SpecialIdentifier); } }

        [JsonIgnore]
        public string UserLevelSpecialIdentifier { get { return string.Format("{0}level", this.UserAmountSpecialIdentifier); } }

        [JsonIgnore]
        public string UserPointsDisplaySpecialIdentifier { get { return string.Format("{0}display", this.UserAmountSpecialIdentifier); } }

        [JsonIgnore]
        public string SpecialIdentifiersReferenceDisplay
        {
            get
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(SpecialIdentifierStringBuilder.SpecialIdentifierHeader + this.UserAmountSpecialIdentifier);
                stringBuilder.AppendLine(SpecialIdentifierStringBuilder.SpecialIdentifierHeader + this.UserLevelSpecialIdentifier);
                return stringBuilder.ToString().Trim(new char[] { '\r', '\n' });
            }
            set { }
        }

        [JsonIgnore]
        public CommandModelBase DefaultLevelUpCommand
        {
            get { return ChannelSession.Settings.GetCommand(this.DefaultLevelUpCommandID); }
            set
            {
                if (value != null)
                {
                    this.DefaultLevelUpCommandID = value.ID;
                    ChannelSession.Settings.SetCommand(value);
                }
                else
                {
                    ChannelSession.Settings.RemoveCommand(this.DefaultLevelUpCommandID);
                    this.DefaultLevelUpCommandID = Guid.Empty;
                }
            }
        }

        public int GetAmount(UserDataModel user)
        {
            if (user.StreamPassAmounts.ContainsKey(this.ID))
            {
                return user.StreamPassAmounts[this.ID];
            }
            return 0;
        }

        public int GetLevel(UserDataModel user) { return (this.GetAmount(user) / this.PointsForLevelUp); }

        public bool HasAmount(UserDataModel user, int amount)
        {
            return (user.IsCurrencyRankExempt || this.GetAmount(user) >= amount);
        }

        public void SetAmount(UserDataModel user, int amount)
        {
            user.StreamPassAmounts[this.ID] = Math.Min(Math.Max(amount, 0), this.MaxPoints);
            if (ChannelSession.Settings != null)
            {
                ChannelSession.Settings.UserData.ManualValueChanged(user.ID);
            }
        }

        public void AddAmount(UserDataModel user, int amount)
        {
            if (!user.IsCurrencyRankExempt && amount > 0)
            {
                int currentLevel = this.GetLevel(user);

                this.SetAmount(user, this.GetAmount(user) + amount);

                int newLevel = this.GetLevel(user);

                if (newLevel > currentLevel)
                {
                    for (int level = (currentLevel + 1); level <= newLevel; level++)
                    {
                        Dictionary<string, string> specialIdentifiers = new Dictionary<string, string>()
                        {
                            { this.UserLevelSpecialIdentifier, level.ToString() }
                        };

                        CommandModelBase command = null;
                        if (this.CustomLevelUpCommands.ContainsKey(level))
                        {
                            command = ChannelSession.Settings.GetCommand(this.CustomLevelUpCommands[level]);
                        }

                        if (command == null && this.DefaultLevelUpCommand != null && this.DefaultLevelUpCommand.IsEnabled)
                        {
                            command = this.DefaultLevelUpCommand;
                        }

                        if (command != null)
                        {
                            UserViewModel userViewModel = ServiceManager.Get<UserService>().GetUserByID(user.ID);
                            if (userViewModel == null)
                            {
                                userViewModel = new UserViewModel(user);
                            }
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                            command.Perform(new CommandParametersModel(userViewModel, specialIdentifiers: specialIdentifiers));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        }
                    }
                }
            }
        }

        public void SubtractAmount(UserDataModel user, int amount)
        {
            if (!user.IsCurrencyRankExempt)
            {
                this.SetAmount(user, this.GetAmount(user) - amount);
            }
        }

        public void ResetAmount(UserDataModel user) { this.SetAmount(user, 0); }

        public void UpdateUserData()
        {
            DateTime date = DateTimeOffset.Now.Date;
            if (ServiceManager.Get<TwitchSessionService>().StreamIsLive && this.StartDate.Date <= date && date <= this.EndDate && this.ViewingRateMinutes > 0)
            {
                DateTimeOffset minActiveTime = DateTimeOffset.Now.Subtract(TimeSpan.FromMinutes(this.MinimumActiveRate));
                foreach (UserViewModel user in ServiceManager.Get<UserService>().GetAllWorkableUsers())
                {
                    if (!user.Data.IsCurrencyRankExempt && user.HasPermissionsTo(this.Permission) && (this.MinimumActiveRate == 0 || user.LastActivity > minActiveTime))
                    {
                        if (user.Data.ViewingMinutes % this.ViewingRateMinutes == 0)
                        {
                            int amount = this.ViewingRateAmount;
                            if (this.SubMultiplier > 1.0 && user.HasPermissionsTo(UserRoleEnum.Subscriber))
                            {
                                amount = (int)Math.Ceiling(((double)amount) * this.SubMultiplier);
                            }
                            this.AddAmount(user.Data, amount);
                            ChannelSession.Settings.UserData.ManualValueChanged(user.ID);
                        }
                    }
                }
            }
        }

        public async Task Reset()
        {
            foreach (UserDataModel user in ChannelSession.Settings.UserData.Values.ToList())
            {
                if (this.GetAmount(user) > 0)
                {
                    this.SetAmount(user, 0);
                    ChannelSession.Settings.UserData.ManualValueChanged(user.ID);
                }
            }
            await ChannelSession.SaveSettings();
        }

        public override bool Equals(object obj)
        {
            if (obj is StreamPassModel)
            {
                return this.Equals((StreamPassModel)obj);
            }
            return false;
        }

        public bool Equals(StreamPassModel other)
        {
            return this.ID.Equals(other.ID);
        }

        public override int GetHashCode()
        {
            return this.ID.GetHashCode();
        }

        private Guid DuplicateCommand(Guid id)
        {
            if (id != Guid.Empty)
            {
                CustomCommandModel command = (CustomCommandModel)ChannelSession.Settings.GetCommand(id);
                if (command != null)
                {
                    command = JSONSerializerHelper.DeserializeFromString<CustomCommandModel>(JSONSerializerHelper.SerializeToString(command));
                    command.ID = Guid.NewGuid();
                    ChannelSession.Settings.SetCommand(command);
                    return command.ID;
                }
            }
            return Guid.Empty;
        }
    }
}