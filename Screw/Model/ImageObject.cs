using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Screw.BaseClasses;
using Screw.View;
using Newtonsoft.Json;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.WpfExtensions;

namespace Screw.Model
{
    public class ImageObject : NotifyPropertyChangedBase
    {
        /// <summary>
        /// Object search mode when searching for world coordinate
        /// </summary>
        public enum SearchMode
        {
            ByMaxBlob,
            ByEdge
        }

        /// <summary>
        /// Corner mode when searching world coordinate
        /// </summary>
        public enum CornerType
        {
            None,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Center
        }

        public ImageObject()
        {
            EdgeMode = 0;   // default rising mode
        }

        #region ROI Selection Para

        private bool IsMouseDown;
        private bool SelRegionDragging;
        private int selRegionDragX0, selRegionDragY0;
        private double xRatio, yRatio;

        // Canvas display and image has a scale differance
        // That's why  there two position and size systems

        /////////////////// Canvas Position and Size ///////////////////

        /// <summary>
        /// Roi region adjust panel on/off
        /// </summary>
        private bool _RoiAdjOn;
        [JsonIgnore]
        public bool RoiAdjOn
        {
            get { return _RoiAdjOn; }
            set { if (_RoiAdjOn != value) { _RoiAdjOn = value; RaisePropertyChanged("RoiAdjOn"); } }
        }

        private int _X0;
        [JsonIgnore]
        public int X0
        {
            get { return _X0; }
            set { if (_X0 != value) { _X0 = value; RaisePropertyChanged("X0"); } }
        }

        private int _Y0;
        [JsonIgnore]
        public int Y0
        {
            get { return _Y0; }
            set { if (_Y0 != value) { _Y0 = value; RaisePropertyChanged("Y0"); } }
        }

        private int _X1;
        [JsonIgnore]
        public int X1
        {
            get { return _X1; }
            set { if (_X1 != value) { _X1 = value; RaisePropertyChanged("X1"); } }
        }

        private int _Y1;
        [JsonIgnore]
        public int Y1
        {
            get { return _Y1; }
            set { if (_Y1 != value) { _Y1 = value; RaisePropertyChanged("Y1"); } }
        }

        private int _SelWidth;
        [JsonIgnore]
        public int SelWidth
        {
            get { return _SelWidth; }
            set { if (_SelWidth != value) { _SelWidth = value; RaisePropertyChanged("SelWidth"); } }
        }

        private int _SelHeight;
        [JsonIgnore]
        public int SelHeight
        {
            get { return _SelHeight; }
            set { if (_SelHeight != value) { _SelHeight = value; RaisePropertyChanged("SelHeight"); } }
        }

        /////////////////// ROI Position and Size ///////////////////

        private int _RoiX0;
        [JsonIgnore]
        public int RoiX0
        {
            get { return _RoiX0; }
            set { if (_RoiX0 != value) { _RoiX0 = value; RaisePropertyChanged("RoiX0"); } }
        }

        private int _RoiY0;
        [JsonIgnore]
        public int RoiY0
        {
            get { return _RoiY0; }
            set { if (_RoiY0 != value) { _RoiY0 = value; RaisePropertyChanged("RoiY0"); } }
        }

        private int _RoiX1;
        [JsonIgnore]
        public int RoiX1
        {
            get { return _RoiX1; }
            set { if (_RoiX1 != value) { _RoiX1 = value; RaisePropertyChanged("RoiX1"); } }
        }

        private int _RoiY1;
        [JsonIgnore]
        public int RoiY1
        {
            get { return _RoiY1; }
            set { if (_RoiY1 != value) { _RoiY1 = value; RaisePropertyChanged("RoiY1"); } }
        }

        private int _RoiWidth;
        [JsonIgnore]
        public int RoiWidth
        {
            get { return _RoiWidth; }
            set { if (_RoiWidth != value) { _RoiWidth = value; RaisePropertyChanged("RoiWidth"); } }
        }

        private int _RoiHeight;
        [JsonIgnore]
        public int RoiHeight
        {
            get { return _RoiHeight; }
            set { if (_RoiHeight != value) { _RoiHeight = value; RaisePropertyChanged("RoiHeight"); } }
        }

        #endregion

        #region Region Selection RelayCommand

        private RelayCommandAttached _mouseUpCommand;
        [JsonIgnore]
        public RelayCommandAttached MouseUpCommand
        {
            get
            {
                if (_mouseUpCommand == null) _mouseUpCommand = new RelayCommandAttached(param => MouseUp((MouseEventArgs)param));
                return _mouseUpCommand;
            }
            set { _mouseUpCommand = value; }
        }

