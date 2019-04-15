using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Text.RegularExpressions;
using System.Web;
using System.Threading.Tasks;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Security.Permissions;

namespace Webapi.net
{
    [Serializable]
    public class ParamsCollection : ICollection, IEnumerable, ISerializable, IDeserializationCallback
    {
        public ParamsCollection()
        {

        }

        protected ParamsCollection(SerializationInfo info, StreamingContext context)
        {
            this._SerializationInfo = info;
        }

        private static string[] EMPTY_STRING_ARRAY = new string[] { };

        [NonSerialized]
        private object _SyncRoot;

        private SerializationInfo _SerializationInfo;

        private string[] _CachedAllKeys = null;
        private Dictionary<string, List<string>> _KeyedValues = null;
        private List<string> _IndexedValues = null;

        public virtual void Add(string key, string value)
        {
            if (_KeyedValues == null)
            {
                _KeyedValues = new Dictionary<string, List<string>>();
            }

            if (!_KeyedValues.TryGetValue(key, out var list))
            {
                list = new List<string>();
                _KeyedValues[key] = list;
            }

            list.Add(value);

            InvalidateCachedArrays();
        }

        public virtual void Add(string value)
        {
            if (_IndexedValues == null)
            {
                _IndexedValues = new List<string>();
            }

            _IndexedValues.Add(value);

            InvalidateCachedArrays();
        }

        public virtual void Set(string key, string value)
        {
            if (_KeyedValues == null)
            {
                _KeyedValues = new Dictionary<string, List<string>>();
            }

            if (!_KeyedValues.TryGetValue(key, out var list))
            {
                list = new List<string>();
                _KeyedValues[key] = list;
            }
            else
            {
                list.Clear();
            }

            list.Add(value);

            InvalidateCachedArrays();
        }

        public virtual void Set(int index, string value)
        {
            if (_IndexedValues == null)
            {
                _IndexedValues = new List<string>();
            }

            if (_IndexedValues.Count > index)
            {
                _IndexedValues[index] = value;
            }
            else
            {
                while (_IndexedValues.Count < index)
                {
                    _IndexedValues.Add(null);
                }

                _IndexedValues.Add(value);
            }

            InvalidateCachedArrays();
        }

        public virtual void Clear()
        {
            _KeyedValues = null;
            _IndexedValues = null;
            InvalidateCachedArrays();
        }

        public virtual string Get(string key)
        {
            if (_KeyedValues != null && _KeyedValues.TryGetValue(key, out var list))
            {
                var count = list.Count;
                return count == 0 ? null : count == 1 ? list[0] : string.Join(", ", list);
            }

            return null;
        }

        public virtual string[] GetValues(string key)
        {
            if (_KeyedValues != null && _KeyedValues.TryGetValue(key, out var list))
            {
                return list.ToArray();
            }

            return null;
        }

        public virtual string Get(int index)
        {
            return _IndexedValues != null ? _IndexedValues[index] : null;
        }

        public virtual bool ContainsKey(string key)
        {
            return _KeyedValues != null && _KeyedValues.ContainsKey(key);
        }

        public virtual int IndexedCount
        {
            get
            {
                return _IndexedValues == null ? 0 : _IndexedValues.Count;
            }
        }

        public virtual int KeyedCount
        {
            get
            {
                return _KeyedValues == null ? 0 : _KeyedValues.Count;
            }
        }

        public virtual int Count
        {
            get
            {
                return IndexedCount + KeyedCount;
            }
        }

        public virtual string[] AllKeys
        {
            get
            {
                if (_CachedAllKeys == null)
                {
                    if (this._KeyedValues == null)
                        _CachedAllKeys = EMPTY_STRING_ARRAY;
                    else
                        _CachedAllKeys = this._KeyedValues.Keys.ToArray();
                }

                return _CachedAllKeys;
            }
        }

        public string this[string key]
        {
            get
            {
                return this.Get(key);
            }
            set
            {
                this.Set(key, value);
            }
        }

        public string this[int index]
        {
            get
            {
                return this.Get(index);
            }
            set
            {
                this.Set(index, value);
            }
        }

        public bool HasKeys()
        {
            return this._KeyedValues.Count > 0;
        }
        
        protected void InvalidateCachedArrays()
        {
            this._CachedAllKeys = null;
        }

        public virtual void Remove(string key)
        {
            if (_KeyedValues != null && _KeyedValues.Remove(key))
            {
                this.InvalidateCachedArrays();
            }
        }

        void System.Collections.ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }


            if (index < 0)
            {
                throw new ArgumentOutOfRangeException("index", "Index out of range");
            }

