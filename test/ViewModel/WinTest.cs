using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using test.BaseClasses;
using test.View;

namespace test.ViewModel
{
    class WinTest : NotifyPropertyChangedBase
    {
        private WindowTTTT windowTTTT;

        public WinTest(WindowTTTT windowTTTT)
        {
            this.windowTTTT = windowTTTT;
        }



        private ObservableCollection<CommonParameters> _TCom_Para3 = new ObservableCollection<CommonParameters>();
        public ObservableCollection<CommonParameters> TCom_Para3
        {
            get { return _TCom_Para3; }
            set { if (_TCom_Para3 != value) { _TCom_Para3 = value; RaisePropertyChanged("TCom_Para3"); } }
        }

        private CommonParameters _TCom_Para2 = new CommonParameters();

        public CommonParameters TCom_Para2
        {
            get
            {
                if (TCom_Para3 != null || TCom_Para3.Count != 0)
                {
                    for (int i = 0; i < TCom_Para3.Count; i++)
                    {
                        if (TCom_Para3[i].SourceCamIndex == 2)
                        {
                            _TCom_Para2 = TCom_Para3[i];
                        }
                    }
                }
                return _TCom_Para2;
            }
            set { if (_TCom_Para2 != value) { _TCom_Para2 = value; RaisePropertyChanged("TCom_Para2"); } }
        }








    }
}
