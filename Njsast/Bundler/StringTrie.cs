using System;
using System.Collections;
using System.Collections.Generic;

namespace Njsast.Bundler
{
    public class StringTrie<T> : IEnumerable<KeyValuePair<StructList<string>, T>>
    {
        struct Val
        {
            public T Value;
            public bool HasValue;
            public RefDictionary<string, Val>? Children;
        }

        Val _root;

        public bool IsJustRoot => _root.Children == null;

        public void Add(in ReadOnlySpan<string> key, T value)
        {
            this[key] = value;
        }

        public ref T this[in ReadOnlySpan<string> key]
        {
            get
            {
                ref var p = ref _root;
                var prefix = key;
                while (!prefix.IsEmpty)
                {
                    p.Children ??= new RefDictionary<string, Val>();
                    p = ref p.Children.GetOrAddValueRef(prefix[0]);
                    prefix = prefix.Slice(1);
                }
                p.HasValue = true;
                return ref p.Value;
            }
        }

        public void EnsureKeyExists(in ReadOnlySpan<string> key,
            Func<IEnumerable<(string key, T value)>, T> valueFactory)
        {
            ref var p = ref _root;
            var prefix = key;
            while (!prefix.IsEmpty)
            {
                if (p.Children == null || !p.Children.ContainsKey(prefix[0]))
                {
                    return;
                }
                p = ref p.Children.GetOrFakeValueRef(prefix[0]);
                prefix = prefix.Slice(1);
            }
            if (p.HasValue) return;
            p.Value = valueFactory(Iterate(p.Children, valueFactory));
            p.HasValue = true;
        }


        static T Build(RefDictionary<string, Val> children, uint idx,
            Func<IEnumerable<(string key, T value)>, T> valueFactory)
        {
            ref var child = ref children.ValueRef(idx);
            if (child.HasValue) return child.Value;
            child.Value = valueFactory(Iterate(child.Children, valueFactory));
            child.HasValue = true;
            return child.Value;
        }

        public IEnumerable<T> Values()
        {
            if (_root.HasValue)
                yield return _root.Value;
            foreach (var value in RecursiveIterateValues(_root.Children))
            {
                yield return value;
            }
        }

        static IEnumerable<T> RecursiveIterateValues(RefDictionary<string,Val>? children)
        {
            if (children == null) yield break;
            foreach (var (_, value) in children)
            {
                if (value.HasValue)
                {
                    yield return value.Value;
                }
                foreach (var pair in RecursiveIterateValues(value.Children))
                {
                    yield return pair;
                }
            }
        }

        static IEnumerable<(string key, T value)> Iterate(RefDictionary<string, Val>? children, Func<IEnumerable<(string key, T value)>,T> valueFactory)
        {
            if (children == null) yield break;
            foreach (var idx in children.Index)
            {
                yield return (children.KeyRef(idx), Build(children, idx, valueFactory));
            }
        }

        /// <param name="key">to find</param>
        /// <param name="prefixLen">len of prefix with longest match</param>
        /// <param name="value">value on that longest match</param>
        /// <returns>true if value exists on exact key</returns>
        public bool TryFindLongestPrefix(in ReadOnlySpan<string> key, out int prefixLen, out T value)
        {
            prefixLen = -1;
            value = default!;
            var prefix = key;
            ref var p = ref _root;
            if (p.HasValue)
            {
                prefixLen = 0;
                value = p.Value;
            }
            var idx = 0;
            while (!prefix.IsEmpty)
            {
                if (p.Children == null || !p.Children.ContainsKey(prefix[0]))
                {
                    return idx == prefixLen;
                }
                p = ref p.Children.GetOrFakeValueRef(prefix[0]);
                idx++;
                if (p.HasValue)
                {
                    prefixLen = idx;
                    value = p.Value;
                }
                prefix = prefix.Slice(1);
            }
            return idx == prefixLen;
        }

        IEnumerator<KeyValuePair<StructList<string>, T>> IEnumerable<KeyValuePair<StructList<string>, T>>.GetEnumerator()
        {
            var key = new StructList<string>();
            if (_root.HasValue)
                yield return new KeyValuePair<StructList<string>, T>(key, _root.Value);
            foreach (var pair in RecursiveIterate(_root.Children, key))
            {
                yield return pair;
            }
        }

        static IEnumerable<KeyValuePair<StructList<string>,T>> RecursiveIterate(RefDictionary<string,Val>? children, StructList<string> key)
        {
            if (children == null) yield break;
            foreach (var (name, value) in children)
            {
                key.Add(name);
                if (value.HasValue)
                {
                    yield return new KeyValuePair<StructList<string>, T>(key, value.Value);
                }
                foreach (var pair in RecursiveIterate(value.Children, key))
                {
                    yield return pair;
                }
                key.Pop();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<StructList<string>, T>>)this).GetEnumerator();
        }
    }
}
