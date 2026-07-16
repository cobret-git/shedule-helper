using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SheduleHelper.Core.Models
{
    /// <summary>
    /// An <see cref="ObservableCollection{T}"/> that can add or replace many items at once while
    /// suppressing per-item change notifications, firing a single reset notification at the end
    /// instead. Avoids the UI-thrash and O(n) individual notifications that repeated
    /// <see cref="ObservableCollection{T}.Add"/> calls would otherwise raise for bulk updates.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    public class ObservableRangeCollection<T> : ObservableCollection<T>
    {
        #region Fields
        private bool _isNotificationSuspended;
        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new, empty instance of the <see cref="ObservableRangeCollection{T}"/> class.
        /// </summary>
        public ObservableRangeCollection() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableRangeCollection{T}"/> class that
        /// contains elements copied from the specified collection.
        /// </summary>
        /// <param name="collection">The collection whose elements are copied to the new collection.</param>
        public ObservableRangeCollection(IEnumerable<T> collection) : base(collection) { }
        #endregion

        #region Methods

        /// <summary>
        /// Adds a range of items and fires a single notification at the end.
        /// </summary>
        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            _isNotificationSuspended = true;

            try
            {
                foreach (var item in collection)
                {
                    Items.Add(item);
                }
            }
            finally
            {
                _isNotificationSuspended = false;
                RaiseResetNotifications();
            }
        }

        /// <summary>
        /// Clears the collection and adds a new range, firing only one notification.
        /// </summary>
        public void ReplaceRange(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            _isNotificationSuspended = true;

            try
            {
                Items.Clear();
                foreach (var item in collection)
                {
                    Items.Add(item);
                }
            }
            finally
            {
                _isNotificationSuspended = false;
                RaiseResetNotifications();
            }
        }
        #endregion

        #region Handlers
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_isNotificationSuspended)
            {
                base.OnCollectionChanged(e);
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (!_isNotificationSuspended)
            {
                base.OnPropertyChanged(e);
            }
        }
        #endregion

        #region Helpers
        private void RaiseResetNotifications()
        {
            OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
        #endregion
    }
}
