using NLog;
using Screw.BaseClasses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Screw.Model
{
    public class DiskManage : NotifyPropertyChangedBase
    {
        Logger logger = LogManager.GetCurrentClassLogger();

        #region Property
        /// <summary>
        /// disk used space
        /// </summary>
        private int _UsedSpace;
        public int UsedSpace
        {
            get { return _UsedSpace; }
            set { if (_UsedSpace != value) { _UsedSpace = value; RaisePropertyChanged("UsedSpace"); } }
        }

        /// <summary>
        /// disk total space
        /// </summary>
        private int _TotalSpace;
        public int TotalSpace
        {
            get { return _TotalSpace; }
            set { if (_TotalSpace != value) { _TotalSpace = value; RaisePropertyChanged("TotalSpace"); } }
        }

        /// <summary>
        /// disk used space ratio
        /// </summary>
        private double _UsedSpaceRatio;
        public double UsedSpaceRatio
        {
            get { return _UsedSpaceRatio; }
            set { if (_UsedSpaceRatio != value) { _UsedSpaceRatio = value; RaisePropertyChanged("UsedSpaceRatio"); } }
        }

        /// <summary>
        /// disk used space ratio
        /// </summary>
        private bool _DiskCleanBusy;
        public bool DiskCleanBusy
        {
            get { return _DiskCleanBusy; }
            set { if (_DiskCleanBusy != value) { _DiskCleanBusy = value; RaisePropertyChanged("DiskCleanBusy"); } }
        }

        /// <summary>
        /// current disk drive
        /// </summary>
        private string _CurrentDrive;
        public string CurrentDrive
        {
            get { return _CurrentDrive; }
            set { if (_CurrentDrive != value) { _CurrentDrive = value; RaisePropertyChanged("CurrentDrive"); } }
        }

        #endregion

        #region Operations
        /// <summary>
        /// get disk info
        /// </summary>
        /// <returns></returns>
        public bool GetDiskSpaceInfo()
        {
            try
            {
                string disk = Directory.GetDirectoryRoot(Directory.GetCurrentDirectory());
                CurrentDrive = disk.Substring(0, 1);
                // Create a DriveInfo instance of current drive drive
                DriveInfo dDrive = new DriveInfo(CurrentDrive);

                // When the drive is accessible..
                if (dDrive.IsReady)
                {
                    TotalSpace = (int)(dDrive.TotalSize / Math.Pow(2, 30));
                    UsedSpace = TotalSpace - (int)(dDrive.AvailableFreeSpace / Math.Pow(2, 30));
                    // Calculate the percentage free space
                    UsedSpaceRatio = UsedSpace / (double)TotalSpace;

                    // Ouput drive information
                    //Console.WriteLine("Drive: {0} ({1}, {2})",
                    //    dDrive.Name, dDrive.DriveFormat, dDrive.DriveType);

                    //Console.WriteLine("\tFree space:\t{0}",
                    //    dDrive.AvailableFreeSpace);
                    //Console.WriteLine("\tTotal space:\t{0}",
                    //    dDrive.TotalSize);

                    //Console.WriteLine("\n\tPercentage used space: {0:0.00}%.",
                    //    UsedSpaceRatio);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("GetDiskSpaceInfo|" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// clean disk
        /// </summary>
        public void CleanDisk(int daysToKeep = 5)
        {
            DiskCleanBusy = true;
            try
            {
                logger.Info("Cleaning out of date images");
                // remove images
                DeleteSubDirectors(@"images", daysToKeep);
            }
            catch (Exception ex)
            {
                logger.Error("CleanDisk|" + ex.Message);
            }
            finally
            {
                DiskCleanBusy = false;
            }

        }

        /// <summary>
        /// remove out of date sub directors in specific folder
        /// </summary>
        /// <param name="path"></param>
        /// <param name="daysToKeep"></param>
        private void DeleteSubDirectors(string path, int daysToKeep)
        {
            DateTime limitDate = DateTime.Now.AddDays(-daysToKeep);
            var subDirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
            foreach (string subDir in subDirs)
            {
                DateTime createdTime = Directory.GetCreationTime(subDir);
                if (createdTime < limitDate)
                {
                    logger.Info("Delete images in " + subDir);
                    Directory.Delete(subDir, true);
                }
            }
        }

        /*
        public static void DeleteFilesAndFoldersRecursively(string target_dir)
        {
            foreach (string file in Directory.GetFiles(target_dir))
            {
                File.Delete(file);
            }

            foreach (string subDir in Directory.GetDirectories(target_dir))
            {
                DeleteFilesAndFoldersRecursively(subDir);
            }

            Thread.Sleep(1); // This makes the difference between whether it works or not. Sleep(0) is not enough.
            Directory.Delete(target_dir);
        }
        */

        #endregion

        #region Commands

        /// <summary>
        /// GetDiskInfo command
        /// </summary>
        private ICommand _GetDiskInfoCommand;
        public ICommand GetDiskInfoCommand
        {
            get
            {
                if (_GetDiskInfoCommand == null)
                {
                    _GetDiskInfoCommand = new RelayCommand(
                        param => this.GetDiskInfoExecute(),
                        param => this.CanGetDiskInfo()
                    );
                }
                return _GetDiskInfoCommand;
            }
        }
        private bool CanGetDiskInfo()
        {
            return true;
        }
        private void GetDiskInfoExecute()
        {
            GetDiskSpaceInfo();
        }

        /// <summary>
        /// DiskClean command
        /// </summary>
        private ICommand _DiskCleanCommand;
        public ICommand DiskCleanCommand
        {
            get
            {
                if (_DiskCleanCommand == null)
                {
                    _DiskCleanCommand = new RelayCommand(
                        param => this.DiskCleanExecute(),
                        param => this.CanDiskClean()
                    );
                }
                return _DiskCleanCommand;
            }
        }
        private bool CanDiskClean()
        {
            return !DiskCleanBusy;
        }
        private void DiskCleanExecute()
        {
            if (!DiskCleanBusy)
            {
                Task.Run(() =>
                {
                    if (!DiskCleanBusy) CleanDisk();
                    // update disk info
                    GetDiskSpaceInfo();
                });
            }
        }

        #endregion
    }
}
