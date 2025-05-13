using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;

using Azure.AI.Projects;
using Azure.Identity;

using ModelContextProtocol.Client;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Protocol.Transport;


#region Load LLM
var connectionString = System.Environment.GetEnvironmentVariable("AZURE_AI_CONNECTION_STRING");
var chatModelId = System.Environment.GetEnvironmentVariable("AZURE_AI_CHAT_MODEL_ID");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("Please set the AZURE_AI_CONNECTION_STRING environment variable.");
    return;
}

if (string.IsNullOrEmpty(chatModelId))
{
    Console.WriteLine("Please set the AZURE_AI_CHAT_MODEL_ID environment variable.");
    return;
}
#endregion

#region Configure the AI Project Client
Console.WriteLine("Configuring AI Project Client...");
AIProjectClient client;
try
{
#pragma warning disable SKEXP0110
    client = AzureAIAgent.CreateAzureAIClient(connectionString, new AzureCliCredential());
#pragma warning restore SKEXP0110
    Console.WriteLine("AI Project Client created successfully");
}
catch (Exception ex)
{
    Console.WriteLine($"Error creating AIProjectClient: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    return;
}

AgentsClient agentsClient = client.GetAgentsClient();
#endregion

#region Get LLM services available to the Agent
Console.WriteLine("Fetching connections to LLM services available to the Agent...");
var connectionsResponse = await client.GetConnectionsClient().GetConnectionsAsync();
var connectionsList = connectionsResponse.Value.Value.ToList();
if (connectionsList.Count == 0)
{
    Console.WriteLine("No connections found.");
    return;
}
for (int i = 0; i < connectionsList.Count; i++)
{
    Console.WriteLine($"Connection {i + 1}:");
    Console.WriteLine($"  ✓ Name = {connectionsList[i].Name}");
    Console.WriteLine($"  ✓ Category = {connectionsList[i].Properties.Category}");
}
#endregion

#region Build SK 'kernel' object
Console.WriteLine("Building Semantic Kernel...");
// Prepare and build kernel with enhanced logging
var builder = Kernel.CreateBuilder();
builder.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace));

Kernel kernel = builder.Build();
Console.WriteLine("Kernel built successfully");
#endregion

#region Create MCP client for Icon Tools
// Create an MCPClient for the Logo MCP server
var mpcServerNameLogoTools = "LogoImageValidationTools";
await using IMcpClient mcpLogoToolsClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
{
    Name = mpcServerNameLogoTools,
    Command = "/usr/local/share/dotnet/dotnet",
    Arguments = ["run", "--project", "/Users/billdev/repos/devpartners/PROJECTS/crankingai/logo/logo-validator-mcp"],
}));

// Show all the available tools
var logoTools = await mcpLogoToolsClient.ListToolsAsync();
Console.WriteLine($"Available {mpcServerNameLogoTools} MCP server tools:");
foreach (var tool in logoTools)
{
    Console.WriteLine($"  ✓ {tool.Name}");
}
#endregion

#if true
#region Create MCP client for Web Search
// Create an MCPClient for the Web Search MCP server
var mpcServerNameWebSearchTools = "WebSearch";
var braveApiKey = System.Environment.GetEnvironmentVariable("BRAVE_API_KEY");
if (string.IsNullOrEmpty(braveApiKey))
{
    Console.WriteLine("Please set the BRAVE_API_KEY environment variable.");
    return;
}

await using IMcpClient mcpSearchClient = await McpClientFactory.CreateAsync(new StdioClientTransport(new()
{
    Name = mpcServerNameWebSearchTools,
    Command = "/usr/local/share/dotnet/dotnet",
    Arguments = ["run", "--project", "/Users/billdev/repos/devpartners/PROJECTS/crankingai/logo/brave-search-mcp"],
    EnvironmentVariables = new Dictionary<string, string>
    {
        { "BRAVE_API_KEY", braveApiKey }
    }
}));

