using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RBXUptimeBot.Models;

namespace RBXUptimeBot.Classes.Services
{
	class MyData
	{
		public int a { get; set; }
	};
	public interface IMongoDbService
	{
		Task<List<MyData>> GetAllAsync();
		Task<MyData?> GetByIdAsync(string id);
		Task CreateAsync(MyData data);
		Task UpdateAsync(string id, MyData data);
		Task DeleteAsync(string id);
	}

	public class MongoDbService : IMongoDbService
	{
		private readonly IMongoCollection<MyData> _collection;

		public MongoDbService(IOptions<MongoDBSettings> mongoDbSettings)
		{
			var mongoClient = new MongoClient(
				mongoDbSettings.Value.ConnectionString);

			var mongoDatabase = mongoClient.GetDatabase(
				mongoDbSettings.Value.DatabaseName);

			_collection = mongoDatabase.GetCollection<MyData>(
				mongoDbSettings.Value.CollectionName);
		}

		public async Task<List<MyData>> GetAllAsync() =>
			await _collection.Find(_ => true).ToListAsync();

		public async Task<MyData?> GetByIdAsync(string id) =>
			await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

		public async Task CreateAsync(MyData data) =>
			await _collection.InsertOneAsync(data);

		public async Task UpdateAsync(string id, MyData data) =>
			await _collection.ReplaceOneAsync(x => x.Id == id, data);

		public async Task DeleteAsync(string id) =>
			await _collection.DeleteOneAsync(x => x.Id == id);
	}
}
