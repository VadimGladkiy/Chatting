using System;
using System.Collections;
using System.Collections.Generic;

namespace Chatting.Server.Collections
{
    /// <summary>
    /// A balanced AVL order-statistics tree that supports O(log n) insert, remove,
    /// and index-based access (GetByIndex) and rank queries (IndexOf).
    /// Duplicates are allowed; insertion places equal keys to the right.
    /// </summary>
    public class OrderStatisticsTree<T> : IEnumerable<T>
    {
        private readonly IComparer<T> _comparer;
        private Node? _root;

        public int Count => _root?.Size ?? 0;

        public OrderStatisticsTree(IComparer<T>? comparer = null)
        {
            _comparer = comparer ?? Comparer<T>.Default;
        }

        private sealed class Node
        {
            public T Value;
            public Node? Left;
            public Node? Right;
            public int Height;
            public int Size; // size of subtree rooted here

            public Node(T value)
            {
                Value = value;
                Height = 1;
                Size = 1;
            }
        }

        #region Public API

        public void Add(T value)
        {
            _root = Insert(_root, value);
        }

        /// <summary>
        /// Remove all elements from the tree.
        /// </summary>
        public void Clear()
        {
            _root = null;
        }

        public bool Remove(T value)
        {
            bool removed;
            (_root, removed) = RemoveInternal(_root, value);
            return removed;
        }

        /// <summary>
        /// Get element at zero-based index in sorted order.
        /// </summary>
        public T GetByIndex(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return GetByIndex(_root!, index);
        }

        /// <summary>
        /// Return the index of the provided value in sorted order.
        /// If duplicates exist, returns the index of the first (left-most) matching element.
        /// Returns -1 if not found.
        /// </summary>
        public int IndexOf(T value)
        {
            return IndexOf(_root, value, 0);
        }

        public bool Contains(T value)
        {
            return IndexOf(value) >= 0;
        }

        #endregion

        #region Internal Tree Operations

        private Node Insert(Node? node, T value)
        {
            if (node == null) return new Node(value);

            int cmp = _comparer.Compare(value, node.Value);
            if (cmp < 0)
            {
                node.Left = Insert(node.Left, value);
            }
            else
            {
                // place equal values to the right to allow duplicates
                node.Right = Insert(node.Right, value);
            }

            Update(node);
            return Balance(node);
        }

        private (Node? node, bool removed) RemoveInternal(Node? node, T value)
        {
            if (node == null) return (null, false);

            int cmp = _comparer.Compare(value, node.Value);
            bool removed = false;

            if (cmp < 0)
            {
                (node.Left, removed) = RemoveInternal(node.Left, value);
            }
            else if (cmp > 0)
            {
                (node.Right, removed) = RemoveInternal(node.Right, value);
            }
            else
            {
                // Found a node equal to value. To remove a single instance, we remove this node.
                removed = true;
                if (node.Left == null || node.Right == null)
                {
                    node = node.Left ?? node.Right;
                    return (node, true);
                }
                else
                {
                    // Replace with in-order successor (smallest in right subtree)
                    Node successor = GetMin(node.Right);
                    node.Value = successor.Value;
                    (node.Right, _) = RemoveInternal(node.Right, successor.Value);
                }
            }

            if (node == null) return (null, removed);

            Update(node);
            return (Balance(node), removed);
        }

        private static Node GetMin(Node node)
        {
            while (node.Left != null) node = node.Left;
            return node;
        }

        private void Update(Node node)
        {
            int lh = node.Left?.Height ?? 0;
            int rh = node.Right?.Height ?? 0;
            node.Height = 1 + Math.Max(lh, rh);

            int ls = node.Left?.Size ?? 0;
            int rs = node.Right?.Size ?? 0;
            node.Size = 1 + ls + rs;
        }

        private int BalanceFactor(Node node)
        {
            int lh = node.Left?.Height ?? 0;
            int rh = node.Right?.Height ?? 0;
            return lh - rh;
        }

        private Node Balance(Node node)
        {
            int bf = BalanceFactor(node);
            if (bf > 1)
            {
                // left heavy
                if (BalanceFactor(node.Left!) < 0)
                {
                    node.Left = RotateLeft(node.Left!);
                }
                return RotateRight(node);
            }
            else if (bf < -1)
            {
                // right heavy
                if (BalanceFactor(node.Right!) > 0)
                {
                    node.Right = RotateRight(node.Right!);
                }
                return RotateLeft(node);
            }
            return node;
        }

        private Node RotateRight(Node y)
        {
            Node x = y.Left!;
            Node? T2 = x.Right;
            x.Right = y;
            y.Left = T2;

            Update(y);
            Update(x);
            return x;
        }

        private Node RotateLeft(Node x)
        {
            Node y = x.Right!;
            Node? T2 = y.Left;
            y.Left = x;
            x.Right = T2;

            Update(x);
            Update(y);
            return y;
        }

        private T GetByIndex(Node node, int index)
        {
            int leftSize = node.Left?.Size ?? 0;
            if (index < leftSize)
            {
                return GetByIndex(node.Left!, index);
            }
            if (index == leftSize)
            {
                return node.Value;
            }
            return GetByIndex(node.Right!, index - leftSize - 1);
        }

        private int IndexOf(Node? node, T value, int baseIndex)
        {
            if (node == null) return -1;

            int cmp = _comparer.Compare(value, node.Value);
            if (cmp < 0)
            {
                return IndexOf(node.Left, value, baseIndex);
            }
            else if (cmp > 0)
            {
                int leftSize = node.Left?.Size ?? 0;
                return IndexOf(node.Right, value, baseIndex + leftSize + 1);
            }
            else
            {
                // Found a match; find left-most match in left subtree (there may be duplicates)
                int leftIndex = IndexOf(node.Left, value, baseIndex);
                if (leftIndex != -1) return leftIndex;

                int leftSize = node.Left?.Size ?? 0;
                return baseIndex + leftSize;
            }
        }

        #endregion

        #region Enumeration

        public IEnumerator<T> GetEnumerator()
        {
            return InOrder(_root).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private IEnumerable<T> InOrder(Node? node)
        {
            if (node == null) yield break;
            foreach (var v in InOrder(node.Left)) yield return v;
            yield return node.Value;
            foreach (var v in InOrder(node.Right)) yield return v;
        }

        #endregion
    }
}
