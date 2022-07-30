using MongoDB.Bson;
using MongoDB.Driver;

public class MongoContext<T>
{
    public static IMongoDatabase _mongo;
    //IMongoCollection<TDocument>

    public MongoContext()
    {
        var client = new MongoClient(worker.BGTestWorker.settings.MongoConnectionString);
        _mongo = client.GetDatabase(worker.BGTestWorker.settings.MongoDB);
    }

    public IMongoCollection<T> getCollection(string collectionName)
    {
        IMongoCollection<T> _collection = _mongo.GetCollection<T>(collectionName);
        return _collection;
    }

    public IMongoCollection<E> getCollection<E>(string collectionName)
    {
        IMongoCollection<E> _collection = _mongo.GetCollection<E>(collectionName);
        return _collection;
    }

    public IEnumerable<T> find(string collectionName)
    {
        IMongoCollection<T> _collection = this.getCollection<T>(collectionName);

        var all = _collection.Find(new BsonDocument());

        return all.ToEnumerable();
    }

    public void insertOne(string collectionName, T data)
    {
        IMongoCollection<T> _collection = this.getCollection<T>(collectionName);
        _collection.InsertOne(data);
    }

    public void insertOne<E>(string collectionName, E data)
    {
        IMongoCollection<E> _collection = this.getCollection<E>(collectionName);
        _collection.InsertOne(data);
    }

    public FilterDefinitionBuilder<T> getFilterBuilder()
    {
        var filter = Builders<T>.Filter;
        return filter;
    }

    public FilterDefinitionBuilder<E> getFilterBuilder<E>()
    {
        var filter = Builders<E>.Filter;
        return filter;
    }

    public T findOne(string collectionName, FilterDefinition<T> filterDefinition)
    {
        IMongoCollection<T> _collection = this.getCollection<T>(collectionName);
        var item = _collection.Find(filterDefinition).Limit(1).Single();
        return item;
    }

    public IEnumerable<T> findWhere(string collectionName, FilterDefinition<T> filterDefinition)
    {
        IMongoCollection<T> _collection = this.getCollection<T>(collectionName);
        var _all = _collection.Find(filterDefinition);

        return _all.ToEnumerable();

    }

    public IEnumerable<E> findWhere<E>(string collectionName, FilterDefinition<E> filterDefinition)
    {
        IMongoCollection<E> _collection = this.getCollection<E>(collectionName);
        var _all = _collection.Find(filterDefinition);

        return _all.ToEnumerable();
    }

    public void replaceOne(string collectionName, FilterDefinition<T> filterDefinition, T payload)
    {
        IMongoCollection<T> _collection = this.getCollection<T>(collectionName);
        var item = _collection.ReplaceOne(filterDefinition, payload);  
    }

    public void replaceOne<E>(string collectionName, FilterDefinition<E> filterDefinition, E payload)
    {
        IMongoCollection<E> _collection = this.getCollection<E>(collectionName);
        var item = _collection.ReplaceOne(filterDefinition, payload);  
    }

    //var _filter = this.getFilterBuilder<DTODNB>();

}