using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace Youtube2Podcast.Functions
{
    class MovieRow : TableEntity
    {
        public MovieRow()
        {
        }
        
        public string Text { get; set; }
    }
}
