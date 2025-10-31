# GitHub Copilot Instructions for AskAI Project

## Project Overview

AskAI is a multi-agent workflow sample collection for procurement domain using Microsoft Agent Framework. It provides various workflow patterns leveraging Azure OpenAI for expert agent-based inquiry response systems.

### Workflows

1. **DynamicGroupChatWorkflow**: Dynamic selection + HITL (Human-in-the-Loop)
2. **TaskBasedWorkflow**: Task management-based stepwise goal achievement
3. **SelectiveGroupChatWorkflow**: Pre-selection and parallel execution for efficient expert utilization
4. **HandoffWorkflow**: Handoff-based dynamic expert selection
5. **GroupChatWorkflow**: Round-robin all-participant group chat
6. **GraphExecutorWorkflow**: Explicit graph-based workflow using Executor and Edge

## Clean Architecture

### Architecture Layers

This project follows clean architecture principles with the following layers:

```
Workflow Layer (Program.cs)
    ↓ depends on
Agent Layer (ChatClientAgent, Router/Planner, Specialist/Worker, Moderator/Aggregator)
    ↓ depends on
AI Service Layer (IChatClient, Microsoft.Extensions.AI)
    ↓ depends on
Infrastructure Layer (Azure OpenAI, Configuration, Logging, Telemetry)
```

### Key Principles

1. **Separation of Concerns**: Each workflow is implemented as an independent project with clear responsibilities
2. **Single Responsibility**: Each agent handles only one domain of expertise
3. **Dependency Inversion**: Upper layers depend on lower layers, never the reverse
4. **Extensibility**: New workflows and agents can be added with minimal impact

### Agent Types and Responsibilities

- **Router/Planner**: Analyzes questions and selects experts or plans tasks
- **Specialist/Worker**: Provides domain-specific knowledge (Contract, Spend, Negotiation, Sourcing, Knowledge, Supplier)
- **Moderator/Aggregator**: Consolidates multiple opinions
- **HITL**: Human approval process

## Logging and Telemetry

### OpenTelemetry Integration

All workflows implement OpenTelemetry-based observability:

- **Default endpoint**: `http://localhost:4317` (Aspire Dashboard)
- **Environment variable**: `OTEL_EXPORTER_OTLP_ENDPOINT`
- **Application Insights**: `APPLICATIONINSIGHTS_CONNECTION_STRING`

### Log Message Standards

#### Phase Separators

```csharp
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
logger.LogInformation("フェーズ 1: タイトル");
logger.LogInformation("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
```

#### Status Messages

- Success: `✓` or `✅`
- Warning: `⚠️`
- Error: `❌`

#### Structured Logging (ALWAYS use this pattern)

```csharp
// ✅ CORRECT: Structured parameters
logger.LogInformation("タスク完了: {TaskId}, 担当: {AssignedWorker}", taskId, worker);

// ❌ AVOID: String interpolation
logger.LogInformation($"タスク完了: {taskId}, 担当: {worker}");
```

### Activity Tracing with TelemetryHelper

#### StartActivity Pattern

```csharp
using var activity = TelemetryHelper.StartActivity(
    Program.ActivitySource,
    "PhaseOrExecutorName",
    new Dictionary<string, object>
    {
        ["key1"] = value1,
        ["key2"] = value2
    });
```

#### LogWithActivity Pattern

```csharp
// Logs to logger AND adds event to current Activity
TelemetryHelper.LogWithActivity(
    _logger,
    activity,
    LogLevel.Information,
    "Message with {Param1} and {Param2}",
    value1, value2);
```

#### Activity Best Practices

1. **Scope**: Use `using var activity` for automatic cleanup
2. **Naming**: Activity names should be descriptive (e.g., "NegotiationProposalGeneration")
3. **Tags**: Include key context (iteration, risk_score, supplier, etc.)
4. **Events**: Use `LogWithActivity` for important state changes
5. **Hierarchy**: Activities automatically form parent-child relationships

### Efficient Logging Practices

1. **Keep code readable**: Use TelemetryHelper methods to avoid cluttering business logic
2. **Log key state changes**: Initialization, transformation, decision points
3. **Use structured parameters**: Always use `{ParameterName}` syntax
4. **Separate sections**: Use `━━━` separators for major phase boundaries
5. **Add Activity tags**: Include metrics that help with tracing and debugging
6. **Batch related logs**: Group related information under one separator block

