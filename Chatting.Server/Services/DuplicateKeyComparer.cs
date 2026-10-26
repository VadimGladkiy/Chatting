namespace Chatting.Server.Services
{
    public class DuplicateKeyComparer
    {
        /// <summary>
        /// Create a comparer for KeyValuePair{T,U} that compares by the Key first.
        /// When keys compare equal, an optional tieBreaker selector is used to provide
        /// a secondary IComparable (for example, the Comment.Id) to produce a deterministic ordering.
        /// If no tieBreaker is provided, equal keys are treated as equal (return 0).
        /// </summary>
        public static Comparer<KeyValuePair<T, U>> CreateKeyValueComparer<T, U>(Func<U, IComparable?>? tieBreaker = null)
            where T : IComparable
        {
            return Comparer<KeyValuePair<T, U>>.Create((x, y) =>
            {
                int keyCmp = Comparer<T>.Default.Compare(x.Key, y.Key);
                if (keyCmp != 0) return keyCmp;

                if (tieBreaker != null)
                {
                    var a = tieBreaker(x.Value);
                    var b = tieBreaker(y.Value);
                    if (a == null && b == null) return 0;
                    if (a == null) return -1;
                    if (b == null) return 1;
                    return a.CompareTo(b);
                }

                return 0;
            });
        }
    }
}
