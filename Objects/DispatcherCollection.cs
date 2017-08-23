using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Windows.Threading;

namespace osu_Player.Objects
{
    public class DispatcherCollection<T> : ObservableCollection<T>
    {
        public Dispatcher EventDispatcher { get; set; }
        
        public DispatcherCollection()
        {
            InitializeEventDispatcher();
        }

        public DispatcherCollection(IEnumerable<T> collection) : base(collection)
        {
            InitializeEventDispatcher();
        }

        public DispatcherCollection(List<T> list) : base(list)
        {
            InitializeEventDispatcher();
        }

        private void InitializeEventDispatcher()
        {
            EventDispatcher = Dispatcher.CurrentDispatcher;
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (IsValidAccess())
            {
                base.OnCollectionChanged(e);
            }
            else
            {
                Action<NotifyCollectionChangedEventArgs> changed = OnCollectionChanged;
                EventDispatcher.Invoke(changed, e);
            }
        }
        
        private bool IsValidAccess()
        {
            return EventDispatcher == null ||
                EventDispatcher.Thread == Thread.CurrentThread;
        }
    }
}