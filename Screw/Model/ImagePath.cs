using Screw.BaseClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Screw.Model
{
    class ImagePath : NotifyPropertyChangedBase
    {



        private BitmapSource _ImageOK;
        public BitmapSource ImageOK
        {
            get { return _ImageOK; }
            set
            {
                if (value != _ImageOK)
                {
                    _ImageOK = value;
                    //_ImageOK.Freeze();//不用freez也可
                    RaisePropertyChanged("ImageOK");
                }
            }
        }
        private BitmapSource _ImageNG;
        public BitmapSource ImageNG
        {
            get { return _ImageNG; }
            set
            {
                if (value != _ImageNG)
                {
                    _ImageNG = value;
                    _ImageNG.Freeze();//不用freez也可
                    RaisePropertyChanged("ImageNG");
                }
            }
        }
        private string _Name;
        public string Name
        {
            get { return _Name; }
            set
            {
                if (value != _Name)
                {
                    _Name = value;
                    RaisePropertyChanged("Name");
                }
            }
        }

        private string _Label;
        public string Label
        {
            get { return _Label; }
            set
            {
                if (value != _Label)
                {
                    _Label = value;
                    RaisePropertyChanged("Label");
                }
            }
        }

        private string _NameNG;
        public string NameNG
        {
            get { return _NameNG; }
            set
            {
                if (value != _NameNG)
                {
                    _NameNG = value;
                    RaisePropertyChanged("NameNG");
                }
            }
        }

        private string _LabelNG;
        public string LabelNG
        {
            get { return _LabelNG; }
            set
            {
                if (value != _LabelNG)
                {
                    _LabelNG = value;
                    RaisePropertyChanged("LabelNG");
                }
            }
        }

    }
}
