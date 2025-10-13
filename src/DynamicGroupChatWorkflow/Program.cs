using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Text;
using ChatRole = Microsoft.Extensions.AI.ChatRole;

// �R���\�[���̕����G���R�[�f�B���O�� UTF-8 �ɐݒ�
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// �ݒ��ǂݍ���
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

// OpenTelemetry �ƃ��M���O��ݒ�
var appInsightsConnectionString = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
    ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

// �󕶎���̏ꍇ���f�t�H���g�l���g�p
if (string.IsNullOrEmpty(otlpEndpoint))
{
    otlpEndpoint = "http://localhost:4317";
}

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService("DynamicGroupChatWorkflow"));
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;

        options.AddOtlpExporter(exporterOptions =>
        {
            exporterOptions.Endpoint = new Uri(otlpEndpoint);
        });

        options.AddConsoleExporter();
    });
    builder.AddSimpleConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    });

    builder.SetMinimumLevel(LogLevel.Information);
});

var activitySource = new ActivitySource("DynamicGroupChatWorkflow");

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService("DynamicGroupChatWorkflow"))
    .AddSource("DynamicGroupChatWorkflow")
    .AddHttpClientInstrumentation()
    .AddOtlpExporter(exporterOptions =>
    {
        exporterOptions.Endpoint = new Uri(otlpEndpoint);
    })
    .AddConsoleExporter()
    .Build();

var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("=== �A�v���P�[�V�����N�� ===");
logger.LogInformation("�e�����g���ݒ�: OTLP Endpoint = {OtlpEndpoint}", otlpEndpoint);
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    logger.LogInformation("Application Insights �ڑ������񂪐ݒ肳��Ă��܂�");
}

// ���ϐ���ݒ肩��擾
var endpoint = configuration["environmentVariables:AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("���ϐ� AZURE_OPENAI_ENDPOINT ���ݒ肳��Ă��܂���B");

var deployment = configuration["environmentVariables:AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("���ϐ� AZURE_OPENAI_DEPLOYMENT_NAME ���ݒ肳��Ă��܂���B");

logger.LogInformation("�G���h�|�C���g: {Endpoint}", endpoint);
logger.LogInformation("�f�v���C�����g��: {DeploymentName}", deployment);

logger.LogInformation("�F�؏��̎擾���iAzure CLI �݂̂��g�p�j...");
var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
{
    ExcludeEnvironmentCredential = true,
    ExcludeManagedIdentityCredential = true,
    ExcludeSharedTokenCacheCredential = true,
    ExcludeVisualStudioCredential = true,
    ExcludeVisualStudioCodeCredential = true,
    ExcludeAzureCliCredential = false,  // Azure CLI �̂ݗL��
    ExcludeAzurePowerShellCredential = true,
    ExcludeAzureDeveloperCliCredential = true,
    ExcludeInteractiveBrowserCredential = true,
    ExcludeWorkloadIdentityCredential = true
});
logger.LogInformation("�F�؏��擾����");

var openAIClient = new AzureOpenAIClient(new Uri(endpoint), credential);
var chatClient = openAIClient.GetChatClient(deployment);
IChatClient extensionsAIChatClient = chatClient.AsIChatClient();

logger.LogInformation("=== Dynamic Group Chat Workflow �f�� ===");
// Keep user-facing prompt only; logger will output structured logs (also to console via exporter)
Console.WriteLine("Router �����I�ɐ��Ƃ�I�����A�K�v�ɉ����ă��[�U�[�Ɉӌ������߂܂��B");
Console.WriteLine("�������͂��Ă��������B");
Console.Write("����> ");
var question = Console.ReadLine();

if (string.IsNullOrWhiteSpace(question))
{
    logger.LogWarning("���₪��ł��B");
    Console.WriteLine("���₪��ł��B");
    return;
}

logger.LogInformation("��M��������: {Question}", question);

// �G�[�W�F���g�쐬
logger.LogInformation("�G�[�W�F���g���쐬��...");
var routerAgent = CreateRouterAgent(extensionsAIChatClient);
var contractAgent = CreateSpecialistAgent(extensionsAIChatClient, "Contract", "�_��֘A�̐���");
var spendAgent = CreateSpecialistAgent(extensionsAIChatClient, "Spend", "�x�o���͂̐���");
var negotiationAgent = CreateSpecialistAgent(extensionsAIChatClient, "Negotiation", "���헪�̐���");
var sourcingAgent = CreateSpecialistAgent(extensionsAIChatClient, "Sourcing", "���B�헪�̐���");
var knowledgeAgent = CreateSpecialistAgent(extensionsAIChatClient, "Knowledge", "�m���Ǘ��̐���");
var supplierAgent = CreateSpecialistAgent(extensionsAIChatClient, "Supplier", "�T�v���C���[�Ǘ��̐���");
var moderatorAgent = CreateModeratorAgent(extensionsAIChatClient);
logger.LogInformation("�G�[�W�F���g�쐬����");

// ���[�N�t���[�\�z
logger.LogInformation("���[�N�t���[���\�z��...");
var workflow = AgentWorkflowBuilder
    .CreateHandoffBuilderWith(routerAgent)
    .WithHandoffs(routerAgent, [contractAgent, spendAgent, negotiationAgent,
                                  sourcingAgent, knowledgeAgent, supplierAgent,
                                  moderatorAgent])
    .WithHandoffs([contractAgent, spendAgent, negotiationAgent,
                   sourcingAgent, knowledgeAgent, supplierAgent], routerAgent)
    .Build();
logger.LogInformation("���[�N�t���[�\�z����");

// ���[�N�t���[���s
var messages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.User, question)
};

