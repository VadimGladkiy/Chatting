namespace Chatting.Server.Services
{
    public class DateTimeDescendingComparer: IComparer<DateTime>
    {
        public int Compare(DateTime x, DateTime y)
        {
            return y.CompareTo(x); // y.CompareTo(x) means descending order
        }
    }
}