        private void MouseUp(MouseEventArgs e)
        {
            IsMouseDown = false;

            //MessageBox.Show(e.GetPosition((IInputElement)e.Source).ToString());
            //X1 = (int)e.GetPosition((IInputElement)e.Source).X;
            //Y1 = (int)e.GetPosition((IInputElement)e.Source).Y;
        }

        private RelayCommandAttached _mouseDownCommand;
        [JsonIgnore]
        public RelayCommandAttached MouseDownCommand
        {
            get
            {
                if (_mouseDownCommand == null) _mouseDownCommand = new RelayCommandAttached(param => MouseDown((MouseEventArgs)param));
                return _mouseDownCommand;
            }
            set { _mouseDownCommand = value; }
        }

        private void MouseDown(MouseEventArgs e)//画图用
        {
            if (RoiAdjOn)
            {
                if (DisplayImage == null) return;
                //MessageBox.Show(e.GetPosition((IInputElement)e.Source).ToString());
                X0 = (int)e.GetPosition((IInputElement)e.Source).X;
                Y0 = (int)e.GetPosition((IInputElement)e.Source).Y;

                Image imageControl = (Image)(IInputElement)e.Source;
                xRatio = DisplayImage.PixelWidth / imageControl.ActualWidth;
                yRatio = DisplayImage.PixelHeight / imageControl.ActualHeight;
                int imageX = (int)(X0 * xRatio);//图对应的xy坐标
                int imageY = (int)(Y0 * yRatio);

                if (MaskDrawMode)//画线模式
                {
                    if (e.RightButton == MouseButtonState.Pressed)
                    {
                        // clear points when right mouse button clicked
                        contourPoints.Clear();
                        maskContourClosed = false;
                    }
                    else
                    {
                        if (maskContourClosed)
                        {
                            // clear points when right mouse button clicked
                            contourPoints.Clear();
                            maskContourClosed = false;
                        }
                        else
                        {
                            if (contourPoints.Count > 1)
                            {
                                // take as close contour click
                                if (Math.Abs(imageX - contourPoints[0].X) <= 5 && Math.Abs(imageY - contourPoints[0].Y) <= 5)
                                {
                                    maskContourClosed = true;
                                    DrawContourMask(contourPoints, maskContourClosed, true);
                                    return;
                                }
                            }
                        }

                        // append point
                        contourPoints.Add(new OpenCvSharp.Point(imageX, imageY));
                    }
                    // display
                    DrawContourMask(contourPoints, maskContourClosed, true);
                }
                else
                {
                    IsMouseDown = true;
                    RoiX0 = imageX;
                    RoiY0 = imageY;
                    SelHeight = 0;
                    SelWidth = 0;
                }
            }
        }

        /// <summary>
        /// draw area mask contour  画出线
        /// </summary>
        public void DrawContourMask(List<OpenCvSharp.Point> srcContours, bool closed, bool drawCircles = false, int offsetX = 0, int offsetY = 0)
        {
            if (SourceImage == null) return;

            List<OpenCvSharp.Point> contours = new List<OpenCvSharp.Point>();
            foreach (OpenCvSharp.Point p in srcContours)
            {
                contours.Add(new OpenCvSharp.Point(p.X - offsetX, p.Y - offsetY));
            }

            using (Mat img = SourceImage.ToMat())
            using (Mat src = img.Channels() == 1 ? img.CvtColor(ColorConversionCodes.GRAY2BGR) : img.Clone())
            {
                if (contours.Count > 0)
                {
                    Scalar lineClr = Scalar.GreenYellow;
                    if (closed)
                    {
                        lineClr = Scalar.GreenYellow;
                    }

                    for (int i = 0; i < contours.Count; i++)
                    {
                        if (drawCircles)
                        {
                            Scalar circleClr = Scalar.DeepPink;

                            if (i == 0)
                            {
                                circleClr = Scalar.DeepPink;
                            }

                            //if (contours.Count == 1 || (contours.Count > 1 && i != contours.Count - 1))
                            //{
                            //    src.Circle(contours[i], 6, circleClr, 1);
                            //}
                            src.Circle(contours[i], 5, circleClr, 1);
                        }

                        if (i > 0)
                        {
                            src.Line(contours[i - 1], contours[i], lineClr, 1);
                        }

                        if (closed && i == contours.Count - 1)
                        {
                            src.Line(contours[i], contours[0], lineClr, 1);
                        }
                    }
                }

                // display
                DisplayImage = src.ToBitmapSource();
            }
        }

