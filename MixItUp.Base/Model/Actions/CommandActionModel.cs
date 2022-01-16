﻿using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum CommandActionTypeEnum
    {
        RunCommand,
        DisableCommand,
        EnableCommand,
        DisableCommandGroup,
        EnableCommandGroup,
        CancelAllCommands,
        PauseAllCommands,
        UnpauseAllCommands,
    }

    [DataContract]
    public class CommandActionModel : ActionModelBase
    {
        [DataMember]
        public CommandActionTypeEnum ActionType { get; set; }

        [DataMember]
        public Guid CommandID { get; set; }
        [DataMember]
        public Type PreMadeType { get; set; }

        [DataMember]
        public string Arguments { get; set; }
        [DataMember]
        public bool WaitForCommandToFinish { get; set; }

        [DataMember]
        public string CommandGroupName { get; set; }

        public CommandActionModel(CommandActionTypeEnum commandActionType, CommandModelBase command, string commandArguments, bool waitForCommandToFinish)
            : this(commandActionType)
        {
            if (command is PreMadeChatCommandModelBase)
            {
                this.PreMadeType = command.GetType();
                this.CommandID = Guid.Empty;
            }
            else
            {
                this.CommandID = command.ID;
                this.PreMadeType = null;
            }
            this.Arguments = commandArguments;
            this.WaitForCommandToFinish = waitForCommandToFinish;
        }

        public CommandActionModel(CommandActionTypeEnum commandActionType, string groupName)
            : this(commandActionType)
        {
            this.CommandGroupName = groupName;
        }

        public CommandActionModel(CommandActionTypeEnum commandActionType)
            : base(ActionTypeEnum.Command)
        {
            this.ActionType = commandActionType;
        }

        [Obsolete]
        public CommandActionModel() { }

        public CommandModelBase Command
        {
            get
            {
                if (this.PreMadeType != null)
                {
                    return ServiceManager.Get<CommandService>().PreMadeChatCommands.FirstOrDefault(c => c.GetType().Equals(this.PreMadeType));
                }
                else
                {
                    return ServiceManager.Get<CommandService>().AllCommands.FirstOrDefault(c => c.ID.Equals(this.CommandID));
                }
            }
        }

        public IEnumerable<CommandModelBase> CommandGroup
        {
            get
            {
                return ServiceManager.Get<CommandService>().AllCommands.Where(c => string.Equals(this.CommandGroupName, c.GroupName)).ToList();
            }
        }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            CommandModelBase command = this.Command;
            if (this.ActionType == CommandActionTypeEnum.RunCommand)
            {
                if (command != null)
                {
                    List<string> newArguments = new List<string>();
                    if (!string.IsNullOrEmpty(this.Arguments))
                    {
                        string processedMessage = await ReplaceStringWithSpecialModifiers(this.Arguments, parameters);
                        newArguments = processedMessage.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    }
                    else
                    {
                        newArguments = parameters.Arguments;
                    }

                    CommandParametersModel copyParameters = parameters.Duplicate();
                    copyParameters.Arguments = newArguments;

                    CommandInstanceModel commandInstance = new CommandInstanceModel(command, copyParameters);
                    if (this.WaitForCommandToFinish)
                    {
                        await ServiceManager.Get<CommandService>().RunDirectlyWithValidation(commandInstance);
                    }
                    else
                    {
                        await ServiceManager.Get<CommandService>().Queue(commandInstance);
                    }
                }
            }
            else if (this.ActionType == CommandActionTypeEnum.DisableCommand || this.ActionType == CommandActionTypeEnum.EnableCommand)
            {
                if (command != null)
                {
                    command.IsEnabled = (this.ActionType == CommandActionTypeEnum.EnableCommand) ? true : false;
                    if (command is ChatCommandModel)
                    {
                        ServiceManager.Get<ChatService>().RebuildCommandTriggers();
                    }
                    else if (command is TimerCommandModel)
                    {
                        await ServiceManager.Get<TimerService>().RebuildTimerGroups();
                    }
                }
            }
            else if (this.ActionType == CommandActionTypeEnum.DisableCommandGroup || this.ActionType == CommandActionTypeEnum.EnableCommandGroup)
            {
                IEnumerable<CommandModelBase> commands = this.CommandGroup;
                if (commands != null)
                {
                    bool chatCommand = false;
                    bool timerCommand = false;
                    foreach (CommandModelBase cmd in commands)
                    {
                        cmd.IsEnabled = (this.ActionType == CommandActionTypeEnum.EnableCommandGroup) ? true : false;
                        ChannelSession.Settings.Commands.ManualValueChanged(cmd.ID);

                        if (cmd is ChatCommandModel)
                        {
                            chatCommand = true;
                        }
                        else if (command is TimerCommandModel)
                        {
                            timerCommand = true;
                        }
                    }

                    if (chatCommand)
                    {
                        ServiceManager.Get<ChatService>().RebuildCommandTriggers();
                    }
                    if (timerCommand)
                    {
                        await ServiceManager.Get<TimerService>().RebuildTimerGroups();
                    }
                }
            }
            else if (this.ActionType == CommandActionTypeEnum.CancelAllCommands)
            {
                foreach (CommandInstanceModel commandInstance in ServiceManager.Get<CommandService>().CommandInstances.Where(c => c.State == CommandInstanceStateEnum.Pending || c.State == CommandInstanceStateEnum.Running))
                {
                    ServiceManager.Get<CommandService>().Cancel(commandInstance);
                }
            }
            else if (this.ActionType == CommandActionTypeEnum.PauseAllCommands)
            {
                await ServiceManager.Get<CommandService>().Pause();
            }
            else if (this.ActionType == CommandActionTypeEnum.UnpauseAllCommands)
            {
                await ServiceManager.Get<CommandService>().Unpause();
            }
        }
    }
}
