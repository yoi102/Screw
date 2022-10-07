using Screw.BaseClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Screw.Model
{
    class GreasePartParameters : NotifyPropertyChangedBase
    {


        /// <summary>
        /// 连续NG时，达到这个个数将弹窗
        /// </summary>
        public ushort GreaseContinuousBadLimit { get; set; }

     


        //亮度限制及其位置
        //第一面的
        /// <summary>
        /// OP架反面，第一第二滴面的，检测OP架是否存在的框
        /// </summary>
        public double SumBrightnessLimitDrop12 { get; set; }
        /// <summary>
        /// OP架反面，第一第二滴面的，检测OP架是否存在的框
        /// </summary>
        public int SumBrightnessDrop12Roi_Left { get; set; }
        /// <summary>
        /// OP架反面，第一第二滴面的，检测OP架是否存在的框
        /// </summary>
        public int SumBrightnessDrop12Roi_Top { get; set; }
        /// <summary>
        /// OP架反面，第一第二滴面的，检测OP架是否存在的框
        /// </summary>
        public int SumBrightnessDrop12Roi_Width { get; set; }
        /// <summary>
        /// OP架反面，第一第二滴面的，检测OP架是否存在的框
        /// </summary>
        public int SumBrightnessDrop12Roi_Height { get; set; }

        //磁铁位置亮度和位置
        //第一个位置，左上-->左下位置
        /// <summary>
        /// OP架正面，第三滴面的，检测磁石是否存螺丝
        /// </summary>
        public double Magnet1BrightnessLimit { get; set; }
        /// <summary>
        /// OP架正面，第三滴面的，检测磁石是否存螺丝
        /// </summary>
        public int Magnet1BrightnessRoi_Left { get; set; }
        /// <summary>
        /// OP架正面，第三滴面的，检测磁石是否存螺丝
        /// </summary>
        public int Magnet1BrightnessRoi_Top { get; set; }
        /// <summary>
        /// OP架正面，第三滴面的，检测磁石是否存螺丝
        /// </summary>
        public int Magnet1BrightnessRoi_Width { get; set; }
        /// <summary>
        /// OP架正面，第三滴面的，检测磁石是否存螺丝
        /// </summary>
        public int Magnet1BrightnessRoi_Height { get; set; }
        //第二个位置
        public double Magnet2BrightnessLimit { get; set; }
        public int Magnet2BrightnessRoi_Left { get; set; }
        public int Magnet2BrightnessRoi_Top { get; set; }
        public int Magnet2BrightnessRoi_Width { get; set; }
        public int Magnet2BrightnessRoi_Height { get; set; }
        //第三个位置
        public double Magnet3BrightnessLimit { get; set; }
        public int Magnet3BrightnessRoi_Left { get; set; }
        public int Magnet3BrightnessRoi_Top { get; set; }
        public int Magnet3BrightnessRoi_Width { get; set; }
        public int Magnet3BrightnessRoi_Height { get; set; }
        //第四个位置
        public double Magnet4BrightnessLimit { get; set; }
        public int Magnet4BrightnessRoi_Left { get; set; }
        public int Magnet4BrightnessRoi_Top { get; set; }
        public int Magnet4BrightnessRoi_Width { get; set; }
        public int Magnet4BrightnessRoi_Height { get; set; }

        //第一滴

        /// <summary>
        /// 第一滴，红竖线的起始位置。经量使这个红竖线位于油脂的初始位置
        /// </summary>
        public int Drop1VerticalLine { get; set; }
        /// <summary>
        /// 第一滴，相对于定位点的roi位置
        /// </summary>
        public int Drop1Roi_T { get; set; }
        /// <summary>
        /// 第一滴，相对于定位点的roi位置
        /// </summary>
        public int Drop1Roi_L { get; set; }
        /// <summary>
        /// 第一滴，相对于定位点的roi位置
        /// </summary>
        public int Drop1Roi_W { get; set; }
        /// <summary>
        /// 第一滴，相对于定位点的roi位置
        /// </summary>
        public int Drop1Roi_H { get; set; }



        /// <summary>
        /// 限定第一滴图，油脂检测的范围
        /// </summary>
        public int Drop1JudgePosition_Top { get; set; }
        /// <summary>
        /// 限定第一滴图，油脂检测的范围
        /// </summary>
        public int Drop1JudgePosition_Left { get; set; }
        /// <summary>
        /// 限定第一滴图，油脂检测的范围
        /// </summary>
        public int Drop1JudgePosition_Width { get; set; }
        /// <summary>
        /// 限定第一滴图，油脂检测的范围
        /// </summary>
        public int Drop1JudgePosition_Height { get; set; }

       

        /// <summary>
        /// 第二滴，检测油脂的二值化阈值
        /// </summary>
        public int Drop1GreaseBlobThresh { get; set; }
        /// <summary>
        /// 当作油脂的最小blob的限制，当blob的对应值大于此值时，将视为油脂的blob
        /// </summary>
        public int Drop1GreaseBlobMinimum_W { get; set; }
        /// <summary>
        /// 油脂blob的下限，当油脂blob对应值小于此值时，将视为少了
        /// </summary>
        private int _Drop1GreaseBlobLower_W;
        public int Drop1GreaseBlobLower_W
        {
            get { return _Drop1GreaseBlobLower_W; }
            set { if (_Drop1GreaseBlobLower_W != value) { _Drop1GreaseBlobLower_W = value; RaisePropertyChanged("Drop1GreaseBlobLower_W"); } }
        }

        private int _Drop1GreaseBlobUpper_W;
        /// <summary>
        /// 油脂blob的上限，当油脂blob对应值大于此值时，将视为多了
        /// </summary>
        public int Drop1GreaseBlobUpper_W
        {
            get { return _Drop1GreaseBlobUpper_W; }
            set { if (_Drop1GreaseBlobUpper_W != value) { _Drop1GreaseBlobUpper_W = value; RaisePropertyChanged("Drop1GreaseBlobUpper_W"); } }
        }
        /// <summary>
        /// 当作油脂的最小blob的限制，当blob的对应值大于此值时，将视为油脂的blob
        /// </summary>
        public int Drop1GreaseBlobMinimum_H { get; set; }

        private int _Drop1GreaseBlobLower_H;
        /// <summary>
        /// 油脂blob的下限，当油脂blob对应值小于此值时，将视为少了
        /// </summary>
        public int Drop1GreaseBlobLower_H
        {
            get { return _Drop1GreaseBlobLower_H; }
            set { if (_Drop1GreaseBlobLower_H != value) { _Drop1GreaseBlobLower_H = value; RaisePropertyChanged("Drop1GreaseBlobLower_H"); } }
        }
        private int _Drop1GreaseBlobUpper_H;
        /// <summary>
        /// 油脂blob的上限，当油脂blob对应值大于此值时，将视为多了
        /// </summary>
        public int Drop1GreaseBlobUpper_H
        {
            get { return _Drop1GreaseBlobUpper_H; }
            set { if (_Drop1GreaseBlobUpper_H != value) { _Drop1GreaseBlobUpper_H = value; RaisePropertyChanged("Drop1GreaseBlobUpper_H"); } }
        }
        /// <summary>
        /// 当作油脂的最小blob的限制，当blob的对应值大于此值时，将视为油脂的blob
        /// </summary>
        public int Drop1GreaseBlobMinimum_A { get; set; }

        private int _Drop1GreaseBlobLower_A;
        /// <summary>
        /// 油脂blob的下限，当油脂blob对应值小于此值时，将视为少了
        /// </summary>
        public int Drop1GreaseBlobLower_A
        {
            get { return _Drop1GreaseBlobLower_A; }
            set { if (_Drop1GreaseBlobLower_A != value) { _Drop1GreaseBlobLower_A = value; RaisePropertyChanged("Drop1GreaseBlobLower_A"); } }
        }
        private int _Drop1GreaseBlobUpper_A;
        /// <summary>
        /// 油脂blob的上限，当油脂blob对应值大于此值时，将视为多了
        /// </summary>
        public int Drop1GreaseBlobUpper_A
        {
            get { return _Drop1GreaseBlobUpper_A; }
            set { if (_Drop1GreaseBlobUpper_A != value) { _Drop1GreaseBlobUpper_A = value; RaisePropertyChanged("Drop1GreaseBlobUpper_A"); } }
        }


        //第二滴
        /// <summary>
        /// 第二滴中，油脂初始的红竖线
        /// </summary>
        public int Drop2VerticalLine { get; set; }
    
       /// <summary>
       /// 用于检测边缘的阈值
       /// </summary>
        public int Drop2VerTh { get; set; }
        /// <summary>
        /// 截取棱上roi用
        /// </summary>
        public int Drop2MinorRoi_T { get; set; }
        /// <summary>
        /// 截取棱上roi用
        /// </summary>
        public int Drop2MinorRoi_W { get; set; }
        /// <summary>
        /// 检测边缘时，作为检测区域的宽
        /// </summary>
        public int Drpo2VerticalEdgeSub_w { get; set; }

        private int[] _Drop2MiniLimitWHA = new int[3];
        /// <summary>
        /// 对于棱上油脂的限制，小于这个值时，油脂少量
        /// </summary>
        public int[] Drop2MiniLimitWHA
        {
            get { return _Drop2MiniLimitWHA; }
            set { if (_Drop2MiniLimitWHA != value) { _Drop2MiniLimitWHA = value; } }
        }
        /// <summary>
        /// 第二滴图，相对于定位点的位置
        /// </summary>
        public int Drop2Roi_T { get; set; }
        public int Drop2Roi_L { get; set; }
        public int Drop2Roi_W { get; set; }
        public int Drop2Roi_H { get; set; }

        /// <summary>
        /// 第二滴图中，检测油脂范围的Roi
        /// </summary>
        public int Drop2JudgePosition_Top { get; set; }
        public int Drop2JudgePosition_Left { get; set; }
        public int Drop2JudgePosition_Width { get; set; }
        public int Drop2JudgePosition_Height { get; set; }
        /// <summary>
        /// 第二滴检测油脂用的二值化阈值
        /// </summary>
        public int Drop2GreaseBlobThresh { get; set; }
        /// <summary>
        /// 视为油脂的最小对应值
        /// </summary>
        public int Drop2GreaseBlobMinimum_W { get; set; }
        /// <summary>
        /// 第二滴油脂的下限
        /// </summary>
        private int _Drop2GreaseBlobLower_W;
        public int Drop2GreaseBlobLower_W
        {
            get { return _Drop2GreaseBlobLower_W; }
            set { if (_Drop2GreaseBlobLower_W != value) { _Drop2GreaseBlobLower_W = value; RaisePropertyChanged("Drop2GreaseBlobLower_W"); } }
        }
        private int _Drop2GreaseBlobUpper_W;
        /// <summary>
        /// 第二滴油脂的上限
        /// </summary>
        public int Drop2GreaseBlobUpper_W
        {
            get { return _Drop2GreaseBlobUpper_W; }
            set { if (_Drop2GreaseBlobUpper_W != value) { _Drop2GreaseBlobUpper_W = value; RaisePropertyChanged("Drop2GreaseBlobUpper_W"); } }
        }
        public int Drop2GreaseBlobMinimum_H { get; set; }
        private int _PositiveGrease2HeightMin;
        public int Drop2GreaseBlobLower_H
        {
            get { return _PositiveGrease2HeightMin; }
            set { if (_PositiveGrease2HeightMin != value) { _PositiveGrease2HeightMin = value; RaisePropertyChanged("Drop2GreaseBlobLower_H"); } }
        }
        private int _Drop2GreaseBlobUpper_H;
        public int Drop2GreaseBlobUpper_H
        {
            get { return _Drop2GreaseBlobUpper_H; }
            set { if (_Drop2GreaseBlobUpper_H != value) { _Drop2GreaseBlobUpper_H = value; RaisePropertyChanged("Drop2GreaseBlobUpper_H"); } }
        }
        public int Drop2GreaseBlobMinimum_A { get; set; }
        private int _Drop2GreaseBlobLower_A;
        public int Drop2GreaseBlobLower_A
        {
            get { return _Drop2GreaseBlobLower_A; }
            set { if (_Drop2GreaseBlobLower_A != value) { _Drop2GreaseBlobLower_A = value; RaisePropertyChanged("Drop2GreaseBlobLower_A"); } }
        }
        private int _Drop2GreaseBlobUpper_A;
        public int Drop2GreaseBlobUpper_A
        {
            get { return _Drop2GreaseBlobUpper_A; }
            set { if (_Drop2GreaseBlobUpper_A != value) { _Drop2GreaseBlobUpper_A = value; RaisePropertyChanged("Drop2GreaseBlobUpper_A"); } }
        }

        //第三滴

        public int Drop3VerticalLine { get; set; }
  
        public int Drop3VerTh { get; set; }
        public int Drop3MinorRoi_T { get; set; }
        public int Drop3MinorRoi_W { get; set; }
        public int Drpo3VerticalEdgeSub_w { get; set; }


        private int[] _Drop3MiniLimitWHA = new int[3];
        public int[] Drop3MiniLimitWHA
        {
            get { return _Drop3MiniLimitWHA; }
            set { if (_Drop3MiniLimitWHA != value) { _Drop3MiniLimitWHA = value; } }
        }
        public int Drop3Roi_T { get; set; }
        public int Drop3Roi_L { get; set; }
        public int Drop3Roi_W { get; set; }
        public int Drop3Roi_H { get; set; }

   

        public int Drop3JudgePosition_Top { get; set; }
        public int Drop3JudgePosition_Left { get; set; }
        public int Drop3JudgePosition_Width { get; set; }
        public int Drop3JudgePosition_Height { get; set; }

        public int Drop3GreaseBlobThresh { get; set; }
        public int Drop3GreaseBlobMinimum_W { get; set; }
        public int Drop3GreaseBlobLower_W { get; set; }
        public int Drop3GreaseBlobUpper_W { get; set; }
        public int Drop3GreaseBlobMinimum_H { get; set; }
        public int Drop3GreaseBlobLower_H { get; set; }
        public int Drop3GreaseBlobUpper_H { get; set; }
        public int Drop3GreaseBlobMinimum_A { get; set; }
        public int Drop3GreaseBlobLower_A { get; set; }
        public int Drop3GreaseBlobUpper_A { get; set; }

        //定位用点
        /// <summary>
        /// 第三滴面，定位点的roi位置
        /// </summary>
        public int  DatumPointDrop3_Left { get; set; }
        public int DatumPointDrop3_Top { get; set; }
        public int DatumPointDrop3_Width { get; set; }
        public int DatumPointDrop3_Height { get; set; }
        /// <summary>
        /// 第三滴用定位点的二值化阈值
        /// </summary>
        public int DatumPointBinValue { get; set; }
  
        /// <summary>
        /// 第三滴面，旋转图片的角度
        /// </summary>
        public float RotateAngle { get; set; }

        //定位Drop12用点
        public int DatumPointDrop12_Left { get; set; }
        public int DatumPointDrop12_Top { get; set; }
        public int DatumPointDrop12_Width { get; set; }
        public int DatumPointDrop12_Height { get; set; }
        /// <summary>
        /// 第一二面，定位物体的二值化阈值
        /// </summary>
        public int DatumPointDrop12BinValue { get; set; }
    
        /// <summary>
        /// 第一二滴面，旋转图片的角度
        /// </summary>
        public float RotateAngleDrop12 { get; set; }





    }
}
