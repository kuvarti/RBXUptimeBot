using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RBXUptimeBot.Models;
using RBXUptimeBot.Classes;
using MongoDB.Bson;

namespace RBXUptimeBot.Classes.Services
{
	public interface IMongoDbService<T> where T : IEntity
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
				Logger.Critical($"Collection name for type {typeof(T).Name} is not configured.");
			}
			else
				_collection = _collectionsCache.GetOrAdd(collectionName, name => database.GetCollection<T>(name));
			try
			{
				database.RunCommandAsync((Command<BsonDocument>)"{ping:1}").GetAwaiter().GetResult();
			}
			catch
			{
				Logger.Critical($"{collectionName} is not initialized");
				_collection = null;
			}
		}

		private string? GetCollectionName(string typeName, Dictionary<string, string> collections)
		{
			if (collections.TryGetValue(typeName, out var collectionName))
			{
				return collectionName;
			}
			return null;
		}

		public async Task<List<T>> GetAllAsync()
		{
			if (_collection != null)
				return await _collection.Find(_ => true).ToListAsync();
			else
				return null;
		}

		public async Task<T?> GetByIdAsync(string id)
		{
			if (_collection != null)
				return await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
			else
				return default(T);
		}

		// TODO may create random id string
		public async Task CreateAsync(T entity)
		{
			if (_collection != null)
				await _collection.InsertOneAsync(entity);
		}

		public async Task UpdateAsync(string id, T entity)
		{
			entity.Id = id;
			if (_collection != null)
				await _collection.ReplaceOneAsync(x => x.Id == id, entity);
		}

		public async Task DeleteAsync(string id)
		{
			if (_collection != null)
				await _collection.DeleteOneAsync(x => x.Id == id);
		}

		public bool IsConnected() => _collection != null;
	}
}