## Library Versions

### .NET Framework

- **.NET**: 8.0
- **Language**: C# 12
- **SDK**: .NET 8 SDK or later

### Microsoft Agent Framework

- **Microsoft.Agents.AI.Workflows**: 1.0.0-preview.251009.1

### AI and Machine Learning

- **Microsoft.Extensions.AI**: 9.9.1
- **Microsoft.Extensions.AI.OpenAI**: 9.9.1-\*
- **Azure.AI.OpenAI**: 2.1.0
- **Azure.Identity**: 1.12.0

### Configuration and Logging

- **Microsoft.Extensions.Configuration**: 9.0.0
- **Microsoft.Extensions.Configuration.Json**: 9.0.0
- **Microsoft.Extensions.Configuration.EnvironmentVariables**: 9.0.0
- **Microsoft.Extensions.Logging**: 9.0.0
- **Microsoft.Extensions.Logging.Console**: 9.0.0

### Telemetry (OpenTelemetry)

- **OpenTelemetry**: 1.10.0
- **OpenTelemetry.Exporter.Console**: 1.10.0
- **OpenTelemetry.Exporter.OpenTelemetryProtocol**: 1.10.0
- **OpenTelemetry.Extensions.Hosting**: 1.10.0
- **OpenTelemetry.Instrumentation.Http**: 1.9.0
- **Azure.Monitor.OpenTelemetry.Exporter**: 1.3.0

### Azure OpenAI Models

- **gpt-4o**: High-quality responses for specialist agents and Moderator
- **gpt-4o-mini**: Lightweight processing for Router and classification tasks

## Folder Structure

```
AskAI/
├── README.md                          # Main project documentation
├── AgentWorkflows.sln                 # Solution file
├── docker-compose.yml                 # Docker Compose for Aspire Dashboard
├── .github/
│   └── copilot-instructions.md       # This file
├── docs/                              # Documentation root
│   ├── architecture/                  # Architecture documentation
│   ├── development/                   # Development guides
│   └── workflows/                     # Workflow-specific documentation
└── src/                               # Source code
    ├── Common/                        # Common library
    ├── DynamicGroupChatWorkflow/
    ├── TaskBasedWorkflow/
    ├── SelectiveGroupChatWorkflow/
    ├── GraphExecutorWorkflow/
    ├── HandoffWorkflow/
    └── GroupChatWorkflow/
```

### Project Organization Principles

1. **Workflow Independence**: Each workflow is an independent project
2. **Common Functionality**: Shared components in `Common` project
3. **Documentation Structure**: Organized by purpose under `docs/`
4. **README Separation**:
   - Top-level README: Overall project overview
   - Workflow README: Individual workflow details

## Agent Implementation Patterns

### Creating ChatClientAgent

```csharp
static ChatClientAgent CreateSpecialistAgent(
    IChatClient chatClient,
    string specialty,
    string description)
{
    var instructions = $"""
あなたは {description} として回答します。
専門知識を活用してユーザーの質問に答えてください。
簡潔で実用的な回答を心がけてください。
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        $"{specialty.ToLower()}_agent",  // Agent ID: snake_case
        $"{specialty} Agent");            // Agent Name: readable
}
```

### Router Pattern (JSON structured output)

```csharp
var instructions = """
あなたは Router Agent です。
ユーザーの質問を分析し、必要な専門家を選抜してください。

以下の JSON 形式で回答してください：
{
  "selected": ["専門家1", "専門家2"],
  "reason": "選抜理由"
}
""";
```

### Moderator Pattern (Structured final answer)

```csharp
var instructions = """
あなたは Moderator Agent です。
複数の専門家の意見を統合し、構造化された最終回答を生成してください。

以下の構造で回答してください：
## 結論
## 根拠
## 各専門家の所見
## 次のアクション
""";
```

## Configuration and Deployment

### Required Environment Variables

```bash
# Azure OpenAI settings
export AZURE_OPENAI_ENDPOINT="https://your-endpoint.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o"

# Telemetry settings (optional)
export OTEL_EXPORTER_OTLP_ENDPOINT="http://localhost:4317"

# Application Insights (optional)
export APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=...;IngestionEndpoint=..."
```

### appsettings.json Structure

```json
{
  "environmentVariables": {
    "AZURE_OPENAI_ENDPOINT": "https://your-endpoint.openai.azure.com/",
    "AZURE_OPENAI_DEPLOYMENT_NAME": "gpt-4o"
  },
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
}
```

