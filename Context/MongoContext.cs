using MongoDB.Bson;
using MongoDB.Driver;

public class MongoContext
{
    public static IMongoDatabase _mongo;
    //IMongoCollection<TDocument>

    public MongoContext()
    {
        var client = new MongoClient(worker.BGTestWorker.settings.MongoConnectionString);
        _mongo = client.GetDatabase(worker.BGTestWorker.settings.MongoDB);
        //_mongo.DropCollection(worker.BGTestWorker.settings.MongoDB);

    }

    public IMongoCollection<T> getCollection<T>(string collectionName)
    {
        IMongoCollection<T> _collection = _mongo.GetCollection<T>(collectionName);
        return _collection;
    }

    public IEnumerable<T> find<T>(string collectionName)
    {
        IMongoCollection<T> _collection = this.getCollection<T>(collectionName);

        var all = _collection.Find(new BsonDocument());

        return all.ToEnumerable();
    }

    public void insertOne<T>(string collectionName, T data)
    {
        IMongoCollection<T> _collection = this.getCollection<T>(collectionName);
        _collection.InsertOne(data);
    }

    public FilterDefinitionBuilder<T> getFilterBuilder<T>()
    {
        var filter = Builders<T>.Filter;
        return filter;
    }

    public T findOne<T>(string collectionName, FilterDefinition<T> filterDefinition)
    {
        IMongoCollection<T> _collection = this.getCollection<T>(collectionName);
        var item = _collection.Find(filterDefinition).Limit(1).Single();
        return item;
    }

    public IEnumerable<T> findWhere<T>(string collectionName, FilterDefinition<T> filterDefinition)
    {
        IMongoCollection<T> _collection = this.getCollection<T>(collectionName);
        var _all = _collection.Find(filterDefinition);

        return _all.ToEnumerable();

    }

    //var _filter = this.getFilterBuilder<DTODNB>();

}