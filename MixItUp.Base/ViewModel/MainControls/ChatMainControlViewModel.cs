﻿using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.User;
using MixItUp.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;

namespace MixItUp.Base.ViewModel.MainControls
{
    public class ChatMainControlViewModel : ChatListControlViewModel
    {
        public IEnumerable<UserViewModel> DisplayUsers { get; private set; }

        public bool ShowViewerAndChatterNumbers { get { return !ChannelSession.Settings.HideViewerAndChatterNumbers; } }

        public bool ShowChatUserList { get { return !ChannelSession.Settings.HideChatUserList; } }

        public long ViewersCount
        {
            get { return this.viewersCount; }
            set
            {
                this.viewersCount = value;
                this.NotifyPropertyChanged();
            }
        }
        private long viewersCount;

        public long ChattersCount
        {
            get { return this.chattersCount; }
            set
            {
                this.chattersCount = value;
                this.NotifyPropertyChanged();
            }
        }
        private long chattersCount;

        public string EnableDisableButtonText
        {
            get { return this.enableDisableButtonText; }
            set
            {
                this.enableDisableButtonText = value;
                this.NotifyPropertyChanged();
            }
        }
        private string enableDisableButtonText = MixItUp.Base.Resources.DisableChat;

        public ICommand ClearChatCommand { get; private set; }
        public ICommand EnableDisableChatCommand { get; private set; }

        public ChatMainControlViewModel(MainWindowViewModel windowViewModel)
            : base(windowViewModel)
        {
            this.ClearChatCommand = this.CreateCommand(async (parameter) =>
            {
                if (await DialogHelper.ShowConfirmation(MixItUp.Base.Resources.ClearChatConfirmation))
                {
                    await ServiceManager.Get<ChatService>().ClearMessages();
                }
            });

            this.EnableDisableChatCommand = this.CreateCommand(async (parameter) =>
            {
                if (!ServiceManager.Get<ChatService>().DisableChat && !await DialogHelper.ShowConfirmation(MixItUp.Base.Resources.DisableChatConfirmation))
                {
                    return;
                }

                ServiceManager.Get<ChatService>().DisableChat = !ServiceManager.Get<ChatService>().DisableChat;
                if (ServiceManager.Get<ChatService>().DisableChat)
                {
                    this.EnableDisableButtonText = MixItUp.Base.Resources.EnableChat;
                }
                else
                {
                    this.EnableDisableButtonText = MixItUp.Base.Resources.DisableChat;
                }
            });
        }

        protected override async Task OnLoadedInternal()
        {
            await base.OnLoadedInternal();

            ServiceManager.Get<ChatService>().DisplayUsersUpdated += ChatService_DisplayUsersUpdated;
            this.DisplayUsers = ServiceManager.Get<ChatService>().DisplayUsers;

            this.Messages.CollectionChanged += Messages_CollectionChanged;

            this.RefreshNumbers();
        }

        protected override async Task OnVisibleInternal()
        {
            await base.OnVisibleInternal();

            this.NotifyPropertyChanged("ShowViewerAndChatterNumbers");
            this.NotifyPropertyChanged("ShowChatUserList");
        }

        private void Messages_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.RefreshNumbers();
        }

        private void RefreshNumbers()
        {
            if (ServiceManager.Get<TwitchSessionService>().StreamV5 != null)
            {
                this.ViewersCount = ServiceManager.Get<TwitchSessionService>().StreamV5.viewers;
            }
            this.ChattersCount = ServiceManager.Get<ChatService>().AllUsers.Count;
        }

        private void ChatService_DisplayUsersUpdated(object sender, EventArgs e)
        {
            this.DisplayUsers = ServiceManager.Get<ChatService>().DisplayUsers;
            this.NotifyPropertyChanged("DisplayUsers");
        }
    }
}
