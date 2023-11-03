using Google.Protobuf.WellKnownTypes;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace NonStreamTokenUsageFunction.Model
{
    public class OpenAiToken
    {
        public string Timestamp { get; set; }
        
        public string AppKey { get; set; }
        public string ApiOperation { get; set; }

        public string PromptTokens { get; set; }

        public string CompletionTokens { get; set; }

        public string TotalTokens { get; set; }



        public Dictionary<string, string> ToDictionary()
        {
            var dict = new Dictionary<string, string> 
            {
                { "Timestamp", Timestamp },
                { "AppKey", AppKey },
                { "ApiOperation", ApiOperation},  
                { "Stream", "False" },
                { "PromptTokens", PromptTokens },
                { "CompletionTokens", CompletionTokens },
                { "TotalTokens", TotalTokens },
            };
            return dict;
        }
    }
}
