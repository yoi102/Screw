using OpenCvSharp;
using Screw.BaseClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Screw.Model
{
    class ScrewPartParameters : NotifyPropertyChangedBase

    {
        /// <summary>
        /// 整体图片旋转的角度，需要经量使马达的丝杆保持垂直于图片
        /// </summary>
        public float RotateAngle_Screw { get; set; }
        /// <summary>
        /// 定位点的ROI位置
        /// </summary>
        public int Roi_DatumPointRange_X { get; set; }
        /// <summary>
        /// 定位点的ROI位置
        /// </summary>
        public int Roi_DatumPointRange_Y { get; set; }
        /// <summary>
        /// 定位点的ROI位置
        /// </summary>
        public int Roi_DatumPointRange_W { get; set; }
        /// <summary>
        /// 定位点的ROI位置
        /// </summary>
        public int Roi_DatumPointRange_H { get; set; }

        /// <summary>
        /// 马达框的大概ROI位置(相对与定位点来说的位置)
        /// </summary>
        public int Roi_Motor_L { get; set; }
        /// <summary>
        /// 马达框的大概ROI位置(相对与定位点来说的位置)
        /// </summary>
        public int Roi_Motor_T { get; set; }
        /// <summary>
        /// 马达框的大概ROI位置(相对与定位点来说的位置)
        /// </summary>
        public int Roi_Motor_W { get; set; }
        /// <summary>
        /// 马达框的大概ROI位置(相对与定位点来说的位置)
        /// </summary>
        public int Roi_Motor_H { get; set; }
        /// <summary>
        /// 测量圆孔的大概ROI位置(相对与定位点来说的位置)
        /// </summary>
        public int Roi_Circular_L { get; set; }
        /// <summary>
        /// 测量圆孔的大概ROI位置(相对与定位点来说的位置)
        /// </summary>
        public int Roi_Circular_T { get; set; }
        /// <summary>
        /// 测量圆孔的大概ROI位置(相对与定位点来说的位置)
        /// </summary>
        public int Roi_Circular_W { get; set; }
        /// <summary>
        /// 测量圆孔的大概ROI位置(相对与定位点来说的位置)
        /// </summary>
        public int Roi_Circular_H { get; set; }
        /// <summary>
        /// 压轴ROI图的位置(相对与定位点来说的位置)
        /// </summary>
        public int Roi_ATablet_X { get; set; }
        /// <summary>
        /// 压轴ROI图的位置(相对与定位点来说的位置)
        /// </summary>
        public int Roi_ATablet_Y { get; set; }
        /// <summary>
        /// 压轴ROI图的位置(相对与定位点来说的位置)
        /// </summary>
        public int Roi_ATablet_W { get; set; }
        /// <summary>
        /// 压轴ROI图的位置(相对与定位点来说的位置)
        /// </summary>
        public int Roi_ATablet_H { get; set; }

        private ushort _OpCircular_W;
        /// <summary>
        /// OP架圆孔Bolb的大小（尽量使这个值为bolb的大小）
        /// </summary>
        public ushort OpCircular_W
        {
            get { return _OpCircular_W; }
            set { if (_OpCircular_W != value) { _OpCircular_W = value; RaisePropertyChanged("OpCircular_W"); } }
        }
        private ushort _OpCircularRangeLimit_W;
        /// <summary>
        /// 测量OP圆孔用的限制
        /// </summary>
        public ushort OpCircularRangeLimit_W
        {
            get { return _OpCircularRangeLimit_W; }
            set { if (_OpCircularRangeLimit_W != value) { _OpCircularRangeLimit_W = value; RaisePropertyChanged("OpCircularRangeLimit_W"); } }
        }
    
        private ushort _OpCircularThresh;
        /// <summary>
        /// 检测OP架上的孔时，用的二值化阈值
        /// </summary>
        public ushort OpCircularThresh
        {
            get { return _OpCircularThresh; }
            set { if (_OpCircularThresh != value) { _OpCircularThresh = value; RaisePropertyChanged("OpCircularThresh"); } }
        }
        private ushort _MotorCircularThresh;
        /// <summary>
        /// 检测马达钣金上的孔时，用的二值化阈值
        /// </summary>
        public ushort MotorCircularThresh
        {
            get { return _MotorCircularThresh; }
            set { if (_MotorCircularThresh != value) { _MotorCircularThresh = value; RaisePropertyChanged("MotorCircularThresh"); } }
        }
        private ushort _MotorCircular_W;
        /// <summary>
        /// 马达钣金Bolb的大小（尽量使这个值为bolb的大小）
        /// </summary>
        public ushort MotorCircular_W
        {
            get { return _MotorCircular_W; }
            set { if (_MotorCircular_W != value) { _MotorCircular_W = value; RaisePropertyChanged("MotorCircular_W"); } }
        }
        private ushort _MotorCircularRangLimit_W;
        /// <summary>
        /// 测量马达钣金圆孔用的限制
        /// </summary>
        public ushort MotorCircularRangLimit_W
        {
            get { return _MotorCircularRangLimit_W; }
            set { if (_MotorCircularRangLimit_W != value) { _MotorCircularRangLimit_W = value; RaisePropertyChanged("MotorCircularRangLimit_W"); } }
        }
      



        private ushort _DeltaXYLimit;
        /// <summary>
        /// 限制Op圆孔和马达圆孔的，质心的差值，当差值大于此值时，将判断圆孔NG
        /// </summary>
        public ushort DeltaXYLimit
        {
            get { return _DeltaXYLimit; }
            set { if (_DeltaXYLimit != value) { _DeltaXYLimit = value; RaisePropertyChanged("DeltaXYLimit"); } }
        }


     


        private ushort _ScrewDatumPointBlobThresh;
        /// <summary>
        /// 检测螺丝的定位物体时的二值化值
        /// </summary>
        public ushort ScrewDatumPointBlobThresh
        {
            get { return _ScrewDatumPointBlobThresh; }
            set { if (_ScrewDatumPointBlobThresh != value) { _ScrewDatumPointBlobThresh = value; RaisePropertyChanged("ScrewDatumPointBlobThresh"); } }
        }


        private double _MasterCircularThresh;
        /// <summary>
        /// 检测Master圆孔时用的，二值化阈值
        /// </summary>
        public double MasterCircularThresh
        {
            get { return _MasterCircularThresh; }
            set { if (_MasterCircularThresh != value) { _MasterCircularThresh = value; RaisePropertyChanged("MasterCircularThresh"); } }
        }
        private double _MasterCircular_W;
        /// <summary>
        /// 检测Master圆孔的Blob大小
        /// </summary>
        public double MasterCircular_W
        {
            get { return _MasterCircular_W; }
            set { if (_MasterCircular_W != value) { _MasterCircular_W = value; RaisePropertyChanged("MasterCircular_W"); } }
        }
        private double _MasterCircularRange_W;
        /// <summary>
        /// 检测Master圆孔的Blob与设定值相差的范围
        /// </summary>
        public double MasterCircularRange_W
        {
            get { return _MasterCircularRange_W; }
            set { if (_MasterCircularRange_W != value) { _MasterCircularRange_W = value; RaisePropertyChanged("MasterCircularRange_W"); } }
        }
    




    }
}