            if (array.Length - index < this.Count)
            {
                throw new ArgumentException("Insufficient space");
            }

            IEnumerator enumerator = this.GetEnumerator();
            while (enumerator.MoveNext())
            {
                int num = index;
                index = num + 1;
                array.SetValue(enumerator.Current, num);
            }
        }

        object System.Collections.ICollection.SyncRoot
        {
            get
            {
                if (this._SyncRoot == null)
                {
                    Interlocked.CompareExchange(ref this._SyncRoot, new object(), null);
                }
                return this._SyncRoot;
            }
        }

        bool System.Collections.ICollection.IsSynchronized
        {
            get
            {
                return false;
            }
        }

        public virtual IEnumerator GetEnumerator()
        {
            return new ParamsCollectionEnumerator(this);
        }

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            int indexedCount = this.IndexedCount;
            int keyedCount = this.KeyedCount;
            info.AddValue("IndexedCount", indexedCount);
            info.AddValue("KeyedCount", keyedCount);

            string[] indexedValues = new string[indexedCount];

            for (int i = 0; i < indexedCount; i++)
            {
                indexedValues[i] = this.Get(i);
            }

            info.AddValue("IndexedValues", indexedValues, typeof(string[]));

            string[] keys = this.AllKeys;
            object[] values = new object[keyedCount];

            for (int i = 0; i < keyedCount; i++)
            {
                values[i] = this.GetValues(keys[i]).ToArray();
            }

            info.AddValue("Keys", keys, typeof(string[]));
            info.AddValue("Values", values, typeof(object[]));
        }

        public virtual void OnDeserialization(object sender)
        {
            if (this._SerializationInfo == null)
            {
                throw new SerializationException();
            }

            SerializationInfo serializationInfo = this._SerializationInfo;
            this._SerializationInfo = null;

            int indexedCount = 0;
            int keyedCount = 0;
            string[] indexedValues = null;
            string[] keys = null;
            object[] values = null;

            SerializationInfoEnumerator enumerator = serializationInfo.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string name = enumerator.Name;
                switch (name)
                {
                    case "IndexedCount":
                        {
                            indexedCount = serializationInfo.GetInt32("IndexedCount");
                            break;
                        }

                    case "KeyedCount":
                        {
                            keyedCount = serializationInfo.GetInt32("KeyedCount");
                            break;
                        }

                    case "IndexedValues":
                        {
                            indexedValues = (string[])serializationInfo.GetValue("IndexedValues", typeof(string[]));
                            break;
                        }

                    case "Keys":
                        {
                            keys = (string[])serializationInfo.GetValue("Keys", typeof(string[]));
                            break;
                        }

                    case "Values":
                        {
                            values = (object[])serializationInfo.GetValue("Values", typeof(object[]));
                            break;
                        }

                    default:
                        break;
                }
            }

            if (indexedValues == null || keys == null || values == null)
            {
                throw new SerializationException();
            }

            if (indexedCount > 0)
            {
                this._IndexedValues = new List<string>(indexedValues);
            }
            else
            {
                this._IndexedValues = null;
            }

            if (keyedCount > 0)
            {
                this._KeyedValues = new Dictionary<string, List<string>>();
                for (int i = 0; i < keyedCount; i++)
                {
                    this._KeyedValues[keys[i]] = new List<string>(values[i] as IEnumerable<string>);
                }
            }
            else
            {
                this._KeyedValues = null;
            }

            InvalidateCachedArrays();
        }

        [Serializable]
        internal class ParamsCollectionEnumerator : IEnumerator
        {
            private int _Pos;
            private ParamsCollection _Coll;

            public object Current
            {
                get
                {
                    if (this._Pos < 0 || this._Pos >= this._Coll.Count)
                    {
                        throw new InvalidOperationException("Invalid operation");
                    }

                    var indexedCount = this._Coll.IndexedCount;

                    if (this._Pos < indexedCount)
                    {
                        return this._Coll.Get(_Pos);
                    }

                    return this._Coll.Get(this._Coll.AllKeys[this._Pos - indexedCount]);
                }
            }

            internal ParamsCollectionEnumerator(ParamsCollection coll)
            {
                this._Coll = coll;
                this._Pos = -1;
            }

            public bool MoveNext()
            {
                if (this._Pos >= this._Coll.Count - 1)
                {
                    this._Pos = this._Coll.Count;
                    return false;
                }

                this._Pos++;

                return true;
            }

            public void Reset()
            {
                this._Pos = -1;
            }
        }
    }
}
