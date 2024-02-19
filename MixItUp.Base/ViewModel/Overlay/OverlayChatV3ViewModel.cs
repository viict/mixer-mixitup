﻿using MixItUp.Base.Model;
using MixItUp.Base.Model.Overlay;
using MixItUp.Base.Model.Overlay.Widgets;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.Trovo;
using MixItUp.Base.ViewModel.Chat.Twitch;
using MixItUp.Base.ViewModel.Chat.YouTube;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.ViewModel.Overlay
{
    public class OverlayChatV3ViewModel : OverlayVisualTextV3ViewModelBase
    {
        public override string DefaultHTML { get { return OverlayChatV3Model.DefaultHTML; } }
        public override string DefaultCSS { get { return OverlayChatV3Model.DefaultCSS; } }
        public override string DefaultJavascript { get { return OverlayChatV3Model.DefaultJavascript; } }

        public string Height
        {
            get { return this.height > 0 ? this.height.ToString() : string.Empty; }
            set
            {
                this.height = this.GetPositiveIntFromString(value);
                this.NotifyPropertyChanged();
            }
        }
        protected int height;

        public string BackgroundColor
        {
            get { return this.backgroundColor; }
            set
            {
                this.backgroundColor = value;
                this.NotifyPropertyChanged();
            }
        }
        private string backgroundColor;

        public string BorderColor
        {
            get { return this.borderColor; }
            set
            {
                this.borderColor = value;
                this.NotifyPropertyChanged();
            }
        }
        private string borderColor;

        public string MaxMessages
        {
            get { return (this.maxMessages > 0) ? this.maxMessages.ToString() : string.Empty; }
            set
            {
                this.maxMessages = this.GetPositiveIntFromString(value);
                this.NotifyPropertyChanged();
            }
        }
        private int maxMessages;

        public int MessageDelayTime
        {
            get { return this.messageDelayTime; }
            set
            {
                this.messageDelayTime = Math.Max(value, 0);
                this.NotifyPropertyChanged();
            }
        }
        private int messageDelayTime;

        public int MessageRemovalTime
        {
            get { return this.messageRemovalTime; }
            set
            {
                this.messageRemovalTime = Math.Max(value, 0);
                this.NotifyPropertyChanged();
            }
        }
        private int messageRemovalTime;

        public bool AddMessagesToTop
        {
            get { return this.addMessagesToTop; }
            set
            {
                this.addMessagesToTop = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool addMessagesToTop;

        public bool IgnoreSpecialtyExcludedUsers
        {
            get { return this.ignoreSpecialtyExcludedUsers; }
            set
            {
                this.ignoreSpecialtyExcludedUsers = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool ignoreSpecialtyExcludedUsers;

        public ObservableCollection<string> UsernamesToIgnore { get; set; } = new ObservableCollection<string>();

        public bool ShowPlatformBadge
        {
            get { return this.showPlatformBadge; }
            set
            {
                this.showPlatformBadge = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool showPlatformBadge;

        public bool ShowRoleBadge
        {
            get { return this.showRoleBadge; }
            set
            {
                this.showRoleBadge = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool showRoleBadge;

        public bool ShowSubscriberBadge
        {
            get { return this.showSubscriberBadge; }
            set
            {
                this.showSubscriberBadge = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool showSubscriberBadge;

        public bool ShowSpecialtyBadge
        {
            get { return this.showSpecialtyBadge; }
            set
            {
                this.showSpecialtyBadge = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool showSpecialtyBadge;

        public OverlayChatV3ViewModel()
            : base(OverlayItemV3Type.Chat)
        {
            this.ShowPlatformBadge = true;
            this.ShowRoleBadge = true;
            this.ShowSubscriberBadge = true;
            this.ShowSpecialtyBadge = true;
        }

        public OverlayChatV3ViewModel(OverlayChatV3Model item)
            : base(item)
        {
            this.height = item.Height;

            this.BackgroundColor = item.BackgroundColor;
            this.BorderColor = item.BorderColor;

            this.maxMessages = item.MaxMessages;
            this.MessageDelayTime = item.MessageDelayTime;
            this.MessageRemovalTime = item.MessageRemovalTime;
            this.AddMessagesToTop = item.AddMessagesToTop;

            this.IgnoreSpecialtyExcludedUsers = item.IgnoreSpecialtyExcludedUsers;
            foreach (string username in item.UsernamesToIgnore)
            {
                this.UsernamesToIgnore.Add(username);
            }

            this.ShowPlatformBadge = item.ShowPlatformBadge;
            this.ShowRoleBadge = item.ShowRoleBadge;
            this.ShowSubscriberBadge = item.ShowSubscriberBadge;
            this.ShowSpecialtyBadge = item.ShowSpecialtyBadge;
        }

        public override Result Validate()
        {
            return new Result();
        }

        public override async Task TestWidget(OverlayWidgetV3Model widget)
        {
            OverlayChatV3Model chat = (OverlayChatV3Model)widget.Item;

            foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.GetConnectedPlatforms())
            {
                if (platform == StreamingPlatformTypeEnum.Twitch)
                {
                    TwitchChatMessageViewModel message = new TwitchChatMessageViewModel(ChannelSession.User, "Hello World! This is a test message so you can see how chat looks Kappa");
                    await chat.AddMessage(message);
                }
                else if (platform == StreamingPlatformTypeEnum.YouTube)
                {
                    YouTubeChatMessageViewModel message = new YouTubeChatMessageViewModel(ChannelSession.User, "Hello World! This is a test message so you can see how chat looks :grinning_face:");
                    await chat.AddMessage(message);
                }
                else if (platform == StreamingPlatformTypeEnum.Trovo)
                {
                    TrovoChatMessageViewModel message = new TrovoChatMessageViewModel(ChannelSession.User, "Hello World! This is a test message so you can see how chat looks :smile");
                    await chat.AddMessage(message);
                }
                else
                {
                    ChatMessageViewModel message = new ChatMessageViewModel(Guid.NewGuid().ToString(), platform, ChannelSession.User);
                    message.AddStringMessagePart("Hello World! This is a test message so you can see how chat looks");
                    await chat.AddMessage(message);
                }
            }

            await base.TestWidget(widget);
        }

        protected override OverlayItemV3ModelBase GetItemInternal()
        {
            OverlayChatV3Model result = new OverlayChatV3Model()
            {
                Height = this.height,

                BackgroundColor = this.BackgroundColor,
                BorderColor = this.BorderColor,

                MaxMessages = this.maxMessages,
                MessageDelayTime = this.MessageRemovalTime,
                MessageRemovalTime = this.MessageRemovalTime,
                AddMessagesToTop = this.AddMessagesToTop,

                IgnoreSpecialtyExcludedUsers = this.IgnoreSpecialtyExcludedUsers,
                UsernamesToIgnore = this.UsernamesToIgnore.ToList(),

                ShowPlatformBadge = this.ShowPlatformBadge,
                ShowRoleBadge = this.ShowRoleBadge,
                ShowSubscriberBadge = this.ShowSubscriberBadge,
                ShowSpecialtyBadge = this.ShowSpecialtyBadge,
            };
            this.AssignProperties(result);
            return result;
        }
    }
}