### Authentication

- **Azure CLI**: Local authentication via `az login`
- **DefaultAzureCredential**: Supports Managed Identity and Service Principal

## Coding Conventions

### General Principles

1. **UTF-8 Encoding**: All files use UTF-8
2. **Japanese Comments**: Domain knowledge comments in Japanese
3. **Structured Logging**: Always use parameterized structured logging
4. **Async Processing**: Use async/await for I/O-bound operations

### Naming Conventions

- **Project Names**: PascalCase (e.g., `SelectiveGroupChatWorkflow`)
- **Agent ID**: snake_case (e.g., `router_agent`, `contract_agent`)
- **Agent Name**: Human-readable (e.g., `Router Agent`, `Contract Agent`)
- **File Names**: PascalCase (e.g., `Program.cs`, `README.md`)

### Error Handling Pattern

```csharp
try
{
    // Processing
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ エラー: {ErrorMessage}", ex.Message);
    // Fallback processing
}
```

## Development Workflow

### Building

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/SelectiveGroupChatWorkflow

# Release build
dotnet build -c Release
```

### Running

```bash
# Run specific workflow
cd src/SelectiveGroupChatWorkflow
dotnet run

# Or from solution root
dotnet run --project src/SelectiveGroupChatWorkflow
```

### Aspire Dashboard Setup

```bash
# Start with Docker Compose
docker compose up -d

# View logs
docker compose logs -f aspire-dashboard

# Stop
docker compose down

# Access dashboard at http://localhost:18888
```

## Important Guidelines for Copilot

### When Adding New Workflows

1. Create new project under `src/`
2. Add to `AgentWorkflows.sln`
3. Add required NuGet packages (reference existing projects)
4. Implement workflow in `Program.cs`
5. Create `README.md` with usage documentation
6. Update top-level `README.md` with new workflow

### When Adding New Specialist Agents

```csharp
var newSpecialist = CreateSpecialistAgent(
    chatClient,
    "NewSpecialty",
    "新しい専門領域の専門家");

specialists.Add("NewSpecialty", newSpecialist);
```

Don't forget to add the new specialist to Router's instructions.

### When Modifying Telemetry

- Use environment variable `OTEL_EXPORTER_OTLP_ENDPOINT`
- Or modify in `appsettings.json`
- Always test with Aspire Dashboard to verify telemetry is working

### Code Quality Standards

1. **Always use structured logging** with parameters, never string interpolation
2. **Use UTF-8 encoding** and set `Console.OutputEncoding = Encoding.UTF8`
3. **Follow clean architecture** - respect layer boundaries
4. **Add appropriate error handling** with fallback mechanisms
5. **Use consistent phase separators** and status emojis in logs
6. **Document complex logic** in Japanese comments
7. **Test with Azure OpenAI** before committing

### Documentation Updates

When making significant changes, update:

- `README.md` for user-facing changes
- `docs/architecture/` for architectural changes
- `docs/development/` for development process changes
- `docs/workflows/` for workflow-specific details
- This file (`.github/copilot-instructions.md`) for Copilot context changes

## Common Patterns to Follow

### Parallel Execution Pattern

```csharp
var tasks = selectedSpecialists.Select(async specialist =>
{
    return await ExecuteSpecialistAsync(specialist, question);
});

var results = await Task.WhenAll(tasks);
```

### Activity Tracing Pattern

```csharp
using var activity = activitySource.StartActivity("Phase1_Router");
activity?.SetTag("question", question);
activity?.SetTag("selected_count", selected.Count);
```

### Configuration Loading Pattern

```csharp
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var endpoint = configuration["environmentVariables:AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("環境変数 AZURE_OPENAI_ENDPOINT が設定されていません。");
```

## Reference Links

- [Microsoft Agent Framework Docs](https://learn.microsoft.com/ja-jp/dotnet/ai/quickstarts/quickstart-ai-chat-with-agents)
- [Azure OpenAI Service](https://learn.microsoft.com/ja-jp/azure/ai-services/openai/)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/net/)
- [.NET Aspire Dashboard](https://learn.microsoft.com/ja-jp/dotnet/aspire/fundamentals/dashboard)

---

**Note**: This file provides context for GitHub Copilot to understand the project structure, conventions, and patterns. For detailed documentation, refer to files in the `docs/` directory.
