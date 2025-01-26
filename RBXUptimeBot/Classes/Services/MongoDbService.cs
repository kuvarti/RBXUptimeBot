using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RBXUptimeBot.Models;
using RBXUptimeBot.Classes;

namespace RBXUptimeBot.Classes.Services
{
	public interface IMongoDbService<T> where T: IEntity
	{
		Task<List<T>> GetAllAsync();
		Task<T?> GetByIdAsync(string id);
		Task CreateAsync(T data);
		Task UpdateAsync(string id, T data);
		Task DeleteAsync(string id);
		bool IsConnected();
	}

	public class MongoDbService<T> : IMongoDbService<T> where T : IEntity
	{
		private readonly IMongoCollection<T> _collection;
		private static readonly ConcurrentDictionary<string, IMongoCollection<T>> _collectionsCache = new();

		public MongoDbService(IOptions<MongoDBSettings> mongoDbSettings)
		{
			var settings = mongoDbSettings.Value;
			var client = new MongoClient(settings.ConnectionString);
			var database = client.GetDatabase(settings.DatabaseName);

			var collectionName = GetCollectionName(typeof(T).Name, settings.Collections);
			if (collectionName == null)
			{
				//throw new ArgumentException($"Collection name for type {typeof(T).Name} is not configured.");
				Logger.Critical("$Collection name for type {typeof(T).Name} is not configured.");
			}
			else
				_collection = _collectionsCache.GetOrAdd(collectionName, name => database.GetCollection<T>(name));
		}

		private string? GetCollectionName(string typeName, Dictionary<string, string> collections)
		{
			if (collections.TryGetValue(typeName, out var collectionName))
			{
				return collectionName;
			}
			return null;
		}

		public async Task<List<T>> GetAllAsync() =>
			await _collection.Find(_ => true).ToListAsync();

		public async Task<T?> GetByIdAsync(string id) =>
			await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

		// TODO may create random id string
		public async Task CreateAsync(T entity) =>
			await _collection.InsertOneAsync(entity);

		public async Task UpdateAsync(string id, T entity)
		{
			entity.Id = id;
			await _collection.ReplaceOneAsync(x => x.Id == id, entity);
		}

		public async Task DeleteAsync(string id) =>
			await _collection.DeleteOneAsync(x => x.Id == id);

		public bool IsConnected() => _collection != null;
	}
}
