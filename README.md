
# Token Usage Azure Open AI Requests

  

## Reference Architecture

![img](/assets/architecture.png)

  
## Prerequisites


### Azure OpenAI

- Create Azure OpenAI instance in your preferred region. Once provisioned, create a deployment with the model of choice: [Create and deploy an Azure OpenAI Service resource](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/create-resource?pivots=web-portal)

### API Management 
1. Create Azure APIM Instance [Create an Azure API Management service instance by using the Azure portal](https://learn.microsoft.com/en-us/azure/api-management/get-started-create-service-instance). 

2. Import Azure OpenAI swagger spec in APIM. [Import API](https://learn.microsoft.com/en-us/azure/api-management/import-and-publish#go-to-your-api-management-instance)  

   In APIM Portal - APIs blade, select Add API and add Azure OpenAI swagger spec. This sample uses the latest stable swagger spec : [Azure OpenAI 2023-05-15 Swagger Spec](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/cognitiveservices/data-plane/AzureOpenAI/inference/stable/2023-05-15/inference.json)

2. In APIM - Backends blade, add a new backend and provide the endpoint of the OpenAI service created
![img](/assets/backend.png)

### Event Hub
Create an Event Hub Namespace resource and Event Hub. [Create an event hub using Azure portal](https://learn.microsoft.com/en-us/azure/event-hubs/event-hubs-create)

### Managed Identities
1. It is recommended to use a managed identity to authenticate the API Management resource to the Event Hub. You can create a User-assigned Managed Identity in the [Azure Portal using these instructions](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/how-manage-user-assigned-managed-identities?pivots=identity-mi-methods-azp).  

    > Save the **ClientID** of the user-assigned managed identity created. It will be required for following steps: 
	 
	1. Add the User-Assigned Managed Identity to the Azure API Management Resource: [Assign User Assigned Managed Identity to APIM](https://learn.microsoft.com/en-us/azure/api-management/api-management-howto-use-managed-service-identity#create-a-user-assigned-managed-identity).
    2. Create a Named value in Azure APIM instance and save the ClientId as a secret. This will be used in the APIM policy for APIs to authenticate with Azure OpenAI Service instead of using OpenAI endpoint key. [Add Named Value in APIM](https://learn.microsoft.com/en-us/azure/api-management/api-management-howto-properties?tabs=azure-portal#add-a-plain-or-secret-value-to-api-management)
    ![img](/assets/namedvalue.png)
	3. Assign the user-assigned managed identity you created in the earlier step to the [Azure Event Hubs Data Sender Azure RBAC role](https://learn.microsoft.com/en-us/azure/api-management/api-management-howto-log-event-hubs?tabs=PowerShell#option-2-configure-api-management-managed-identity).
	4. Assign the user-assigned managed identity you created in the earlier step to the [Cognitive Services OpenAI User](https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/managed-identity).

  ## Create API Management Logger 

Create a [API Management Logger with user assigned managed identity](https://learn.microsoft.com/en-us/azure/api-management/api-management-howto-log-event-hubs?tabs=PowerShell#logger-with-user-assigned-managed-identity-credentials)
> Save the name the **APIM logger** created 
  

### **Create the custom API Management Policy**

1. In APIM Portal - APIs blade, Select the Azure OpenAI Service API that you have created and select All Operations. Update the inbound section of the policy to set your Azure OpenAI service as the backend and authentication mechanism to be the Managed Identity Created above. [Set Backend Service in APIM Policy](https://docs.microsoft.com/en-us/azure/api-management/api-management-howto-policies#set-backend-service)
[Authenticate with managed identity in APIM POlicy](https://learn.microsoft.com/en-us/azure/api-management/authentication-managed-identity-policy). 

A sample policy is [provided in this repository](/token-usage-non-streaming-request/apim-policy/inboundpolicy-alloperations.xml).
> **Note** Replace the name of the backend service and managed clientId in the policy as per your configuration
![img](/assets/inboundpolicy-alloperations.png)

2. Update the policy section of `completions` and `chat completions` APIs outbound policy to send tokens count values from Azure OpenAI API response to eventhub using `log-to-event-hub` policy.
A sample policy is [provided in this repository][provided in this repository](/token-usage-non-streaming-request/apim-policy/logtoeventhubpolicy-nonstream.xml).
> **Note** Replace the name of APIM logger in the policy as per your configuration
![img](/assets/logtoeventhubpolicy-nonstream.png)


### Create APIM Product and Subscription 

1. Add a Product in APIM for the Azure OpenAI API Created above [Create and publish an APIM product](https://learn.microsoft.com/en-us/azure/api-management/api-management-howto-add-products?tabs=azure-portal)

2. Add Subscription for the Product for each client requesting access to Azure OpenAI API [Create subscriptions in Azure API Management](https://docs.microsoft.com/en-us/azure/api-management/api-management-howto-create-subscriptions?tabs=azure-portal)

Subscription Key will help in identifying requests and response from each client for token calculation and chargebacks. It can also be used for rate limiting and throttling.
 

## Test Azure OpenAI APIs

Test Azure OpenAI APis from Azure APIM Testing Portal or Postman, or from your client Apps

![img](/assets/test-api.png) 

> **Note** 
> Curl commands to test the Azure OpenAI APIs are are provided in this repository: [test-scripts](/test-scripts/).
Update the values of APIMServiceName and SubscriptionKey or other variables as needed.
  
## EventHub Triggered Azure Function 
A Sample Azure Function in .Net is provided in this repository that extracts and calculates tokens count for every open AI Request and Response for every Client App and sends it to AppInsights Telemetry. Token Usage data can then be used to calculate chargeback. [Azure Function Sample Code](/token-usage-non-streaming-request/NonStreamTokenUsageFunction)

### **Prerequisite**
An Azure AppInsights instance is needed by the Azure Function to send the token usage data. [Create AppInsights](https://docs.microsoft.com/en-us/azure/azure-monitor/app/create-workspace-resource)

### **Test Azure Function Locally**
To test locally, update the values of `AppInsightsConnectionString`, `EventHubConnectionString` and `EventHubName` in localsetings.json as per your configuration.
> ![img](/assets/azurefunction-localsettings.png)

### **Deploy Azure Function to Azure**
On Azure Portal, create an Azure Function App and deploy the Azure Function. [Create Azure Function using Azure Portal](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-function-app-portal#create-a-function-app).

Once deployed, update the values of the App Settings of the Azure Function with the values of `AppInsightsConnectionString`, `EventHubConnectionString` and `EventHubName` as per your configuration. [Azure Function AppSettings](https://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-use-azure-function-app-settings?tabs=portal)


  
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