        private RelayCommandAttached _MouseMoveCommand;
        [JsonIgnore]
        public RelayCommandAttached MouseMoveCommand
        {
            get
            {
                if (_MouseMoveCommand == null) _MouseMoveCommand = new RelayCommandAttached(param => MouseMove((MouseEventArgs)param));
                return _MouseMoveCommand;
            }
            set { _MouseMoveCommand = value; }
        }

        private void MouseMove(MouseEventArgs e)//画框
        {
            if (DisplayImage == null) return;

            if (!SelRegionDragging & IsMouseDown)
            {
                //MessageBox.Show(e.GetPosition((IInputElement)e.Source).ToString());
                X1 = (int)e.GetPosition((IInputElement)e.Source).X;
                Y1 = (int)e.GetPosition((IInputElement)e.Source).Y;

                SelHeight = Y1 - Y0;
                SelWidth = X1 - X0;

                Image imageControl = (Image)(IInputElement)e.Source;
                xRatio = DisplayImage.PixelWidth / imageControl.ActualWidth;
                yRatio = DisplayImage.PixelHeight / imageControl.ActualHeight;
                RoiX1 = (int)(X1 * xRatio);
                RoiY1 = (int)(Y1 * yRatio);//////////////////////////////////////

                // ROI Image size
                RoiWidth = RoiX1 - RoiX0;
                RoiHeight = RoiY1 - RoiY0;
            }
        }

        private RelayCommandAttached _SelRegionMouseUpCommand;
        [JsonIgnore]
        public RelayCommandAttached SelRegionMouseUpCommand
        {
            get
            {
                if (_SelRegionMouseUpCommand == null) _SelRegionMouseUpCommand = new RelayCommandAttached(param => SelRegionMouseUp((MouseEventArgs)param));
                return _SelRegionMouseUpCommand;
            }
            set { _SelRegionMouseUpCommand = value; }
        }

        private void SelRegionMouseUp(MouseEventArgs e)
        {
            SelRegionDragging = false;
        }

        private RelayCommandAttached _SelRegionMouseDownCommand;
        [JsonIgnore]
        public RelayCommandAttached SelRegionMouseDownCommand
        {
            get
            {
                if (_SelRegionMouseDownCommand == null) _SelRegionMouseDownCommand = new RelayCommandAttached(param => SelRegionMouseDown((MouseEventArgs)param));
                return _SelRegionMouseDownCommand;
            }
            set { _SelRegionMouseDownCommand = value; }
        }

        private void SelRegionMouseDown(MouseEventArgs e)
        {
            selRegionDragX0 = (int)e.GetPosition((IInputElement)e.Source).X;//获取位置坐标
            selRegionDragY0 = (int)e.GetPosition((IInputElement)e.Source).Y;

            SelRegionDragging = true;//画图允许
        }

        private RelayCommandAttached _SelRegionMouseMoveCommand;
        [JsonIgnore]
        public RelayCommandAttached SelRegionMouseMoveCommand
        {
            get
            {
                if (_SelRegionMouseMoveCommand == null) _SelRegionMouseMoveCommand = new RelayCommandAttached(param => SelRegionMouseMove((MouseEventArgs)param));
                return _SelRegionMouseMoveCommand;
            }
            set { _SelRegionMouseMoveCommand = value; }
        }

        private void SelRegionMouseMove(MouseEventArgs e)
        {
            if (SelRegionDragging)
            {
                //MessageBox.Show(e.GetPosition((IInputElement)e.Source).ToString());
                int selRegionDragX1 = (int)e.GetPosition((IInputElement)e.Source).X;
                int selRegionDragY1 = (int)e.GetPosition((IInputElement)e.Source).Y;

                int dragX = selRegionDragX1 - selRegionDragX0;
                int dragY = selRegionDragY1 - selRegionDragY0;
                X0 += dragX;
                Y0 += dragY;
                // convert
                SelectArea2Roi();
            }
        }

