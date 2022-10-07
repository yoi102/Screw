using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace test.BaseClasses
{




    /// <summary>
    /// Available cross ViewModel messages
    /// </summary>
    public enum ViewModelMessages
    {
        MessageType1 = 1,
    };

    /// <summary>
    /// Provides loosely-coupled messaging between
    /// various colleagues.  All references to objects
    /// are stored weakly, to prevent memory leaks.
    /// </summary>
    public class Mediator
    {
        #region Fields
        private static Mediator _instance = new Mediator();

        Dictionary<ViewModelMessages, Action<Object>> internalList
            = new Dictionary<ViewModelMessages, Action<Object>>();
        #endregion // Fields

        #region Property
        /// <summary>
        /// The singleton instance
        /// </summary>
        public static Mediator Instance
        {
            get
            {
                return _instance;
            }
        }
        #endregion

        #region Ctor
        //CTORs
        static Mediator() { }
        private Mediator() { }
        #endregion // Constructor

        #region Methods
        public void Register(ViewModelMessages message, Action<object> callback)
        {
            internalList[message] = callback;
        }

        public void NotifyColleagues(ViewModelMessages message, object args)
        {
            if (internalList.ContainsKey(message))
            {
                internalList[message].Invoke(args);
            }
        }
        #endregion // Methods
    }
}

