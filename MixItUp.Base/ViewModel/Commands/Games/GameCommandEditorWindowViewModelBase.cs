﻿using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Commands.Games;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Commands;
using MixItUp.Base.ViewModel.Requirements;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Games
{
    public class RoleProbabilityPayoutViewModel : NotifyPropertyChangedBase
    {
        public UserRoleEnum Role
        {
            get { return this.role; }
            set
            {
                this.role = value;
                this.NotifyPropertyChanged();
            }
        }
        private UserRoleEnum role;

        public int Probability
        {
            get { return this.probability; }
            set
            {
                this.probability = value;
                this.NotifyPropertyChanged();
            }
        }
        private int probability;

        public double Payout
        {
            get { return this.payout; }
            set
            {
                this.payout = value;
                this.NotifyPropertyChanged();
            }
        }
        private double payout;

        public RoleProbabilityPayoutViewModel(UserRoleEnum role, int probability, double payout)
        {
            this.Role = role;
            this.Probability = probability;
            this.Payout = payout;
        }

        public RoleProbabilityPayoutViewModel(RoleProbabilityPayoutModel model) : this(model.Role, model.Probability, model.Payout) { }

        public RoleProbabilityPayoutModel GetModel() { return new RoleProbabilityPayoutModel(this.Role, this.Probability, this.Payout); }
    }

    public class GameOutcomeViewModel : NotifyPropertyChangedBase
    {
        public string Name
        {
            get { return this.name; }
            set
            {
                this.name = value;
                this.NotifyPropertyChanged();
            }
        }
        private string name;

        public ObservableCollection<RoleProbabilityPayoutViewModel> RoleProbabilityPayouts { get; set; } = new ObservableCollection<RoleProbabilityPayoutViewModel>();

        public int UserChance
        {
            get { return this.userRoleProbabilityPayout.Probability; }
            set
            {
                this.userRoleProbabilityPayout.Probability = value;
                this.NotifyPropertyChanged();
            }
        }
        public double UserPayout
        {
            get { return this.userRoleProbabilityPayout.Payout; }
            set
            {
                this.userRoleProbabilityPayout.Payout = value;
                this.NotifyPropertyChanged();
            }
        }
        private RoleProbabilityPayoutViewModel userRoleProbabilityPayout;

        public int SubChance
        {
            get { return this.subRoleProbabilityPayout.Probability; }
            set
            {
                this.subRoleProbabilityPayout.Probability = value;
                this.NotifyPropertyChanged();
            }
        }
        public double SubPayout
        {
            get { return this.subRoleProbabilityPayout.Payout; }
            set
            {
                this.subRoleProbabilityPayout.Payout = value;
                this.NotifyPropertyChanged();
            }
        }
        private RoleProbabilityPayoutViewModel subRoleProbabilityPayout;

        public int ModChance
        {
            get { return this.modRoleProbabilityPayout.Probability; }
            set
            {
                this.modRoleProbabilityPayout.Probability = value;
                this.NotifyPropertyChanged();
            }
        }
        public double ModPayout
        {
            get { return this.modRoleProbabilityPayout.Payout; }
            set
            {
                this.modRoleProbabilityPayout.Payout = value;
                this.NotifyPropertyChanged();
            }
        }
        private RoleProbabilityPayoutViewModel modRoleProbabilityPayout;

        public CustomCommandModel Command
        {
            get { return this.command; }
            set
            {
                this.command = value;
                this.NotifyPropertyChanged();
            }
        }
        private CustomCommandModel command;

        public GameOutcomeViewModel() : this(string.Empty) { }

        public GameOutcomeViewModel(string name) : this(name, 0, 0) { }

        public GameOutcomeViewModel(string name, int probability, double payout) : this(name, probability, payout, new CustomCommandModel(MixItUp.Base.Resources.GameSubCommand)) { }

        public GameOutcomeViewModel(string name, int probability, double payout, CustomCommandModel command)
        {
            this.Name = name;
            this.RoleProbabilityPayouts.Add(new RoleProbabilityPayoutViewModel(UserRoleEnum.User, probability, payout));
            this.RoleProbabilityPayouts.Add(new RoleProbabilityPayoutViewModel(UserRoleEnum.Subscriber, probability, payout));
            this.RoleProbabilityPayouts.Add(new RoleProbabilityPayoutViewModel(UserRoleEnum.Mod, probability, payout));
            this.Command = command;
            this.SetRoleProbabilityPayoutProperties();
        }

        public GameOutcomeViewModel(GameOutcomeModel model)
        {
            this.Name = model.Name;
            foreach (var kvp in model.RoleProbabilityPayouts)
            {
                this.RoleProbabilityPayouts.Add(new RoleProbabilityPayoutViewModel(kvp.Value));
            }
            this.Command = model.Command;
            this.SetRoleProbabilityPayoutProperties();
        }
        
        public virtual Result Validate()
        {
            if (string.IsNullOrEmpty(this.Name))
            {
                return new Result(MixItUp.Base.Resources.GameCommandOutcomeMissingName);
            }

            if (this.Command == null)
            {
                return new Result(MixItUp.Base.Resources.GameCommandOutcomeMissingCommand);
            }

            return new Result();
        }

        public virtual GameOutcomeModel GetModel() { return new GameOutcomeModel(this.Name, this.RoleProbabilityPayouts.ToDictionary(rpp => rpp.Role, rpp => rpp.GetModel()), this.Command); }

        private void SetRoleProbabilityPayoutProperties()
        {
            this.userRoleProbabilityPayout = this.RoleProbabilityPayouts.FirstOrDefault(rpp => rpp.Role == UserRoleEnum.User);
            this.subRoleProbabilityPayout = this.RoleProbabilityPayouts.FirstOrDefault(rpp => rpp.Role == UserRoleEnum.Subscriber);
            this.modRoleProbabilityPayout = this.RoleProbabilityPayouts.FirstOrDefault(rpp => rpp.Role == UserRoleEnum.Mod);
        }
    }

    public abstract class GameCommandEditorWindowViewModelBase : ChatCommandEditorWindowViewModel
    {
        public GameCurrencyRequirementViewModel GameCurrencyRequirement { get { return this.Requirements.GameCurrency; } }

        public ObservableCollection<GameOutcomeViewModel> Outcomes { get; set; } = new ObservableCollection<GameOutcomeViewModel>();

        public ICommand AddOutcomeCommand { get; set; }
        public ICommand DeleteOutcomeCommand { get; set; }

        public GameCommandEditorWindowViewModelBase(GameCommandModelBase existingCommand) : base(existingCommand) { this.SetUICommands(); }

        public GameCommandEditorWindowViewModelBase(CurrencyModel currency)
            : base()
        {
            if (this.GameCurrencyRequirement == null && currency != null)
            {
                this.Requirements.GameCurrency = new GameCurrencyRequirementViewModel(currency);
            }
            this.SetUICommands();
        }

        public override async Task<Result> Validate()
        {
            Result result = await base.Validate();
            if (!result.Success)
            {
                return result;
            }

            return new Result();
        }

        public override Task<CommandModelBase> GetCommand() { throw new NotImplementedException(); }

        public override Task SaveCommandToSettings(CommandModelBase command)
        {
            GameCommandModelBase c = (GameCommandModelBase)command;
            ChannelSession.GameCommands.Remove(c);
            ChannelSession.GameCommands.Add(c);
            ChannelSession.Services.Chat.RebuildCommandTriggers();
            return Task.FromResult(0);
        }

        protected Result ValidateOutcomes(IEnumerable<GameOutcomeViewModel> outcomes)
        {
            if (outcomes.Count() == 0)
            {
                return new Result(MixItUp.Base.Resources.GameCommandAtLeast1Outcome);
            }

            Dictionary<UserRoleEnum, int> probabilities = new Dictionary<UserRoleEnum, int>();
            foreach (RoleProbabilityPayoutViewModel rpp in outcomes.First().RoleProbabilityPayouts)
            {
                probabilities[rpp.Role] = 0;
            }

            foreach (GameOutcomeViewModel outcome in outcomes)
            {
                Result result = outcome.Validate();
                if (!result.Success)
                {
                    return result;
                }

                foreach (RoleProbabilityPayoutViewModel rpp in outcome.RoleProbabilityPayouts)
                {
                    probabilities[rpp.Role] += rpp.Probability;
                }
            }

            foreach (var kvp in probabilities)
            {
                if (kvp.Value != 100)
                {
                    return new Result(string.Format(MixItUp.Base.Resources.GameCommandRoleProbabilityNotEqualTo100, EnumLocalizationHelper.GetLocalizedName(kvp.Key)));
                }
            }

            return new Result();
        }

        protected CustomCommandModel CreateBasicChatCommand() { return new CustomCommandModel(MixItUp.Base.Resources.GameSubCommand); }

        protected CustomCommandModel CreateBasicChatCommand(string message, bool whisper = false)
        {
            CustomCommandModel command = this.CreateBasicChatCommand();
            command.Actions.Add(new ChatActionModel(message, sendAsStreamer: false, isWhisper: whisper));
            return command;
        }

        protected CustomCommandModel CreateBasic2ChatCommand(string message1, string message2)
        {
            CustomCommandModel command = this.CreateBasicChatCommand();
            command.Actions.Add(new ChatActionModel(message1));
            command.Actions.Add(new ChatActionModel(message2));
            return command;
        }

        protected CustomCommandModel CreateBasicCurrencyCommand(CurrencyModel currency, string amount)
        {
            CustomCommandModel command = this.CreateBasicChatCommand();
            command.Actions.Add(new ConsumablesActionModel(currency, ConsumablesActionTypeEnum.AddToUser, amount));
            return command;
        }

        protected CustomCommandModel CreateBasicChatCurrencyCommand(string message, CurrencyModel currency, string amount, bool whisper = false)
        {
            CustomCommandModel command = this.CreateBasicChatCommand();
            command.Actions.Add(new ChatActionModel(message, sendAsStreamer: false, isWhisper: whisper));
            command.Actions.Add(new ConsumablesActionModel(currency, ConsumablesActionTypeEnum.AddToUser, amount));
            return command;
        }

        private void SetUICommands()
        {
            this.AddOutcomeCommand = this.CreateCommand((parameter) =>
            {
                this.Outcomes.Add(new GameOutcomeViewModel());
                return Task.FromResult(0);
            });

            this.DeleteOutcomeCommand = this.CreateCommand((parameter) =>
            {
                this.Outcomes.Remove((GameOutcomeViewModel)parameter);
                return Task.FromResult(0);
            });
        }
    }
}