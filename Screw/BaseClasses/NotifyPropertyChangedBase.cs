using System.ComponentModel;

namespace Screw.BaseClasses
{
    public class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        // Declare the event
        public event PropertyChangedEventHandler PropertyChanged;
        // Create the OnPropertyChanged method to raise the event
        protected void RaisePropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)

            {
                handler(this, new PropertyChangedEventArgs(name));

            }
        }
    }
}
