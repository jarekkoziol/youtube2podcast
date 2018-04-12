using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Youtube2Podcast.Functions
{
    static class Extensions
    {
        public static async Task AddMessageAsJsonAsync<T>(this CloudQueue cloudQueue, T objectToAdd,            
            TimeSpan? timeToLive = null)
        {
            var messageAsJson = JsonConvert.SerializeObject(objectToAdd);
            var cloudQueueMessage = new CloudQueueMessage(messageAsJson);
            
            await cloudQueue.AddMessageAsync(cloudQueueMessage,
                timeToLive:timeToLive,
                initialVisibilityDelay: null,
                options: new QueueRequestOptions(),
                operationContext: new OperationContext());
        }
    }
}
