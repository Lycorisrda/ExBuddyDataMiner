// ReSharper disable InconsistentNaming
namespace ExBuddyDataMiner
{
	using SQLite;

	public class Database : SQLiteConnection
	{
		public Database(string path, bool createTables = false)
			: base(path)
		{
			if (createTables)
			{
				CreateTable<MasterpieceSupplyDutyResult>();
				CreateTable<RequiredItemResult>();
			}
		}
	}
}
