using RBXUptimeBot.Classes;

namespace RBXUptimeBot.Classes
{
	public static class ListExtensions
	{
		public static void AddOrChange<T>(this List<T> list, T item) where T : ActiveItem
		{
			ActiveItem existingItem = list.Find(x => x.Account == item.Account);

			if (existingItem == null) list.Add(item);
			else
			{
				existingItem.PID = item.PID;
				existingItem.StartTime = item.StartTime;
			}
		}
	}
}