// Show all the available tools
var webSearchTools = await mcpSearchClient.ListToolsAsync();
Console.WriteLine($"Available {mpcServerNameWebSearchTools} MCP server tools:");
foreach (var tool in webSearchTools)
{
    Console.WriteLine($"  ✓ {tool.Name}");
}
#endregion
#endif


#region Create the Logo Agent
Console.WriteLine($"Creating {mpcServerNameLogoTools} Agent...");

// Register tools with the kernel
#pragma warning disable SKEXP0001
kernel.Plugins.AddFromFunctions($"{mpcServerNameLogoTools}", logoTools.Select(aiFunction => aiFunction.AsKernelFunction()));
kernel.Plugins.AddFromFunctions($"{mpcServerNameWebSearchTools}", webSearchTools.Select(aiFunction => aiFunction.AsKernelFunction()));
#pragma warning restore SKEXP0001

// Create agent with clearer instructions
Azure.AI.Projects.Agent definition = await agentsClient.CreateAgentAsync(
    chatModelId,
    // name: $"{mpcServerNameLogoTools} Finder Agent",
    // name: $"Logo Image Finder Agent",
    name: $"Diagnostice Agent",
    // description: "Provides Accessible HTML referencing the logo for a technology brand, project, product, or property.",
    description: "You are a diagnostic agent that simply lists all the tools you have been given access to and describe what they do.",
    instructions:
    #if true
    @"You are an agent that provides Accessible HTML referencing the logo for a brand, project product, or property.

A logo for a brand, project, product, or property is usually an image in PNG or JPG format and is openly available on the web.

Given a brand, project, product, or property name repository like 'Python' or 'Semantic Kernel' or 'Java' do the following:

1. Use available tools to search the web, find the URL to a logo for the technology brand, project, product, or property.
2. Use available tools to validate the URL points to a valid logo image.
3. Give as a response the logo image URL, a description of the logo, and a mention of the technology brand, project, product, or property formatted in accessible HTML.

If you cannot find a logo at first, keep trying, but after 10 failed attempts return an error message and mention how many logos were considered.

Always use the available tools for web search and image/logo validation."
#else
@"List all the tools you have been given access to and describe what they do."
#endif
);

// Create the agent with the tools
#pragma warning disable SKEXP0110
AzureAIAgent agent = new(definition, agentsClient);
#pragma warning restore SKEXP0110
Console.WriteLine($"Agent {mpcServerNameLogoTools} created.");
#endregion

// Create thread
#pragma warning disable SKEXP0110
Microsoft.SemanticKernel.Agents.AgentThread agentThread = new AzureAIAgentThread(agent.Client);
#pragma warning restore SKEXP0110


var brandName = // from command line args
    args.Length > 0 ? args[0] : "Python";

Console.WriteLine($"Seeking logo for '{brandName}'...");
Console.WriteLine($"__________________________________________________");

#if false
ChatMessageContent message = new(AuthorRole.User,
    $"Please get the HTML-formatted (but Accessible) reference to the logo for " +
    $" ='{brandName}'");
#else
ChatMessageContent message = new(AuthorRole.User,
    $"Technology project for which we seek a logo is '{brandName}'");
#endif

#if false
var executionSettings = new OpenAIPromptExecutionSettings
{
    MaxTokens = 1,
    Temperature = 1.99f,
    TopP = 0.99f,
};

// Configure agent invocation options
AgentInvokeOptions agentInvokeOptions = new()
{
    KernelArguments = new KernelArguments(executionSettings)
};
#endif

try
{
    await foreach (ChatMessageContent response in agent.InvokeAsync(message, agentThread))
    // , options: agentInvokeOptions))
    {
        Console.WriteLine(response.Content);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error occurred while invoking agent: {ex.Message}");
}
finally
{
    await agentThread.DeleteAsync();
#pragma warning disable SKEXP0110
    await agent.Client.DeleteAgentAsync(agent.Id);
#pragma warning restore SKEXP0110
}

Console.WriteLine($"__________________________________________________");
Console.WriteLine("Adios from Logo Agent! My work here is done.");
