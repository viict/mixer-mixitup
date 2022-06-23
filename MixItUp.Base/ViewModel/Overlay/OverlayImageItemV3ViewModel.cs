﻿using MixItUp.Base.Model.Overlay;
using MixItUp.Base.Services;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Overlay
{
    public class OverlayImageItemV3ViewModel : OverlayItemV3ViewModelBase
    {
        public string ImagePath
        {
            get { return this.imagePath; }
            set
            {
                this.imagePath = value;
                this.NotifyPropertyChanged();
            }
        }
        private string imagePath;

        public string Width
        {
            get { return this.width > 0 ? this.width.ToString() : string.Empty; }
            set
            {
                this.width = this.GetPositiveIntFromString(value);
                this.NotifyPropertyChanged();
            }
        }
        private int width;

        public string Height
        {
            get { return this.height > 0 ? this.height.ToString() : string.Empty; }
            set
            {
                this.height = this.GetPositiveIntFromString(value);
                this.NotifyPropertyChanged();
            }
        }
        private int height;

        public ICommand BrowseFilePathCommand { get; set; }

        public OverlayImageItemV3ViewModel()
            : base(OverlayItemV3Type.Image)
        {
            this.SetCommands();
        }

        public OverlayImageItemV3ViewModel(OverlayImageItemV3Model item)
            : base(item)
        {
            this.ImagePath = item.ImagePath;
            this.width = item.Width;
            this.height = item.Height;

            this.SetCommands();
        }

        public OverlayImageItemV3Model GetItem()
        {
            OverlayImageItemV3Model result = new OverlayImageItemV3Model()
            {
                HTML = this.HTML,
                CSS = this.CSS,
                Javascript = this.Javascript,

                ImagePath = this.ImagePath,
                Width = this.width,
                Height = this.height,
            };

            return result;
        }

        private void SetCommands()
        {
            this.BrowseFilePathCommand = this.CreateCommand(() =>
            {
                string filepath = ServiceManager.Get<IFileService>().ShowOpenFileDialog(ServiceManager.Get<IFileService>().ImageFileFilter());
                if (!string.IsNullOrEmpty(filepath))
                {
                    this.ImagePath = filepath;
                }
            });
        }
    }
}
