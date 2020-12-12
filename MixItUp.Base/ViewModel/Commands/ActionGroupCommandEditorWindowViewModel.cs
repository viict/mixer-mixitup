﻿using MixItUp.Base.Model.Commands;
using MixItUp.Base.Util;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Commands
{
    public class ActionGroupCommandEditorWindowViewModel : CommandEditorWindowViewModelBase
    {
        public ActionGroupCommandEditorWindowViewModel(ActionGroupCommandModel existingCommand)
            : base(existingCommand)
        { }

        public ActionGroupCommandEditorWindowViewModel() : base() { }

        public override Task<Result> Validate()
        {
            if (string.IsNullOrEmpty(this.Name))
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.ACommandNameMustBeSpecified));
            }
            return Task.FromResult(new Result());
        }

        public override Task<CommandModelBase> GetCommand() { return Task.FromResult<CommandModelBase>(new ActionGroupCommandModel(this.Name)); }

        public override Task SaveCommandToSettings(CommandModelBase command)
        {
            ActionGroupCommandModel c = (ActionGroupCommandModel)command;
            ChannelSession.ActionGroupCommands.Remove(c);
            ChannelSession.ActionGroupCommands.Add(c);
            return Task.FromResult(0);
        }
    }
}