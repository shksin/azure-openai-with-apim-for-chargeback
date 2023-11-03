using System;
using System.Collections.Generic;
using Azure.Messaging.EventHubs;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using NonStreamTokenUsageFunction.Model;

namespace EventHubTriggerFunction
{
    public class EventHubTriggerFunction
    {
        private readonly ILogger _logger;
        private TelemetryClient _telemetryClient;


        public EventHubTriggerFunction(TelemetryClient telemetryClient, ILoggerFactory loggerFactory)
        {
            _telemetryClient = telemetryClient;
            _logger = loggerFactory.CreateLogger<EventHubTriggerFunction>();

        }

        [Function("TokenUsageFunction")]
        public async Task Run([EventHubTrigger("%EventHubName%", Connection = "EventHubConnection")] string[] openAiTokenResponse)
        {
            
            //Eventhub Messages arrive as an array            
            foreach (var tokenData in openAiTokenResponse)
            {
                try
                {
                    _logger.LogInformation($"Azure OpenAI Tokens Data Received: {tokenData}");
                    var OpenAiToken = JsonSerializer.Deserialize<OpenAiToken>(tokenData);

                    if (OpenAiToken == null)
                    {
                        _logger.LogError($"Invalid OpenAi Api Token Response Received. Skipping.");
                        continue;
                    }                                    

                    _telemetryClient.TrackEvent("Azure OpenAI Tokens", OpenAiToken.ToDictionary());
                }
                catch (Exception e)
                {
                    _logger.LogError($"Error occured when processing TokenData: {tokenData}", e.Message);
                }
            }

        }
    }
}
