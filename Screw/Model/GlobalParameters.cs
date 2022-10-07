using Screw.BaseClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Screw.Model
{
    class GlobalParameters : NotifyPropertyChangedBase
    {


        #region Property

        public string Header_folder { get; set; }
        public string OPIDFile { get; set; }
        
        public string Password { get; set; }
        public bool AutoStart { get; set; }
        public bool DebugMode { get; set; }

        private ushort _CleanDay_short;
        public ushort CleanDay_short
        {
            get { return _CleanDay_short; }
            set { if (_CleanDay_short != value) { _CleanDay_short = value; RaisePropertyChanged("CleanDay_short"); } }
        }
        private ushort _CleanDay_long;
        public ushort CleanDay_long
        {
            get { return _CleanDay_long; }
            set { if (value != _CleanDay_long) { _CleanDay_long = value; RaisePropertyChanged("CleanDay_long"); } }
        }
        public double RobotAngleDelta { get; set; }
        public double MasterHole1_X { get; set; }
        public double MasterHole1_Y { get; set; }
        public double MasterHole2_X { get; set; }
        public double MasterHole2_Y { get; set; }
  



     
        private Queue<string> _PreviousOPID = new Queue<string>();

        public Queue<string> PreviousOPID
        {
            get { return _PreviousOPID; }
            set { if (_PreviousOPID != value) { _PreviousOPID = value; RaisePropertyChanged("PreviousOPID"); } }
        }

        private Queue<ushort> _PreviousFixture = new Queue<ushort>();

        public Queue<ushort> PreviousFixture
        {
            get { return _PreviousFixture; }
            set { if (_PreviousFixture != value) { _PreviousFixture = value; RaisePropertyChanged("PreviousFixture"); } }
        }

        

        private Queue<string> _FormerOPID = new Queue<string>();

        public Queue<string> FormerOPID
        {
            get { return _FormerOPID; }
            set { if (_FormerOPID != value) { _FormerOPID = value; RaisePropertyChanged("FormerOPID"); } }
        }


        private Queue<ushort> _PrevioseFixture = new Queue<ushort>();
        public Queue<ushort> PrevioseFixture
        {
            get { return _PrevioseFixture; }
            set { if (_PrevioseFixture != value) { _PrevioseFixture = value; RaisePropertyChanged("PrevioseFixture"); } }
        }

        private Queue<string> _Drop12FormerOPID = new Queue<string>();

        public Queue<string> Drop12FormerOPID
        {
            get { return _Drop12FormerOPID; }
            set { if (_Drop12FormerOPID != value) { _Drop12FormerOPID = value; RaisePropertyChanged("Drop12FormerOPID"); } }
        }


        private Queue<ushort> _Drop12PrevioseFixture = new Queue<ushort>();
        public Queue<ushort> Drop12PrevioseFixture
        {
            get { return _Drop12PrevioseFixture; }
            set { if (_Drop12PrevioseFixture != value) { _Drop12PrevioseFixture = value; RaisePropertyChanged("Drop12PrevioseFixture"); } }
        }

        private Queue<double> _ShiftXSC = new Queue<double>();

        public Queue<double> ShiftXSC
        {
            get { return _ShiftXSC; }
            set { if (_ShiftXSC != value) { _ShiftXSC = value; RaisePropertyChanged("ShiftXSC"); } }
        }
        private Queue<double> _ShiftYSC = new Queue<double>();

        public Queue<double> ShiftYSC
        {
            get { return _ShiftYSC; }
            set { if (_ShiftYSC != value) { _ShiftYSC = value; RaisePropertyChanged("ShiftYSC"); } }
        }


        public double MasterDistanceX { get; set; }

        public double Master1Angle { get; set; }
       
   

        /// <summary>
        /// Amount of Tablet
        /// </summary>
        private double _TotalTablet;

        public double TotalTablet
        {
            get { return _TotalTablet; }
            set{ if (value != _TotalTablet){ _TotalTablet = value;RaisePropertyChanged("TotalTablet"); } }
        }

        /// <summary>
        /// Amount of TabletNG
        /// </summary>
        private double _TabletNG;

        public double TabletNG
        {
            get { return _TabletNG; }
            set { if (value != _TabletNG) { _TabletNG = value; RaisePropertyChanged("TabletNG"); } }
        }
      

        /// <summary>
        /// The TabletNG  Ratio
        /// </summary>
        private double _TabletNGRatio;

        public double TabletNGRatio
        {
            get { return _TabletNGRatio; }
            set { if (value != _TabletNGRatio) { _TabletNGRatio = value; RaisePropertyChanged("TabletNGRatio"); } }
        }

        /// <summary>
        /// TabletNGRatio*360
        /// </summary>
        private double _TabletNGRatio360;

        public double TabletNGRatio360
        {
            get { return _TabletNGRatio360; }
            set { if (value != _TabletNGRatio360) { _TabletNGRatio360 = value; RaisePropertyChanged("TabletNGRatio360"); } }
        }



        /// <summary>
        /// Amount of Motor
        /// </summary>
        private double _TotalMotor;
        public double TotalMotor
        {
            get { return _TotalMotor; }
            set { if (value != _TotalMotor) { _TotalMotor = value; RaisePropertyChanged("TotalMotor"); } }
        }


        /// <summary>
        /// Amount of MotorNG
        /// </summary>
        private double _MotorNG;
        public double MotorNG
        {
            get { return _MotorNG; }
            set { if (value != _MotorNG) { _MotorNG = value; RaisePropertyChanged("MotorNG"); } }
        }


        /// <summary>
        /// The MotorNG  Ratio
        /// </summary>
        private double _MotorNGRatio;
        public double MotorNGRatio
        {
            get { return _MotorNGRatio; }
            set { if (value != _MotorNGRatio) { _MotorNGRatio = value; RaisePropertyChanged("MotorNGRatio"); } }
        }


        private double _MotorNGRatio360;
        public double MotorNGRatio360
        {
            get { return _MotorNGRatio360; }
            set { if (value != _MotorNGRatio360) { _MotorNGRatio360 = value; RaisePropertyChanged("MotorNGRatio360"); } }
        }

        public bool MasterSecondHole { get; set; }

        public double OriginX { get; set; }

        public double OriginY { get; set; }

        public double XRatio { get; set; }

        public double YRatio { get; set; }

        public double LimitOriginXY { get; set; }

        #endregion

    }
}