        /// <summary>
        /// SelRegionMoveLeft command
        /// </summary>
        private ICommand _SelRegionMoveLeftCommand;
        [JsonIgnore]
        public ICommand SelRegionMoveLeftCommand
        {
            get
            {
                if (_SelRegionMoveLeftCommand == null)
                {
                    _SelRegionMoveLeftCommand = new RelayCommand(
                        param => this.SelRegionMoveLeftExecute(),
                        param => this.CanSelRegionMoveLeft()
                    );
                }
                return _SelRegionMoveLeftCommand;
            }
        }
        private bool CanSelRegionMoveLeft()
        {
            return true;
        }
        private void SelRegionMoveLeftExecute()
        {
            if (X0 > 0)
            {
                X0 -= 1;
                SelectArea2Roi();
            }
        }

        /// <summary>
        /// SelRegionMoveUp command
        /// </summary>
        private ICommand _SelRegionMoveUpCommand;
        [JsonIgnore]
        public ICommand SelRegionMoveUpCommand
        {
            get
            {
                if (_SelRegionMoveUpCommand == null)
                {
                    _SelRegionMoveUpCommand = new RelayCommand(
                        param => this.SelRegionMoveUpExecute(),
                        param => this.CanSelRegionMoveUp()
                    );
                }
                return _SelRegionMoveUpCommand;
            }
        }
        private bool CanSelRegionMoveUp()
        {
            return true;
        }
        private void SelRegionMoveUpExecute()
        {
            if (Y0 > 0)
            {
                Y0 -= 1;
                SelectArea2Roi();
            }
        }

        /// <summary>
        /// SelRegionMoveRight command
        /// </summary>
        private ICommand _SelRegionMoveRightCommand;
        [JsonIgnore]
        public ICommand SelRegionMoveRightCommand
        {
            get
            {
                if (_SelRegionMoveRightCommand == null)
                {
                    _SelRegionMoveRightCommand = new RelayCommand(
                        param => this.SelRegionMoveRightExecute(),
                        param => this.CanSelRegionMoveRight()
                    );
                }
                return _SelRegionMoveRightCommand;
            }
        }
        private bool CanSelRegionMoveRight()
        {
            return true;
        }
        private void SelRegionMoveRightExecute()
        {
            X0 += 1;
            SelectArea2Roi();
        }

        /// <summary>
        /// SelRegionMoveDown command
        /// </summary>
        private ICommand _SelRegionMoveDownCommand;
        [JsonIgnore]
        public ICommand SelRegionMoveDownCommand
        {
            get
            {
                if (_SelRegionMoveDownCommand == null)
                {
                    _SelRegionMoveDownCommand = new RelayCommand(
                        param => this.SelRegionMoveDownExecute(),
                        param => this.CanSelRegionMoveDown()
                    );
                }
                return _SelRegionMoveDownCommand;
            }
        }
        private bool CanSelRegionMoveDown()
        {
            return true;
        }
        private void SelRegionMoveDownExecute()
        {
            Y0 += 1;
            SelectArea2Roi();
        }


        /// <summary>
        /// SelRegionLeftExtend command
        /// </summary>
        private ICommand _SelRegionLeftExtendCommand;
        [JsonIgnore]
        public ICommand SelRegionLeftExtendCommand
        {
            get
            {
                if (_SelRegionLeftExtendCommand == null)
                {
                    _SelRegionLeftExtendCommand = new RelayCommand(
                        param => this.SelRegionLeftExtendExecute(),
                        param => this.CanSelRegionLeftExtend()
                    );
                }
                return _SelRegionLeftExtendCommand;
            }
        }
        private bool CanSelRegionLeftExtend()
        {
            return true;
        }
        private void SelRegionLeftExtendExecute()
        {
            SelWidth -= 1;
            SelectArea2Roi();
        }

        /// <summary>
        /// SelRegionUpExtend command
        /// </summary>
        private ICommand _SelRegionUpExtendCommand;
        [JsonIgnore]
        public ICommand SelRegionUpExtendCommand
        {
            get
            {
                if (_SelRegionUpExtendCommand == null)
                {
                    _SelRegionUpExtendCommand = new RelayCommand(
                        param => this.SelRegionUpExtendExecute(),
                        param => this.CanSelRegionUpExtend()
                    );
                }
                return _SelRegionUpExtendCommand;
            }
        }
        private bool CanSelRegionUpExtend()
        {
            return true;
        }
        private void SelRegionUpExtendExecute()
        {
            SelHeight -= 1;
            SelectArea2Roi();
        }