logger.LogInformation("��������������������������������������������������������������������������������");
logger.LogInformation("���[�N�t���[���s�J�n");
logger.LogInformation("��������������������������������������������������������������������������������");

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300));

try
{
    using var workflowActivity = activitySource.StartActivity("Workflow: Dynamic Group Chat", ActivityKind.Internal);
    workflowActivity?.SetTag("initial.question", question);

    logger.LogInformation("���[�N�t���[���G�[�W�F���g�ɕϊ���...");
    var workflowAgent = await workflow.AsAgentAsync("workflow", "Dynamic Workflow");
    logger.LogInformation("�G�[�W�F���g�ϊ�����");
    workflowActivity?.SetTag("workflow.agent.id", workflowAgent.Id);

    var thread = workflowAgent.GetNewThread();
    logger.LogInformation("�X���b�h�쐬����");

    logger.LogInformation("���b�Z�[�W��: {MessageCount}", messages.Count);
    logger.LogInformation("���b�Z�[�W���e: {MessageText}", messages[0].Text);
    logger.LogInformation("�X�g���[�~���O�J�n...");

    var currentAgent = "";
    var messageCount = 0;
    var updateCount = 0;
    var maxMessages = 30;
    var currentMessage = new StringBuilder();
    var pendingQuestion = new StringBuilder();
    var waitingForUserInput = false;
    Activity? agentActivity = null;

    await foreach (var update in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
    {
        updateCount++;
        // �G�[�W�F���g���ς������\��
        if (!string.IsNullOrEmpty(update.AgentId) && update.AgentId != currentAgent)
        {
            // ���[�U�[���͑҂��`�F�b�N
            if (waitingForUserInput)
            {
                logger.LogInformation("[���[�U�[���͂�ҋ@] ����: {Question}", pendingQuestion.ToString());
                if (agentActivity != null)
                {
                    agentActivity.SetTag("message.length", currentMessage.Length);
                    agentActivity.SetTag("message.content.preview", TruncateForTelemetry(currentMessage.ToString()));
                    agentActivity.SetTag("status", "waiting-for-user");
                    agentActivity.Dispose();
                    agentActivity = null;
                }
                Console.WriteLine("\n��������������������������������������������������������������������������������");
                Console.WriteLine("?? ���[�U�[���͂��K�v�ł�");
                Console.WriteLine("��������������������������������������������������������������������������������");
                Console.Write("\n��> ");

                var userResponse = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(userResponse))
                {
                    messages.Add(new ChatMessage(ChatRole.User, userResponse));
                    logger.LogInformation("[���[�U�[���͎�M] ��: {Response}", userResponse);
                    workflowActivity?.AddEvent(new ActivityEvent("user-input-received", tags: new ActivityTagsCollection
                    {
                        { "user.input.length", userResponse.Length }
                    }));

                    // ���[�N�t���[���Ď��s
                    thread = workflowAgent.GetNewThread();
                    await foreach (var newUpdate in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
                    {
                        if (!string.IsNullOrEmpty(newUpdate.Text))
                        {
                            Console.Write(newUpdate.Text);
                        }
                    }
                    waitingForUserInput = false;
                    break;
                }

                waitingForUserInput = false;
                pendingQuestion.Clear();
            }

            // �O�̃��b�Z�[�W���o��
            if (currentMessage.Length > 0)
            {
                var messageText = currentMessage.ToString();
                var messagePreview = TruncateForTelemetry(messageText);
                logger.LogInformation("[���b�Z�[�W���� #{MessageCount}] �G�[�W�F���g: {AgentId}, ���e��: {ContentLength}, ���e: {Content}",
                    messageCount, currentAgent, currentMessage.Length, messageText);
                if (agentActivity != null)
                {
                    agentActivity.SetTag("message.length", currentMessage.Length);
                    agentActivity.SetTag("message.content.preview", messagePreview);
                    agentActivity.Dispose();
                    agentActivity = null;
                }
                currentMessage.Clear();
                pendingQuestion.Clear();
            }

            currentAgent = update.AgentId;
            messageCount++;

            logger.LogInformation("[���b�Z�[�W�J�n #{MessageCount}] �G�[�W�F���g��: {AgentName}, �G�[�W�F���gID: {AgentId}, ���[��: {Role}",
                messageCount, update.AuthorName ?? "�s��", update.AgentId, update.Role?.ToString() ?? "�s��");
            var agentDisplayName = update.AuthorName ?? $"Agent {messageCount}";
            agentActivity = activitySource.StartActivity($"Agent Turn: {agentDisplayName} (#{messageCount})", ActivityKind.Internal);
            agentActivity?.SetTag("agent.name", update.AuthorName ?? "�s��");
            agentActivity?.SetTag("agent.id", update.AgentId);
            agentActivity?.SetTag("agent.role", update.Role?.ToString() ?? "�s��");
            agentActivity?.SetTag("message.ordinal", messageCount);

            if (messageCount > maxMessages)
            {
                logger.LogWarning("?? �ő僁�b�Z�[�W�� ({MaxMessages}) �ɒB���܂����B", maxMessages);
                break;
            }

            Console.WriteLine($"\n������ {update.AuthorName ?? currentAgent} ������");
        }

        // �e�L�X�g��\��
        if (!string.IsNullOrEmpty(update.Text))
        {
            Console.Write(update.Text);
            currentMessage.Append(update.Text);
            pendingQuestion.Append(update.Text);
            agentActivity?.AddEvent(new ActivityEvent("stream-token", tags: new ActivityTagsCollection
            {
                { "token.length", update.Text.Length }
            }));

            // Router ����̎�������o�i�u�H�v�ŏI���ꍇ�j
            if (currentAgent == "router_agent" && update.Text.Contains("�H"))
            {
                waitingForUserInput = true;
            }
        }
    }

    // �Ō�̃��b�Z�[�W���o��
    if (currentMessage.Length > 0)
    {
        var messageText = currentMessage.ToString();
        var messagePreview = TruncateForTelemetry(messageText);
        logger.LogInformation("[���b�Z�[�W���� #{MessageCount}] �G�[�W�F���g: {AgentId}, ���S�ȓ��e��: {ContentLength}, ���e: {Content}",
            messageCount, currentAgent, currentMessage.Length, messageText);
        if (agentActivity != null)
        {
            agentActivity.SetTag("message.length", currentMessage.Length);
            agentActivity.SetTag("message.content.preview", messagePreview);
            agentActivity.Dispose();
        }
    }

    workflowActivity?.SetTag("total.messages", messageCount);
    workflowActivity?.SetTag("total.updates", updateCount);
    logger.LogInformation("�X�g���[�~���O�����B���b�Z�[�W��: {MessageCount}", messageCount);
    workflowActivity?.Stop();
}
catch (OperationCanceledException)
{
    logger.LogWarning("?? �^�C���A�E�g: ���[�N�t���[�����ԓ��Ɋ������܂���ł����B");
}
catch (Exception ex)
{
    logger.LogError(ex, "? �G���[: {ExceptionType}, ���b�Z�[�W: {ErrorMessage}",
        ex.GetType().Name, ex.Message);
    if (ex.InnerException != null)
    {
        logger.LogError("�����G���[: {InnerErrorMessage}", ex.InnerException.Message);
    }
}

logger.LogInformation("��������������������������������������������������������������������������������");
logger.LogInformation("���[�N�t���[���s����");
logger.LogInformation("��������������������������������������������������������������������������������");

Console.WriteLine("Enter �L�[�������ďI�����Ă�������...");
Console.ReadLine();

logger.LogInformation("=== �A�v���P�[�V�����I�� ===");

static string TruncateForTelemetry(string content, int maxLength = 512)
{
    if (string.IsNullOrEmpty(content))
    {
        return string.Empty;
    }

    if (content.Length <= maxLength)
    {
        return content;
    }

    return content[..maxLength] + "...";
}

static ChatClientAgent CreateRouterAgent(IChatClient chatClient)
{
    var instructions = """
���Ȃ��͒��B�̈�̃��[�^�[�ł��B

����:
1. ���[�U�[����𕪐͂��A�K�v�Ȑ��Ƃ𓮓I�ɑI��
2. ���ƂɃn���h�I�t���Ĉӌ������W
3. ���Ƃ̈ӌ��𓥂܂��A����ɏ�񂪕K�v�����f:
   - ���̐��Ƃ̈ӌ����K�v �� ���̐��ƂɃn���h�I�t
   - ���[�U�[�̒ǉ���񂪕K�v �� ���[�U�[�Ɏ��₵�Ă��������i�u�H�v�ŏI��鎿�╶�j
4. �\���ȏ�񂪏W�܂�����AModerator Agent �Ƀn���h�I�t

���p�\�ȃG�[�W�F���g:
- Contract Agent (�_��֘A)
- Spend Agent (�x�o����)
- Negotiation Agent (���헪)
- Sourcing Agent (���B�헪)
- Knowledge Agent (�m���Ǘ�)
- Supplier Agent (�T�v���C���[�Ǘ�)
- Moderator Agent (�ŏI����) �� �\���ȏ�񂪏W�܂����ꍇ�̂�

Human-in-the-Loop �̃K�C�h���C��:
- ���Ƃ̈ӌ��𕷂������ʁA���[�U�[�̒ǉ����i�\�Z�A�����A�D�掖���Ȃǁj���K�v�ȏꍇ:
  ������e�𖾊m�ɋL�q���Ă��������i��: "�\�Z�̏���������Ă��������B" "��]����_����Ԃ͉��N�ł����H"�j
  ����́u�H�v�ŏI���悤�ɂ��Ă��������B
- ���[�U�[����̉񓚂��󂯎������A����𓥂܂��Ď��̃A�N�V����������

�d�v:
- �K���K�؂ȃG�[�W�F���g�Ƀn���h�I�t���Ă�������
- �ߏ�ȃn���h�I�t�͔����Ă��������i�ʏ��2-3���̐��Ƃŏ\���j
- ���[�U�[�ւ̎���͖{���ɕK�v�ȏꍇ�̂݁i1��܂Ő����j
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        "router_agent",
        "Router Agent");
}

static ChatClientAgent CreateSpecialistAgent(IChatClient chatClient, string specialty, string description)
{
    var instructions = $"""
���Ȃ��� {description} �Ƃ��ĉ񓚂��܂��B

����:
- ���m�������p���ă��[�U�[�̎���ɓ�����
- ��b�����ɑ��̐��Ƃ̈ӌ��⃆�[�U�[�̒ǉ���񂪊܂܂�Ă���ꍇ�A�������Q�l�ɂ���
- �񓚂�����������A�K�� Router Agent �Ƀn���h�I�t���Č��ʂ�񍐂���

�񓚂̃K�C�h���C��:
- �Ȍ�����̓I�ɉ񓚁i2-3�����x�j
- �K�v�ɉ����āA���̐��Ƃ̈ӌ��ւ̌��y���\
- �s���ȓ_������΁ARouter Agent �ɒǉ����̕K�v����`����
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        $"{specialty.ToLowerInvariant()}_agent",
        $"{specialty} Agent");
}

static ChatClientAgent CreateModeratorAgent(IChatClient chatClient)
{
    var instructions = """
���Ȃ��̓��f���[�^�[�ł��B

����:
Router ����n���ꂽ��b������ǂ݁A�����̐��Ƃ̈ӌ��ƃ��[�U�[�̒ǉ����𓝍����čŏI�񓚂𐶐����܂��B

�v������:
- �e���Ƃ̏����𑸏d���Ȃ���A��ѐ��̂��錋�_�𓱂�
- ���[�U�[���񋟂����ǉ�����K�؂ɔ��f����
- �񓚂͈ȉ��̌`���ō\����:

## ���_
[�������ꂽ���_]

## ����
[�e���Ƃ̈ӌ��ƃ��[�U�[���͂𓥂܂�������]

## �e���Ƃ̏���
- Contract: [�v��]
- Negotiation: [�v��]
...

## ���[�U�[����̒ǉ����
[���[�U�[���񋟂������̗v��i�Y������ꍇ�j]

## ���̃A�N�V����
[��������鎟�̃X�e�b�v]
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        "moderator_agent",
        "Moderator Agent");
}
