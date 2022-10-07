using Screw.BaseClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Screw.Model
{
    public class ComposedMessage : NotifyPropertyChangedBase
    {
        /// <summary>
        /// command
        /// </summary>
        private string _cmd;
        public string cmd
        {
            get { return _cmd; }
            set { if (_cmd != value) { _cmd = value; RaisePropertyChanged("cmd"); } }
        }

        /// <summary>
        /// parameter
        /// </summary>
        private string _para;
        public string para
        {
            get { return _para; }
            set { if (_para != value) { _para = value; RaisePropertyChanged("para"); } }
        }

        /// <summary>
        /// contains image or not
        /// </summary>
        private bool _contains_image;
        public bool contains_image
        {
            get { return _contains_image; }
            set { if (_contains_image != value) { _contains_image = value; RaisePropertyChanged("contains_image"); } }
        }

        /// <summary>
        /// serialzied image data
        /// </summary>
        private string _base64_img;
        public string base64_img
        {
            get { return _base64_img; }
            set { if (_base64_img != value) { _base64_img = value; RaisePropertyChanged("base64_img"); } }
        }

        /// <summary>
        /// clearing message for outside calling
        /// </summary>
        public void Clear()
        {
            cmd = "";
            para = "";
            base64_img = "";
            contains_image = false;
        }
    }
}
