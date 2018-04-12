using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Google.Apis.YouTube.v3.Data;
using System.Threading.Tasks;

namespace Youtube2Podcast.Functions
{
    class YoutubeApi
    {
        public async Task<IEnumerable<SearchResult>> SearchAsync(string channelId)
        {
            var apiKey = Environment.GetEnvironmentVariable("YoutubeApiKey");
            
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = apiKey,
                ApplicationName = "AzureFunction"
            });

            var searchListRequest = youtubeService.Search.List("snippet");
            searchListRequest.ChannelId = channelId;
            searchListRequest.MaxResults = 50;
            searchListRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
            
            var searchListResponse = await searchListRequest.ExecuteAsync();
            return searchListResponse.Items.Where(i => i.Id.Kind == "youtube#video");           
        }
    }
}