        /// <summary>
        /// SelRegionRightExtend command
        /// </summary>
        private ICommand _SelRegionRightExtendCommand;
        [JsonIgnore]
        public ICommand SelRegionRightExtendCommand
        {
            get
            {
                if (_SelRegionRightExtendCommand == null)
                {
                    _SelRegionRightExtendCommand = new RelayCommand(
                        param => this.SelRegionRightExtendExecute(),
                        param => this.CanSelRegionRightExtend()
                    );
                }
                return _SelRegionRightExtendCommand;
            }
        }
        private bool CanSelRegionRightExtend()
        {
            return true;
        }
        private void SelRegionRightExtendExecute()
        {
            SelWidth += 1;
            SelectArea2Roi();
        }

        /// <summary>
        /// SelRegionBottomExtend command
        /// </summary>
        private ICommand _SelRegionBottomExtendCommand;
        [JsonIgnore]
        public ICommand SelRegionBottomExtendCommand
        {
            get
            {
                if (_SelRegionBottomExtendCommand == null)
                {
                    _SelRegionBottomExtendCommand = new RelayCommand(
                        param => this.SelRegionBottomExtendExecute(),
                        param => this.CanSelRegionBottomExtend()
                    );
                }
                return _SelRegionBottomExtendCommand;
            }
        }
        private bool CanSelRegionBottomExtend()
        {
            return true;
        }
        private void SelRegionBottomExtendExecute()
        {
            SelHeight += 1;
            SelectArea2Roi();
        }

        /// <summary>
        /// Converting selected position and area on canvas to Image scale ROI
        /// </summary>
        private void SelectArea2Roi()
        {
            X1 = X0 + SelWidth;
            Y1 = Y0 + SelHeight;

            RoiX0 = (int)(X0 * xRatio);
            RoiY0 = (int)(Y0 * yRatio);
            RoiX1 = (int)(X1 * xRatio);
            RoiY1 = (int)(Y1 * yRatio);

            // ROI Image size
            RoiWidth = RoiX1 - RoiX0;
            RoiHeight = RoiY1 - RoiY0;
        }

        #endregion

        #region AreaMaskPara

        public List<OpenCvSharp.Point> contourPoints = new List<OpenCvSharp.Point>();

        #endregion

        #region Properties

        private string _Name;
        public string Name
        {
            get { return _Name; }
            set { if (_Name != value) { _Name = value; RaisePropertyChanged("Name"); } }
        }

        /// <summary>
        /// Source camera name
        /// </summary>
        private string _SourceCamName;
        public string SourceCamName
        {
            get { return _SourceCamName; }
            set { if (_SourceCamName != value) { _SourceCamName = value; RaisePropertyChanged("SourceCamName"); } }
        }

        /// <summary>
        /// Index of source camera in cameralist
        /// *** this value is going to be updated refering to "SourceCamName" after cameras a loaded ***
        /// </summary>
        private int _SourceCamIndex;
        [JsonIgnore]
        public int SourceCamIndex
        {
            get { return _SourceCamIndex; }
            set { if (_SourceCamIndex != value) { _SourceCamIndex = value; RaisePropertyChanged("SourceCamIndex"); } }
        }

        /// <summary>
        /// source image
        /// </summary>
        private BitmapSource _SourceImage;
        [JsonIgnore]
        public BitmapSource SourceImage
        {
            get { return _SourceImage; }
            set
            {
                if (value != _SourceImage)
                {
                    _SourceImage = value;
                    _SourceImage.Freeze();
                    RaisePropertyChanged("SourceImage");
                }
            }
        }

        /// <summary>
        /// Display image
        /// </summary>
        private BitmapSource _DisplayImage;
        [JsonIgnore]
        public BitmapSource DisplayImage
        {
            get { return _DisplayImage; }
            set
            {
                if (value != _DisplayImage)
                {
                    _DisplayImage = value;
                    _DisplayImage.Freeze();
                    RaisePropertyChanged("DisplayImage");
                }
            }
        }

        /// <summary>
        /// image number counting
        /// </summary>
        private int _ImageCount;
        [JsonIgnore]
        public int ImageCount
        {
            get { return _ImageCount; }
            set { if (_ImageCount != value) { _ImageCount = value; RaisePropertyChanged("ImageCount"); } }
        }

        /// <summary>
        /// Auto generated image saving path
        /// </summary>
        [JsonIgnore]
        public string ImageSavingPath
        {
            get
            {
                return string.Format(@"images\{0}\{1}", Name, DateTime.Now.ToString("yyMMdd"));
            }
        }

        [JsonIgnore]
        public int RefX0;
        [JsonIgnore]
        public int RefY0;

