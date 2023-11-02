
# Token Usage Azure Open AI Requests

  

## Reference Architecture

![img](/assets/architecture.png)

  
## Prerequisites


### Azure OpenAI

- Create Azure OpenAI instance in your preferred region. Once provisioned, create a deployment with the model of choice: [Create and deploy an Azure OpenAI Service resource](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/create-resource?pivots=web-portal)

### API Management 
1. Create Azure APIM Instance [Create an Azure API Management service instance by using the Azure portal](https://learn.microsoft.com/en-us/azure/api-management/get-started-create-service-instance). 

2. Import Azure OpenAI swagger spec in APIM. [Import API](https://learn.microsoft.com/en-us/azure/api-management/import-and-publish#go-to-your-api-management-instance)  

   In APIM - APIs blade, select Add API and add Azure OpenAI swagger spec. This sample uses the latest stable swagger spec : [Azure OpenAI 2023-05-15 Swagger Spec](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/cognitiveservices/data-plane/AzureOpenAI/inference/stable/2023-05-15/inference.json)

2. In APIM - Backends blade, add a new backend and provide the endpoint of the OpenAI service created
![img](/assets/backend.png)

### Event Hub
Create an Event Hub Namespace resource and Event Hub. [Create an event hub using Azure portal](https://learn.microsoft.com/en-us/azure/event-hubs/event-hubs-create)

### Managed Identities
1. It is recommended to use a managed identity to authenticate the API Management resource to the Event Hub. You can create a User-assigned Managed Identity in the [Azure Portal using these instructions](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/how-manage-user-assigned-managed-identities?pivots=identity-mi-methods-azp).  

2. Note the client id of the user-assigned managed identity. It will be required for following steps: 
	 
	1. Add the User-Assigned Managed Identity to the Azure API Management Resource: [Assign User Assigned Managed Identity to APIM](https://learn.microsoft.com/en-us/azure/api-management/api-management-howto-use-managed-service-identity#create-a-user-assigned-managed-identity).
	2. Assign the user-assigned managed identity you created in the earlier step to the [Azure Event Hubs Data Sender Azure RBAC role](https://learn.microsoft.com/en-us/azure/api-management/api-management-howto-log-event-hubs?tabs=PowerShell#option-2-configure-api-management-managed-identity).
	3. Assign the user-assigned managed identity you created in the earlier step to the [Cognitive Services OpenAI User](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/managed-identity).

  ## Create API Management Logger 

1. Create a [API Management Logger with user assigned managed identity](https://learn.microsoft.com/en-us/azure/api-management/api-management-howto-log-event-hubs?tabs=PowerShell#logger-with-user-assigned-managed-identity-credentials)
2. **Note** the name assigned to the APIM logger 
  

### **Create the custom API Management Policy**

2. Copy and paste the custom Azure API Management Policy [provided in this repository](../token-usage-non-streaming-request/apim-policy/apim-policy-event-hub-logging.xml). You must modify the variables in the comment section of the policy with the values that match your implementation. The policy will create two events, one for the request and one for the response. The events are correlated with the message-id property which a unique GUID generated for each message to the API.

### Create APIM Product and Subscription 

1. Add a Product in APIM for the Azure OpenAI API Created above [Create and publish an APIM product](https://learn.microsoft.com/en-us/azure/api-management/api-management-howto-add-products?tabs=azure-portal)

2. Add Subscription for the Product for each client requesting access to Azure OpenAI API [Create subscriptions in Azure API Management](https://docs.microsoft.com/en-us/azure/api-management/api-management-howto-create-subscriptions?tabs=azure-portal)

Subscription Key will help in identifying requests and response from each client for token calculation and chargebacks. It can also be used for rate limiting and throttling.
 

## Test Azure OpenAI APIs

Test Azure OpenAI APis from Azure APIM Testing Portal or Postman, or from your client Apps

![img](/assets/test-api.png) 

Curl commands to test the Azure OpenAI APIs are are provided in this repository: [test-scripts](/test-scripts/) 
  
## EventHub Triggered Azure Function to process APIM Logs and log Token Usage Information in Application Insights

A Sample Azure Function in .Net is provided in this repository that extracts and calculates tokens count for every open AI Request and Response for every Client App and sends it to AppInsights Telemetry: [OpenAI Token Usage Azure Function](../assets/azure-function-python.py)

> Create an Event Hub Triggered Azure Function in your preferred
> language. [Create Azure
> Function](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-function-app-portal#create-a-function-app)

  
## Azure Monitor queries to fetch tokens usage metrics

  - Once the Chargeback functionapp calculates the prompt and completion tokens per OpenAI request, it logs the information to Azure Log Analytics.
- All custom logs from function app is logged to a table called `customEvents`
- Example query to identify token usage by a specific client key:

```kusto

customEvents

| where name contains "Azure OpenAI Tokens"

| extend tokenData = parse_json(customDimensions)

| where tokenData.AppKey contains "your-client-key"

| project

Timestamp = tokenData.Timestamp,

Stream = tokenData.Stream,

ApiOperation = tokenData.ApiOperation,

PromptTokens = tokenData.PromptTokens,

CompletionTokens = tokenData.CompletionTokens,

TotalTokens = tokenData.TotalTokens

```
- Example query to fetch aggregated token usage for all consumers

```kusto

customEvents

| where name contains "Azure OpenAI Tokens"

| extend tokenData = parse_json(customDimensions)

| extend

AppKey = tokenData.AppKey,

PromptTokens = tokenData.PromptTokens,.xml

CompletionTokens = tokenData.CompletionTokens,

TotalTokens = tokenData.TotalTokens

| summarize PromptTokens = sum(toint(PromptTokens)) , CompletionTokens = sum(toint(CompletionTokens)), TotalTokens = sum(toint(TotalTokens)) by tostring(AppKey)

| project strcat(substring(tostring(AppKey),0,8), "XXXX"), PromptTokens, CompletionTokens, TotalTokens

```

The queries can be visualized using Azure Dashboard

![azuredashboard](assets/azure-dashboard.png)

