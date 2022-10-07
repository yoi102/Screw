using lobe;
using Newtonsoft.Json;
using NLog;
using OpenCvSharp;
using Screw.BaseClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Screw.Model
{
    public class LobePredictor : NotifyPropertyChangedBase
    {
        Logger logger = LogManager.GetCurrentClassLogger();
        private ImageClassifier classifier;

        public LobePredictor()
        {

        }

        #region Methods

        /// <summary>
        /// Load Lobe model from signature file
        /// </summary>
        /// <returns></returns>
        public bool LoadModel()
        {
            if (!File.Exists(SignatureFilePath))
            {
                IsReady = false;
                return false;
            }

            try
            {
                ImageClassifier.Register("onnx", () => new OnnxImageClassifier());
                classifier = ImageClassifier.CreateFromSignatureFile(new FileInfo(SignatureFilePath));
                IsReady = true;
                return true;
            }
            catch (Exception ex)
            {
                IsReady = false;
                logger.Error("LoadModel|{0}", ex.Message);//跳这个了报错。
                return false;
            }
        }

        /// <summary>
        /// predict image
        /// </summary>
        /// <param name="img"></param>
        /// <param name="label"></param>
        /// <param name="confidence"></param>
        /// <returns></returns>
        public bool Predict(Mat img, ref string label, ref double confidence)//输入图片返回标签,调用这个就可以？confidence置信度？
        {
            if (!IsReady)
            {
                logger.Error("Predict|{0} not ready!", Name);
                return false;
            }
            try
            {
                var ret = lobe.OpenCvSharp.ImageClassifierExtensions.Classify(classifier, img);
                label = ret.Prediction.Label;
                confidence = ret.Prediction.Confidence;
                logger.Debug("Lobe Predictor {0} - Label:{1} Confidence:{2}", Name, label, confidence);
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("Predict|{0}", ex.Message);
                return false;
            }
        }

        #endregion

        #region Porperty

        private string _Name;
        public string Name
        {
            get { return _Name; }
            set { if (_Name != value) { _Name = value; RaisePropertyChanged("Name"); } }
        }

        private string _SignatureFilePath;//先给个文件.json的
        public string SignatureFilePath
        {
            get { return _SignatureFilePath; }
            set { if (_SignatureFilePath != value) { _SignatureFilePath = value; RaisePropertyChanged("SignatureFilePath"); } }
        }

        private bool _IsBusy;
        [JsonIgnore]
        public bool IsBusy
        {
            get { return _IsBusy; }
            set { if (_IsBusy != value) { _IsBusy = value; RaisePropertyChanged("IsBusy"); } }
        }

        private bool _ErrorFlag;
        [JsonIgnore]
        public bool ErrorFlag
        {
            get { return _ErrorFlag; }
            set { if (_ErrorFlag != value) { _ErrorFlag = value; RaisePropertyChanged("ErrorFlag"); } }
        }

        private bool _IsReady;
        [JsonIgnore]
        public bool IsReady
        {
            get { return _IsReady; }
            set { if (_IsReady != value) { _IsReady = value; RaisePropertyChanged("IsReady"); } }
        }

        /// <summary>
        /// request response time
        /// </summary>
        private int _ResponseTime;
        [JsonIgnore]
        public int ResponseTime
        {
            get { return _ResponseTime; }
            set { if (_ResponseTime != value) { _ResponseTime = value; RaisePropertyChanged("ResponseTime"); } }
        }

        #endregion

    }
}
