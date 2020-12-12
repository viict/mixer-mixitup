﻿using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Commands.Games;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Util;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Games
{
    public class HitmanGameCommandEditorWindowViewModel : GameCommandEditorWindowViewModelBase
    {
        public int MinimumParticipants
        {
            get { return this.minimumParticipants; }
            set
            {
                this.minimumParticipants = value;
                this.NotifyPropertyChanged();
            }
        }
        private int minimumParticipants;

        public int TimeLimit
        {
            get { return this.timeLimit; }
            set
            {
                this.timeLimit = value;
                this.NotifyPropertyChanged();
            }
        }
        private int timeLimit;

        public int HitmanTimeLimit
        {
            get { return this.hitmanTimeLimit; }
            set
            {
                this.hitmanTimeLimit = value;
                this.NotifyPropertyChanged();
            }
        }
        private int hitmanTimeLimit;

        public string CustomWordsFilePath
        {
            get { return this.customWordsFilePath; }
            set
            {
                this.customWordsFilePath = value;
                this.NotifyPropertyChanged();
            }
        }
        private string customWordsFilePath;

        public CustomCommandModel StartedCommand
        {
            get { return this.startedCommand; }
            set
            {
                this.startedCommand = value;
                this.NotifyPropertyChanged();
            }
        }
        private CustomCommandModel startedCommand;

        public CustomCommandModel UserJoinCommand
        {
            get { return this.userJoinCommand; }
            set
            {
                this.userJoinCommand = value;
                this.NotifyPropertyChanged();
            }
        }
        private CustomCommandModel userJoinCommand;

        public CustomCommandModel NotEnoughPlayersCommand
        {
            get { return this.notEnoughPlayersCommand; }
            set
            {
                this.notEnoughPlayersCommand = value;
                this.NotifyPropertyChanged();
            }
        }
        private CustomCommandModel notEnoughPlayersCommand;

        public CustomCommandModel HitmanApproachingCommand
        {
            get { return this.hitmanApproachingCommand; }
            set
            {
                this.hitmanApproachingCommand = value;
                this.NotifyPropertyChanged();
            }
        }
        private CustomCommandModel hitmanApproachingCommand;

        public CustomCommandModel HitmanAppearsCommand
        {
            get { return this.hitmanAppearsCommand; }
            set
            {
                this.hitmanAppearsCommand = value;
                this.NotifyPropertyChanged();
            }
        }
        private CustomCommandModel hitmanAppearsCommand;

        public CustomCommandModel UserSuccessCommand
        {
            get { return this.userSuccessCommand; }
            set
            {
                this.userSuccessCommand = value;
                this.NotifyPropertyChanged();
            }
        }
        private CustomCommandModel userSuccessCommand;

        public CustomCommandModel UserFailureCommand
        {
            get { return this.userFailureCommand; }
            set
            {
                this.userFailureCommand = value;
                this.NotifyPropertyChanged();
            }
        }
        private CustomCommandModel userFailureCommand;

        public ICommand BrowseCustomWordsFilePathCommand { get; set; }

        public HitmanGameCommandEditorWindowViewModel(HitmanGameCommandModel command)
            : base(command)
        {
            this.MinimumParticipants = command.MinimumParticipants;
            this.TimeLimit = command.TimeLimit;
            this.HitmanTimeLimit = command.HitmanTimeLimit;
            this.CustomWordsFilePath = command.CustomWordsFilePath;
            this.StartedCommand = command.StartedCommand;
            this.UserJoinCommand = command.UserJoinCommand;
            this.NotEnoughPlayersCommand = command.NotEnoughPlayersCommand;
            this.HitmanApproachingCommand = command.HitmanApproachingCommand;
            this.HitmanAppearsCommand = command.HitmanAppearsCommand;
            this.UserSuccessCommand = command.UserSuccessCommand;
            this.UserFailureCommand = command.UserFailureCommand;

            this.SetUICommands();
        }

        public HitmanGameCommandEditorWindowViewModel(CurrencyModel currency)
            : base(currency)
        {
            this.MinimumParticipants = 2;
            this.TimeLimit = 60;
            this.HitmanTimeLimit = 30;
            this.StartedCommand = this.CreateBasicChatCommand(MixItUp.Base.Resources.GameCommandHitmanStartedExample);
            this.UserJoinCommand = this.CreateBasicChatCommand();
            this.NotEnoughPlayersCommand = this.CreateBasicChatCommand(MixItUp.Base.Resources.GameCommandNotEnoughPlayersExample);
            this.HitmanApproachingCommand = this.CreateBasicChatCommand(MixItUp.Base.Resources.GameCommandHitmanApproachingExample);
            this.HitmanAppearsCommand = this.CreateBasicChatCommand(MixItUp.Base.Resources.GameCommandHitmanAppearsExample);
            this.UserSuccessCommand = this.CreateBasicChatCommand(string.Format(MixItUp.Base.Resources.GameCommandHItmanUserSuccessExample, currency.Name));
            this.UserFailureCommand = this.CreateBasicChatCommand();

            this.SetUICommands();
        }

        public override Task<CommandModelBase> GetCommand()
        {
            return Task.FromResult<CommandModelBase>(new HitmanGameCommandModel(this.Name, this.GetChatTriggers(), this.MinimumParticipants, this.TimeLimit, this.HitmanTimeLimit, this.CustomWordsFilePath,
                this.StartedCommand, this.UserJoinCommand, this.NotEnoughPlayersCommand, this.HitmanApproachingCommand, this.HitmanAppearsCommand, this.UserSuccessCommand, this.UserFailureCommand));
        }

        public override async Task<Result> Validate()
        {
            Result result = await base.Validate();
            if (!result.Success)
            {
                return result;
            }

            if (this.MinimumParticipants < 1)
            {
                return new Result(MixItUp.Base.Resources.GameCommandMinimumParticipantsMustBeGreaterThan0);
            }

            if (this.TimeLimit <= 0 || this.HitmanTimeLimit <= 0)
            {
                return new Result(MixItUp.Base.Resources.GameCommandTimeLimitMustBePositive);
            }

            return new Result();
        }

        private void SetUICommands()
        {
            this.BrowseCustomWordsFilePathCommand = this.CreateCommand((parameter) =>
            {
                string filePath = ChannelSession.Services.FileService.ShowOpenFileDialog(ChannelSession.Services.FileService.TextFileFilter());
                if (!string.IsNullOrEmpty(filePath))
                {
                    this.CustomWordsFilePath = filePath;
                }
                return Task.FromResult(0);
            });
        }
    }
}