        /// <summary>
        /// judge area mask drawing mode
        /// </summary>
        private bool _MaskDrawMode;
        public bool MaskDrawMode
        {
            get { return _MaskDrawMode; }
            set { if (_MaskDrawMode != value) { _MaskDrawMode = value; RaisePropertyChanged("MaskDrawMode"); } }
        }

        private bool maskContourClosed = false;

        #endregion

        #region Property For Global Coordinate

        /// <summary>
        /// Mark Object Searching Mode
        /// </summary>
        private SearchMode _MarkSearchMode;
        public SearchMode MarkSearchMode
        {
            get { return _MarkSearchMode; }
            set { if (_MarkSearchMode != value) { _MarkSearchMode = value; RaisePropertyChanged("MarkSearchMode"); } }
        }

        /// <summary>
        /// Edge searching mode: rising or falling edge
        /// </summary>
        private int _EdgeMode;
        public int EdgeMode
        {
            get { return _EdgeMode; }
            set { if (_EdgeMode != value) { _EdgeMode = value; RaisePropertyChanged("EdgeMode"); } }
        }

        /// <summary>
        /// Mark Corner Type
        /// </summary>
        private CornerType _MarkCornerType;
        public CornerType MarkCornerType
        {
            get { return _MarkCornerType; }
            set { if (_MarkCornerType != value) { _MarkCornerType = value; RaisePropertyChanged("MarkCornerType"); } }
        }

        /// <summary>
        /// Mark Left
        /// </summary>
        private int _MarkLeft;
        public int MarkLeft
        {
            get { return _MarkLeft; }
            set { if (_MarkLeft != value) { _MarkLeft = value; RaisePropertyChanged("MarkLeft"); } }
        }

        /// <summary>
        /// Mark Top
        /// </summary>
        private int _MarkTop;
        public int MarkTop
        {
            get { return _MarkTop; }
            set { if (_MarkTop != value) { _MarkTop = value; RaisePropertyChanged("MarkTop"); } }
        }

        /// <summary>
        /// Mark Width
        /// </summary>
        private int _MarkWidth;
        public int MarkWidth
        {
            get { return _MarkWidth; }
            set { if (_MarkWidth != value) { _MarkWidth = value; RaisePropertyChanged("MarkWidth"); } }
        }

        /// <summary>
        /// Mark Height
        /// </summary>
        private int _MarkHeight;
        public int MarkHeight
        {
            get { return _MarkHeight; }
            set { if (_MarkHeight != value) { _MarkHeight = value; RaisePropertyChanged("MarkHeight"); } }
        }

        #endregion

        #region Methods

        /// <summary>
        /// syschronize display image to source image
        /// </summary>
        public void SyncDisplay()
        {
            if (SourceImage != null)
                DisplayImage = SourceImage.Clone();
        }

        #endregion

        #region RelayCommands

        /// <summary>
        /// SetMark command
        /// </summary>
        private ICommand _SetMarkCommand;
        [JsonIgnore]
        public ICommand SetMarkCommand
        {
            get
            {
                if (_SetMarkCommand == null)
                {
                    _SetMarkCommand = new RelayCommand(
                        param => this.SetMarkExecute(),
                        param => this.CanSetMark()
                    );
                }
                return _SetMarkCommand;
            }
        }
        private bool CanSetMark()
        {
            return true;
        }
        private void SetMarkExecute()
        {
            MarkLeft = RoiX0;
            MarkTop = RoiY0;
            MarkWidth = RoiWidth;
            MarkHeight = RoiHeight;
            MessageBox.Show("已设置,记得再按下面设置键,设置想要的位置");
        }

        /// <summary>
        /// OpenEditorWnd command
        /// </summary>
        private ICommand _OpenEditorWndCommand;
        [JsonIgnore]
        public ICommand OpenEditorWndCommand
        {
            get
            {
                if (_OpenEditorWndCommand == null)
                {
                    _OpenEditorWndCommand = new RelayCommand(
                        param => this.OpenEditorWndExecute(),
                        param => this.CanOpenEditorWnd()
                    );
                }
                return _OpenEditorWndCommand;
            }
        }
        private bool CanOpenEditorWnd()
        {
            return true;
        }
        private void OpenEditorWndExecute()
        {
            // start chart window
            wndImageObjEditor imgObjEditor = wndImageObjEditor.GetInstance();
            imgObjEditor.DataContext = this;
            imgObjEditor.Show();
        }

        #endregion
    }
}
