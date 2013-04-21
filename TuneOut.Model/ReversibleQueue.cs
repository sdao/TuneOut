using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;

namespace TuneOut
{
    /// <summary>
    /// A queue that remembers all dequeued items and can retrieve them if necessary.
    /// </summary>
    /// <typeparam name="T">The type of objects in the queue.</typeparam>
    class ReversibleQueue<T> : IList, IReadOnlyObservableList<T>
    {
        readonly Object _syncRoot = new Object();
        readonly List<T> _backing;
        int _head = 0;
        bool _batching = false;

        public ReversibleQueue()
        {
            _backing = new List<T>();
        }


        public ReversibleQueue(IEnumerable<T> backing)
        {
            _backing = backing.ToList();
        }

        /// <summary>
        /// Temporarily stops <seealso cref="NotifyCollectionChangedEventHandler"/> from occurring.
        /// </summary>
        public void BatchChanges()
        {
            _batching = true;
        }

        /// <summary>
        /// Flushes any batched changes and sends a <seealso cref="NotifyCollectionChangedAction.Reset"/> message.
        /// </summary>
        public void FlushChanges()
        {
            _batching = false;

            OnCommonPropertyChanged();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        #region Queue

        /// <summary>
        /// Adds an object to the tail of the queue.
        /// </summary>
        /// <param name="value">The object to enqueue.</param>
        /// <returns>The position into which the new element was inserted.</returns>
        public int Enqueue(T value)
        {
            Contract.Ensures(Count == Contract.OldValue(Count) + 1);
            Contract.Ensures(((ICollection)this).Count == Contract.OldValue(((ICollection)this).Count) + 1);
            Contract.Ensures(Contract.Result<int>() < Count);
            Contract.Ensures(Contract.Result<int>() < ((ICollection)this).Count);

            int queueAddPosition = Count;
            _backing.Add(value);

            OnCommonPropertyChanged();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, queueAddPosition));

            return queueAddPosition;
        }

