﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Infrastructure
{
    public class MongoDbConnection : IDbConnection
    {
        private static readonly IMongoDatabase MongoDatabase;
        private readonly IModelFactory _modelFactory;

        static MongoDbConnection()
        {
            var connectionString = "mongodb://localhost:27017";
            var mongoClient = new MongoClient(connectionString);
            mongoClient.DropDatabase("TriviaDB", CancellationToken.None);
            MongoDatabase = mongoClient.GetDatabase("TriviaDB");
        }

        public MongoDbConnection(IModelFactory modelFactory)
        {
            _modelFactory = modelFactory;
            Initialize();
        }

        public async Task Initialize()
        {
            var emptyFilter = new BsonDocument();
            var players = await GetAllAsync<Player>(emptyFilter);
            if (!players.Any())
            {
                players = _modelFactory.CreatePlayers(100);
                await SaveAsync(players);
            }

            var categories = await GetAllAsync<Category>(emptyFilter);
            if (!categories.Any())
            {
                categories = _modelFactory.CreateCategories(10);
                await SaveAsync(categories);
            }

            foreach (var category in categories)
            {
                var categoryFilter = new BsonDocument(nameof(Question.Category), category.Name);
                var categoryQuestions = await GetAllAsync<Question>(categoryFilter);
                if (!categoryQuestions.Any())
                {
                    var questionIds = new List<ObjectId>();
                    for (int i = 0; i < 50; i++)
                    {
                        var newQuestionWithAnswers = _modelFactory.CreateQuestionWithAnswers(i, category.Name);
                        await SaveAsync(newQuestionWithAnswers.answers);
                        newQuestionWithAnswers.question.Answers = newQuestionWithAnswers.answers.Select(answer => answer.Id);
                        await SaveAsync(newQuestionWithAnswers.question);
                        questionIds.Add(newQuestionWithAnswers.question.Id);
                    }

                    category.Questions = questionIds;
                    await UpdateAsync(category);
                }
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>(BsonDocument filter) where T : IModel
        {
            var collection = MongoDatabase.GetCollection<T>(typeof(T).ToString());
            return await collection.Find(filter).ToListAsync();
        }

        public async Task<T> GetAsync<T>(ObjectId id) where T : IModel
        {
            var filter = new BsonDocument("_id", id);
            return await GetAsync<T>(filter);
        }

        public async Task<T> GetAsync<T>(BsonDocument filter) where T : IModel
        {
            var items = await GetAllAsync<T>(filter);
            return items.FirstOrDefault();
        }

        public async Task SaveAsync<T>(T item) where T : IModel
        {
            var collection = MongoDatabase.GetCollection<T>(item.GetType().ToString());
            await collection.InsertOneAsync(item);
        }

        public async Task SaveAsync<T>(IEnumerable<T> items) where T : IModel
        {
            var collection = MongoDatabase.GetCollection<T>(items.First().GetType().ToString());
            await collection.InsertManyAsync(items);
        }

        public async Task UpdateAsync<T>(T item) where T : IModel
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", item.Id);
            var items = MongoDatabase.GetCollection<BsonDocument>(item.ToString());
            await items.ReplaceOneAsync(filter, item.ToBsonDocument());
        }

        public async Task RemoveAsync<T>(BsonDocument filter) where T : IModel
        {
            var collection = MongoDatabase.GetCollection<BsonDocument>(typeof(T).ToString());
            await collection.DeleteOneAsync(new BsonDocumentFilterDefinition<BsonDocument>(filter));
        }

    }
}
