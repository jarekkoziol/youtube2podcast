using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Google.Apis.YouTube.v3.Data;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Youtube2Podcast.Functions
{
    public class FetchNewVideos
    {
        [FunctionName("FetchNewVideos")]
        public static async Task Run(
            [TimerTrigger("0 */5 * * * *", RunOnStartup = true)]TimerInfo myTimer, 
            TraceWriter log)
        {
            try
            {
                var instance = new FetchNewVideos(log);
                await instance.ExecuteAsync(GetChannels());
            }
            catch(Exception ex)
            {
                log.Error("Exception in function", ex);
            }
            
        }

        private TraceWriter _log;
        private YoutubeApi _youtubeApi;
        private FetchNewVideos(TraceWriter log)
        {
            _log = log;
            _youtubeApi = GetYoutubeApi();
        }        

        private async Task ExecuteAsync(List<string> channels)
        {            
            var table = await GetTableReference();
            var queue = await GetQueue();

            var tasks = channels.Select(c => ProcessChannelAsync(table, queue, c)).ToArray();
            Task.WaitAll(tasks);

            _log.Info($"FetchNewVideos function executed at: {DateTime.Now}");
        }


        private static List<string> GetChannels()
        {            
            return new List<string>() { "UCNXIUnJCtcKjkwm3OhP3Iqw", "UCLm7DMKc2OAPTihoGQwCWmw" };
        }       

        private YoutubeApi GetYoutubeApi()
        {
            return new YoutubeApi();
        }

        private async Task<CloudTable> GetTableReference()
        {
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

            // Create the table client.
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            // Retrieve a reference to the table.
            CloudTable table = tableClient.GetTableReference("Movies");

            // Create the table if it doesn't exist.
            await table.CreateIfNotExistsAsync();
            return table;
        }

        private static async Task<CloudQueue> GetQueue()
        {
            var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            
            var queueClient = storageAccount.CreateCloudQueueClient();

            var queue = queueClient.GetQueueReference("movies-queue");
            await queue.CreateIfNotExistsAsync();

            return queue;
        }

        private async Task ProcessChannelAsync(CloudTable moviesTable, CloudQueue queue, string channelId)
        {
            _log.Info("Processing channel " + channelId);

            var moviesFromApi = await _youtubeApi.SearchAsync(channelId);
            
            var savedMoviesIds = await GetAlreadySavedMovies(moviesTable, channelId);

            var newMovies = moviesFromApi.Where(m => !savedMoviesIds.Contains(m.Id.VideoId)).ToList();

            _log.Info("Found new moives: " + newMovies.Count);

            await SaveMovies(newMovies, channelId, moviesTable);

            await SendMoviesToQueue(newMovies, channelId, queue);
        }

        private Task SendMoviesToQueue(List<SearchResult> newMovies, string channelId, CloudQueue queue)
        {
            var tasks = newMovies.Select(x => queue.AddMessageAsJsonAsync<MovieMessage>(new MovieMessage()
            {
                ChannelId = channelId,
                VideoId = x.Id.VideoId
            }, TimeSpan.FromHours(6))).ToArray();

            Task.WaitAll(tasks);

            if (tasks.Any())
            {
                _log.Info("Sendings new movies to queue succed");
            }

            return Task.CompletedTask;
        }

        private async Task SaveMovies(IEnumerable<SearchResult> movies, string channelId, CloudTable table)
        {
            var operation = new TableBatchOperation();

            foreach (var movie in movies)
            {
                _log.Info("Saving new movie: " + movie.Snippet.Title);

                operation.Insert(new MovieRow()
                {
                    PartitionKey = GetPartitionKey(channelId),
                    RowKey = movie.Id.VideoId,
                    Text = movie.Snippet.Title
                });
            }

            if (operation.Any())
            {
                await table.ExecuteBatchAsync(operation);
                _log.Info("Saving new movies sucessed");
            }
        }

        private async Task<List<string>> GetAlreadySavedMovies(CloudTable moviesTable, string channelId)
        {
            var query = new TableQuery<DynamicTableEntity>()
                            .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, GetPartitionKey(channelId)))
                            .Select(new List<string>() { "RowKey" });

            var result = new List<string>();
            var tableContinuationToken = new TableContinuationToken();
            TableContinuationToken token = null;
            do
            {
                TableQuerySegment<DynamicTableEntity> resultSegment = await moviesTable.ExecuteQuerySegmentedAsync(query, token);
                token = resultSegment.ContinuationToken;

                foreach (DynamicTableEntity entity in resultSegment.Results)
                {
                    result.Add(entity.RowKey);
                }
            } while (token != null);

            return result;
        }

        private static string GetPartitionKey(string channelId)
        {
            return $"MoviesForChannel_{channelId}";
        }
    }
}
