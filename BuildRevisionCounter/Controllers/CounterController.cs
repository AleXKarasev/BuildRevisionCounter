﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using BuildRevisionCounter.Interfaces;
using BuildRevisionCounter.Model;
using BuildRevisionCounter.Security;
using MongoDB.Driver;

namespace BuildRevisionCounter.Controllers
{
	[RoutePrefix("api/counter")]
	[BasicAuthentication]
	public class CounterController : ApiController
	{
		private readonly IMongoDBStorage _mongoDbStorage;

		/// <summary>
		/// Конструктор контроллера номеров ревизий.
		/// </summary>
		/// <param name="mongoDbStorage">Объект для получения данных из БД Монго.</param>
		public CounterController(IMongoDBStorage mongoDbStorage)
		{
			if (mongoDbStorage == null)
				throw new ArgumentNullException("mongoDbStorage");

			_mongoDbStorage = mongoDbStorage;
		}

        [HttpGet]
        [Route("")]
        [Authorize(Roles = "admin, editor, anonymous")]
        public async Task<IReadOnlyCollection<RevisionModel>> GetAllRevision()
        {
            var revisions = await _mongoDbStorage.Revisions
                .Find(r => true)
                .ToListAsync();

            if (revisions == null)
                throw new HttpResponseException(HttpStatusCode.NotFound);

            return revisions;
        }

		[HttpGet]
		[Route("{revisionName}")]
		[Authorize(Roles = "admin, editor, anonymous")]
		public async Task<long> Current([FromUri] string revisionName)
		{
			var revision = await _mongoDbStorage.Revisions
				.Find(r => r.Id == revisionName)
				.SingleOrDefaultAsync();

			if (revision == null)
				throw new HttpResponseException(HttpStatusCode.NotFound);

			return revision.NextNumber;
		}

		[HttpPost]
		[Route("{revisionName}")]
		[Authorize(Roles = "buildserver")]
		public async Task<long> Bumping([FromUri] string revisionName)
		{
            var existRevision = await _mongoDbStorage.Revisions
                .CountAsync(r => r.Id == revisionName);
		    if (existRevision == 0)
		    {
		        await _mongoDbStorage.Revisions
		            .InsertOneAsync(new RevisionModel
		            {
                        Id = revisionName,
		                NextNumber = 0,
		                Created = DateTime.UtcNow
		            });
		        return 0;
		    }
		    else
		    {
                var result = await _mongoDbStorage.Revisions
                .FindOneAndUpdateAsync<RevisionModel>(
                    r => r.Id == revisionName,
                    Builders<RevisionModel>.Update
                        .Inc(r => r.NextNumber, 1)
                        .Set(r => r.Updated, DateTime.UtcNow),
                    new FindOneAndUpdateOptions<RevisionModel>
                    {
                        IsUpsert = true,
                        ReturnDocument = ReturnDocument.After
                    });


                return result.NextNumber;
            }
		}
	}
}