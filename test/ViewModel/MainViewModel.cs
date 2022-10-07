using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using test.BaseClasses;
using test.View;

namespace test.ViewModel
{
    class MainViewModel : NotifyPropertyChangedBase
    {
        private MainWindow mainWindow;

        public MainViewModel(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;

            LoadComParaJsonData();
            //LoadComPara2JsonData();


            Com_Para3[(int)JudgeType.NG2].Name = "sadsad";
            //TTT.Show();


        }
        //WindowTTTT TTT = new WindowTTTT();













        private ObservableCollection<CommonParameters> _Com_Para3 = new ObservableCollection<CommonParameters>();
        public ObservableCollection<CommonParameters> Com_Para3
        {
            get { return _Com_Para3; }
            set { if (_Com_Para3 != value) { _Com_Para3 = value; RaisePropertyChanged("Com_Para3"); } }
        }

        /// <summary>
        /// 指向对于的Com_Para3[i]了。
        /// </summary>
        private CommonParameters _Com_Para1 = new CommonParameters();

        public CommonParameters Com_Para1
        {
            get
            {
                if (Com_Para3 != null || Com_Para3.Count != 0)
                {
                    for (int i = 0; i < Com_Para3.Count; i++)
                    {
                        if (Com_Para3[i].SourceCamIndex == 1)//弄个索引属性（可以是名字），指向指定的对象
                        {
                            _Com_Para1 = Com_Para3[i];
                        }
                    }
                }
                return _Com_Para1;
            }
            set { if (_Com_Para1 != value) { _Com_Para1 = value; RaisePropertyChanged("Com_Para1"); } }
        }



        private CommonParameters _Com_Para2 = new CommonParameters();

        public CommonParameters Com_Para2
        {
            get
            {
                if (Com_Para3 != null || Com_Para3.Count != 0)
                {
                    for (int i = 0; i < Com_Para3.Count; i++)
                    {
                        if (Com_Para3[i].SourceCamIndex == 2)
                        {
                            _Com_Para2 = Com_Para3[i];
                        }
                    }
                }
                return _Com_Para2;
            }
            set { if (_Com_Para2 != value) { _Com_Para2 = value; RaisePropertyChanged("Com_Para2"); } }
        }



        private static string CreateJsonFolder = "Json";
        private static string CommonParaFile = CreateJsonFolder + "\\CommonPara.json";


        private bool LoadComParaJsonData()
        {
            try
            {
                if (File.Exists(CommonParaFile))
                {
                    Com_Para3 = JsonConvert.DeserializeObject<ObservableCollection<CommonParameters>>(File.ReadAllText(CommonParaFile), new JsonSerializerSettings//修改parameters为自己需要存储的文件就OK？
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        TypeNameHandling = TypeNameHandling.Auto,
                        Formatting = Formatting.Indented,
                        DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
                        DateParseHandling = DateParseHandling.DateTime
                    });

                }

                if (Com_Para3 == null || Com_Para3.Count == 0)
                {
                    Com_Para3 = new ObservableCollection<CommonParameters>();

                    // add image objects mannully
                    Com_Para3.Add(new CommonParameters()
                    {
                        Name = "研究机构",
                        Height = 156
                    });
                    Com_Para3.Add(new CommonParameters()
                    {
                        Name = "公司的方式",
                        Height = 88888
                    });

                    if (Com_Para3.Count > 0)
                    {
                        for (int i = 0; i < Com_Para3.Count; i++)
                        {
                            Com_Para3[i].SourceCamIndex = i;
                        }
                    }

                    SaveComParaToJsonData();
                }
            }

            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Save Spin setting to file
        /// </summary>
        /// <returns></returns>
        private bool SaveComParaToJsonData()
        {
            try
            {
                JsonSerializer serializer = new JsonSerializer();//需要引用Newtonsoft.Json
                serializer.NullValueHandling = NullValueHandling.Ignore;
                serializer.TypeNameHandling = TypeNameHandling.Auto;
                serializer.Formatting = Formatting.Indented;
                serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
                serializer.DateParseHandling = DateParseHandling.DateTime;

                using (StreamWriter sw = new StreamWriter(CommonParaFile))
                {
                    using (JsonWriter writer = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(writer, Com_Para3, typeof(ObservableCollection<CommonParameters>));//修改parameters为自己需要存储的类的属性和命令
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
                MessageBox.Show("1ERRor");
            }
            return true;
        }


        private bool LoadComPara2JsonData()
        {
            try
            {
                if (File.Exists(CommonParaFile))
                {
                    Com_Para1 = JsonConvert.DeserializeObject<CommonParameters>(File.ReadAllText(CommonParaFile), new JsonSerializerSettings//修改parameters为自己需要存储的文件就OK？
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        TypeNameHandling = TypeNameHandling.Auto,
                        Formatting = Formatting.Indented,
                        DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
                        DateParseHandling = DateParseHandling.DateTime
                    });
                }

                else
                {
                    Directory.CreateDirectory(CreateJsonFolder);
                    Com_Para2 = new CommonParameters();
                    Com_Para2.Name = "loolololo";
                    Com_Para2.Height = 88;

                    JsonSerializer serializer = new JsonSerializer();//需要引用Newtonsoft.Json
                    serializer.NullValueHandling = NullValueHandling.Ignore;
                    serializer.TypeNameHandling = TypeNameHandling.Auto;
                    serializer.Formatting = Formatting.Indented;
                    serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
                    serializer.DateParseHandling = DateParseHandling.DateTime;

                    using (StreamWriter sw = new StreamWriter(CommonParaFile))
                    {
                        using (JsonWriter writer = new JsonTextWriter(sw))
                        {
                            serializer.Serialize(writer, Com_Para2, typeof(CommonParameters));//修改parameters为自己需要存储的类的属性和命令
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                return false;
                MessageBox.Show("2ERRor"+ ex);

            }

            return true;
        }

        /// <summary>
        /// Save Spin setting to file
        /// </summary>
        /// <returns></returns>
        private bool SaveComPara2ToJsonData()
        {
            try
            {
                JsonSerializer serializer = new JsonSerializer();//需要引用Newtonsoft.Json
                serializer.NullValueHandling = NullValueHandling.Ignore;
                serializer.TypeNameHandling = TypeNameHandling.Auto;
                serializer.Formatting = Formatting.Indented;
                serializer.DateFormatHandling = DateFormatHandling.MicrosoftDateFormat;
                serializer.DateParseHandling = DateParseHandling.DateTime;

                using (StreamWriter sw = new StreamWriter(CommonParaFile))
                {
                    using (JsonWriter writer = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(writer, Com_Para2, typeof(CommonParameters));//修改parameters为自己需要存储的类的属性和命令
                    }
                }
            }
            catch (Exception ex)
            {
                return false;
                MessageBox.Show("1ERRor" + ex);

            }
            return true;
        }














        private ICommand _SaveComand;

        public ICommand SaveComand
        {
            get
            {
                if (_SaveComand == null)
                {
                    _SaveComand = new RelayCommand(
                        param => this.UseSaveImage(),
                        param => this.CanSaveImage()
                    );
                }
                return _SaveComand;
            }
        }
        private bool CanSaveImage()
        {
            return true;
        }
        private void UseSaveImage()
        {
            SaveComParaToJsonData();
            //SaveComPara2ToJsonData();
            MessageBox.Show("JSON！！！\n╰(艹皿艹 )\t╰(艹皿艹 )\t╰(艹皿艹 )", "截图");

        }






    }
}
