using SaGlue.BaseClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaGlue.Model
{
    class MorphologicalParameters : NotifyPropertyChangedBase
    {
        private ushort _PositiveGlueBinValue;
        public ushort PositiveGlueBinValue
        {
            get { return _PositiveGlueBinValue; }
            set { if (_PositiveGlueBinValue != value) { _PositiveGlueBinValue = value; RaisePropertyChanged("PositiveGlueBinValue"); } }
        }
        private ushort _PositiveGlueWidth;
        public ushort PositiveGlueWidth
        {
            get { return _PositiveGlueWidth; }
            set { if (_PositiveGlueWidth != value) { _PositiveGlueWidth = value; RaisePropertyChanged("PositiveGlueWidth"); } }
        }
        private ushort _PositiveGlueWidthLimit;
        public ushort PositiveGlueWidthLimit
        {
            get { return _PositiveGlueWidthLimit; }
            set { if (_PositiveGlueWidthLimit != value) { _PositiveGlueWidthLimit = value; RaisePropertyChanged("PositiveGlueWidthLimit"); } }
        }
        private ushort _PositiveGlueHeight;
        public ushort PositiveGlueHeight
        {
            get { return _PositiveGlueHeight; }
            set { if (_PositiveGlueHeight != value) { _PositiveGlueHeight = value; RaisePropertyChanged("PositiveGlueHeight"); } }
        }
        private ushort _PositiveGlueHeightLimit;
        public ushort PositiveGlueHeightLimit
        {
            get { return _PositiveGlueHeightLimit; }
            set { if (_PositiveGlueHeightLimit != value) { _PositiveGlueHeightLimit = value; RaisePropertyChanged("PositiveGlueHeightLimit"); } }
        }
        private ushort _PositiveGlueArea;
        public ushort PositiveGlueArea
        {
            get { return _PositiveGlueArea; }
            set { if (_PositiveGlueArea != value) { _PositiveGlueArea = value; RaisePropertyChanged("PositiveGlueArea"); } }
        }
        private ushort _PositiveGlueAreaLimit;
        public ushort PositiveGlueAreaLimit
        {
            get { return _PositiveGlueAreaLimit; }
            set { if (_PositiveGlueAreaLimit != value) { _PositiveGlueAreaLimit = value; RaisePropertyChanged("PositiveGlueAreaLimit"); } }
        }



        private ushort _NegativeGlueBinValue;
        public ushort NegativeGlueBinValue
        {
            get { return _NegativeGlueBinValue; }
            set { if (_NegativeGlueBinValue != value) { _NegativeGlueBinValue = value; RaisePropertyChanged("NegativeGlueBinValue"); } }
        }

        private ushort _NegativeGlueWidth;
        public ushort NegativeGlueWidth
        {
            get { return _NegativeGlueWidth; }
            set { if (_NegativeGlueWidth != value) { _NegativeGlueWidth = value; RaisePropertyChanged("NegativeGlueWidth"); } }
        }
        private ushort _NegativeGlueWidthLimit;
        public ushort NegativeGlueWidthLimit
        {
            get { return _NegativeGlueWidthLimit; }
            set { if (_NegativeGlueWidthLimit != value) { _NegativeGlueWidthLimit = value; RaisePropertyChanged("NegativeGlueWidthLimit"); } }
        }
        private ushort _NegativeGlueHeight;
        public ushort NegativeGlueHeight
        {
            get { return _NegativeGlueHeight; }
            set { if (_NegativeGlueHeight != value) { _NegativeGlueHeight = value; RaisePropertyChanged("NegativeGlueHeight"); } }
        }
        private ushort _NegativeGlueHeightLimit;
        public ushort NegativeGlueHeightLimit
        {
            get { return _NegativeGlueHeightLimit; }
            set { if (_NegativeGlueHeightLimit != value) { _NegativeGlueHeightLimit = value; RaisePropertyChanged("NegativeGlueHeightLimit"); } }
        }
        private ushort _NegativeGlueArea;
        public ushort NegativeGlueArea
        {
            get { return _NegativeGlueArea; }
            set { if (_NegativeGlueArea != value) { _NegativeGlueArea = value; RaisePropertyChanged("NegativeGlueArea"); } }
        }
        private ushort _NegativeGlueAreaLimit;
        public ushort NegativeGlueAreaLimit
        {
            get { return _NegativeGlueAreaLimit; }
            set { if (_NegativeGlueAreaLimit != value) { _NegativeGlueAreaLimit = value; RaisePropertyChanged("NegativeGlueAreaLimit"); } }
        }





        private ushort _DatumPointBinValue;
        public ushort DatumPointBinValue
        {
            get { return _DatumPointBinValue; }
            set { if (_DatumPointBinValue != value) { _DatumPointBinValue = value; RaisePropertyChanged("DatumPointBinValue"); } }
        }
        private ushort _DatumPointWidth;
        public ushort DatumPointWidth
        {
            get { return _DatumPointWidth; }
            set { if (_DatumPointWidth != value) { _DatumPointWidth = value; RaisePropertyChanged("DatumPointWidth"); } }
        }
        private ushort _DatumPointWidthLimit;
        public ushort DatumPointWidthLimit
        {
            get { return _DatumPointWidthLimit; }
            set { if (_DatumPointWidthLimit != value) { _DatumPointWidthLimit = value; RaisePropertyChanged("DatumPointWidthLimit"); } }
        }
        private ushort _DatumPointHeight;
        public ushort DatumPointHeight
        {
            get { return _DatumPointHeight; }
            set { if (_DatumPointHeight != value) { _DatumPointHeight = value; RaisePropertyChanged("DatumPointHeight"); } }
        }
        private ushort _DatumPointHeightLimit;
        public ushort DatumPointHeightLimit
        {
            get { return _DatumPointHeightLimit; }
            set { if (_DatumPointHeightLimit != value) { _DatumPointHeightLimit = value; RaisePropertyChanged("DatumPointHeightLimit"); } }
        }
        private ushort _DatumPointArea;
        public ushort DatumPointArea
        {
            get { return _DatumPointArea; }
            set { if (_DatumPointArea != value) { _DatumPointArea = value; RaisePropertyChanged("DatumPointArea"); } }
        }
        private ushort _DatumPointAreaLimit;
        public ushort DatumPointAreaLimit
        {
            get { return _DatumPointAreaLimit; }
            set { if (_DatumPointAreaLimit != value) { _DatumPointAreaLimit = value; RaisePropertyChanged("DatumPointAreaLimit"); } }
        }






    }
}
