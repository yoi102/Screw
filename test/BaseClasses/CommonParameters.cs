using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace test.BaseClasses
{
    class CommonParameters : NotifyPropertyChangedBase
    {

        private string _Name;
        public string Name
        {
            get { return _Name; }
            set { if (_Name != value) { _Name = value; RaisePropertyChanged("Name"); } }
        }


        private double _Height;
        public double Height
        {
            get { return _Height; }
            set { if (_Height != value) { _Height = value; RaisePropertyChanged("Height"); } }
        }


        




        private int _SourceCamIndex;
        public int SourceCamIndex
        {
            get { return _SourceCamIndex; }
            set { if (_SourceCamIndex != value) { _SourceCamIndex = value; RaisePropertyChanged("SourceCamIndex"); } }
        }








    }
}