        /// <summary>
        /// Adds a collection of objects to the tail of the queue.
        /// </summary>
        /// <param name="items">The collection of objects to enqueue.</param>
        public void Enqueue(IEnumerable<T> items)
        {
            int queueAddStart = Count;
            var list = items as List<T> ?? items.ToList();
            _backing.AddRange(list);

            OnCommonPropertyChanged();
            for (int i = 0; i < list.Count; i++)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, list[i], queueAddStart + i));
            }
        }

        /// <summary>
        /// Removes a set number of objects, then removes an additional object from the queue, and then sets the <seealso cref="Current"/> property.
        /// </summary>
        /// <param name="index">The index of the object to return. Must be less than the <seealso cref="Count"/> of objects in the queue.</param>
        /// <returns>The last object removed.</returns>
        /// <exception cref="ArgumentOutOfRangeException">if index >= Count</exception>
        /// <exception cref="InvalidOperationException">if Count == 0</exception>
        public T Dequeue(int index = 0)
        {
            Contract.Requires<ArgumentOutOfRangeException>(index < Count);
            Contract.Requires<InvalidOperationException>(Count != 0);
            Contract.Ensures(Count == Contract.OldValue(Count) - index - 1);
            Contract.Ensures(((ICollection)this).Count == Contract.OldValue(((ICollection)this).Count) - index - 1);

            int queueRemoveStart = _head;

            _head += index + 1;

            OnCommonPropertyChanged();
            for (int i = queueRemoveStart; i < _head; i++)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, _backing[i], 0));
            }

            return Current;
        }

        /// <summary>
        /// Re-enqueues the last object removed from the queue, that is, the object that is currently contained in the <seealso cref="Current"/> property.
        /// </summary>
        /// <returns>The new value of the <seealso cref="Current"/> property, or null if the first object dequeued from the queue gets re-enqueued.</returns>
        /// <exception cref="InvalidOperationException">if <seealso cref="DequeuedCount"/> &lt; 1 when the method is called</exception>
        public T EnqueueBack()
        {
            Contract.Requires<InvalidOperationException>(DequeuedCount > 0);
            Contract.Ensures(Count == Contract.OldValue(Count) + 1);
            Contract.Ensures(((ICollection)this).Count == Contract.OldValue(((ICollection)this).Count) + 1);

            _head--;

            OnCommonPropertyChanged();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, _backing[_head], 0));

            return Current;
        }

        /// <summary>
        /// Inserts an object at an arbitrary position in the queue.
        /// </summary>
        /// <param name="index">The position to insert the object. Must be between 0 and the <seealso cref="Count"/> of the queue, inclusive.</param>
        /// <param name="value">The object to insert.</param>
        /// <exception cref="ArgumentOutOfRangeException">if index &gt; Count || index &lt; 0</exception>
        public void Insert(int index, T value)
        {
            Contract.Requires<ArgumentOutOfRangeException>(index <= Count && index >= 0);
            Contract.Ensures(Count == Contract.OldValue(Count) + 1);
            Contract.Ensures(((ICollection)this).Count == Contract.OldValue(((ICollection)this).Count) + 1);

            _backing.Insert(QueueToBackingIndex(index), (T)value);

            OnCommonPropertyChanged();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, value, index));
        }

        /// <summary>
        /// Removes the object currently contained in the <seealso cref="Current"/> property from the queue's memory,
        /// dequeuing the next object in the process.
        /// </summary>
        /// <returns>The object that was removed.</returns>
        /// <exception cref="InvalidOperationException">if <seealso cref="Count"/> is 0 when the method is called</exception>
        /// <exception cref="InvalidOperationException">if <seealso cref="HasCurrent"/> is false when the method is called</exception>
        /// <remarks>To get the object that is automatically dequeued as a side effect of this method, check the <seealso cref="Current"/> property.</remarks>
        public T Kill()
        {
            Contract.Requires<InvalidOperationException>(Count != 0);
            Contract.Requires<InvalidOperationException>(HasCurrent);
            Contract.Ensures(Count == Contract.OldValue(Count) - 1);
            Contract.Ensures(((ICollection)this).Count == Contract.OldValue(((ICollection)this).Count) - 1);

            var value = _backing[_head - 1];
            _backing.RemoveAt(_head - 1);

            OnCommonPropertyChanged();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, Current, 0));

            return value;
        }

        /// <summary>
        /// Removes the specified object if it exists in the queue.
        /// </summary>
        /// <param name="value">The object to remove.</param>
        /// <returns>Whether the object was found and removed.</returns>
        public bool Remove(T value)
        {
            int queueIndex = IndexOf((T)value);
            if (queueIndex != -1)
            {
                RemoveAt(queueIndex);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Removes the object at an arbitrary index in the queue.
        /// </summary>
        /// <param name="index">The index of the object to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">if index &gt;= Count || index &lt; 0</exception>
        public void RemoveAt(int index)
        {
            Contract.Requires<ArgumentOutOfRangeException>(index < Count && index >= 0);

            var objectRemoved = _backing[QueueToBackingIndex(index)];
            _backing.RemoveAt(QueueToBackingIndex(index));

            OnCommonPropertyChanged();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, objectRemoved, index));
        }

        /// <summary>
        /// Removes the collection of objects if they exist in the queue.
        /// </summary>
        /// <param name="items">The objects to remove.</param>
        /// <exception cref="ArgumentNullException">if <paramref name="items"/> is null</exception>
        public void Remove(IEnumerable<T> items)
        {
            Contract.Requires<ArgumentNullException>(items != null);

            foreach (var value in items)
            {
                Remove(value);
            }
        }

        /// <summary>
        /// Resets the entire queue, including all dequeued objects in memory.
        /// </summary>
        public void Clear()
        {
            _backing.Clear();
            _head = 0;

            OnCommonPropertyChanged();
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <summary>
        /// Looks for an object, and returns its index if found.
        /// </summary>
        /// <param name="value">The object to look for.</param>
        /// <returns>The object's index if it exists in the queue, or -1 otherwise.</returns>
        public int IndexOf(T value)
        {
            int backingIndex = _backing.IndexOf(value, _head);
            return BackingToQueueIndex(backingIndex);
        }

        #endregion

        #region Properties

        /// <summary>
        /// The previous object dequeued, if any objects have been dequeued thus far.
        /// </summary>
        public T Current
        {
            get
            {
                if (HasCurrent)
                {
                    return _backing[_head - 1];
                }
                else
                {
                    return default(T);
                }
            }
        }

        /// <summary>
        /// Gets whether an object has already been dequeued (and not yet re-enqueued).
        /// When true, <seealso cref="EnqueueBack()"/> and <seealso cref="Kill()"/> can be called.
        /// </summary>
        public bool HasCurrent
        {
            get
            {
                return _head > 0;
            }
        }

        /// <summary>
        /// The number of objects that have been enqueued and not yet dequeued.
        /// </summary>
        public int Count
        {
            get
            {
                Contract.Ensures(Contract.Result<int>() >= 0);
                return _backing.Count - _head;
            }
        }

        /// <summary>
        /// Gets the count of dequeued objects that have not yet been re-enqueued.
        /// </summary>
        public int DequeuedCount
        {
            get
            {
                return _head;
            }
        }

        public T this[int index]
        {
            get
            {


                return _backing[QueueToBackingIndex(index)];
            }
            set
            {
                Contract.Requires<ArgumentOutOfRangeException>(index < Count && index >= 0);

                _backing[QueueToBackingIndex(index)] = value;
            }
        }

        #endregion

        #region Index conversion

        private int BackingToQueueIndex(int backingIndex)
        {
            if (backingIndex == -1) return -1;
            else return backingIndex - _head;
        }

        private int QueueToBackingIndex(int queueIndex)
        {
            if (queueIndex == -1) return -1;
            else return queueIndex + _head;
        }

        #endregion

        #region IList implementation

        int IList.Add(object value)
        {
            if (value is T)
            {
                return Enqueue((T)value);
            }
            else throw new ArgumentException("value");
        }

        void IList.Clear()
        {
            Clear();
        }

        bool IList.Contains(object value)
        {
            if (value is T)
            {
                return _backing.IndexOf((T)value, _head) != -1;
            }
            else return false;
        }

        int IList.IndexOf(object value)
        {
            if (value is T)
            {
                return IndexOf((T)value);
            }
            else return -1;
        }

        void IList.Insert(int index, object value)
        {
            if (value is T)
            {
                if (index <= Count && index >= 0)
                {
                    Insert(index, (T)value);
                }
                else throw new ArgumentOutOfRangeException("index");
            }
            else throw new ArgumentException("value");
        }

        bool IList.IsFixedSize
        {
            get { return false; }
        }

        bool IList.IsReadOnly
        {
            get { return false; }
        }

        void IList.Remove(object value)
        {
            if (value is T)
            {
                Remove((T)value);
            }
            else throw new ArgumentException("value");
        }

        void IList.RemoveAt(int index)
        {
            if (index < Count && index >= 0)
            {
                RemoveAt(index);
            }
            else throw new ArgumentOutOfRangeException("index");
        }

        object IList.this[int index]
        {
            get
            {
                if (index < Count && index >= 0)
                {
                    return this[index];
                }
                else throw new ArgumentOutOfRangeException("index");
            }
            set
            {
                if (value is T)
                {
                    if (index < Count && index >= 0)
                    {
                        this[index] = (T)value;
                    }
                    else throw new ArgumentOutOfRangeException("index");
                }
                else throw new ArgumentException("value");
            }
        }

        #endregion

        #region ICollection implementation

        void ICollection.CopyTo(Array array, int index)
        {
            if (array.Length - index >= Count)
            {
                for (int i = 0; i < Count; i++)
                {
                    array.SetValue(_backing[QueueToBackingIndex(i)], index + i);
                }
            }
            else throw new ArgumentException("array");
        }

        int ICollection.Count
        {
            get { return Count; }
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }

        object ICollection.SyncRoot
        {
            get { return _syncRoot; }
        }

        #endregion

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _backing.Skip(_head).GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _backing.Skip(_head).GetEnumerator();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnCommonPropertyChanged()
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (!_batching && h != null)
            {
                h(this, new PropertyChangedEventArgs("Count"));
                h(this, new PropertyChangedEventArgs("Current"));
                h(this, new PropertyChangedEventArgs("Item[]"));
                h(this, new PropertyChangedEventArgs("HasCurrent"));
            }
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        protected void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_batching && CollectionChanged != null)
            {
                CollectionChanged(this, e);
            }
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(_backing != null);
            Contract.Invariant(_head >= 0);
            Contract.Invariant(_head <= _backing.Count);
            Contract.Invariant(_syncRoot != null);
            Contract.Invariant(Count >= 0);
            Contract.Invariant(DequeuedCount >= 0);
        }
    }

    /// <summary>
    /// Represents a read-only collection of items that can be accessed by index, and which
    /// is observable if its backing collection changes.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IReadOnlyObservableList<T> : INotifyPropertyChanged, INotifyCollectionChanged, IReadOnlyList<T>
    {

    }
}
