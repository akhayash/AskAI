using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
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

// ���ϐ���ݒ肩��擾
var endpoint = configuration["environmentVariables:AZURE_OPENAI_ENDPOINT"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("���ϐ� AZURE_OPENAI_ENDPOINT ���ݒ肳��Ă��܂���B");

var deployment = configuration["environmentVariables:AZURE_OPENAI_DEPLOYMENT_NAME"]
    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? throw new InvalidOperationException("���ϐ� AZURE_OPENAI_DEPLOYMENT_NAME ���ݒ肳��Ă��܂���B");

Console.WriteLine($"�G���h�|�C���g: {endpoint}");
Console.WriteLine($"�f�v���C�����g��: {deployment}");

Console.WriteLine("\n�F�؏��̎擾���iAzure CLI �݂̂��g�p�j...");
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
Console.WriteLine("�F�؏��擾����");

var openAIClient = new AzureOpenAIClient(new Uri(endpoint), credential);
var chatClient = openAIClient.GetChatClient(deployment);
IChatClient extensionsAIChatClient = chatClient.AsIChatClient();

Console.WriteLine("\n=== Group Chat Workflow �f�� ===");
Console.WriteLine("�������͂��Ă��������B");
Console.Write("����> ");
var question = Console.ReadLine();

if (string.IsNullOrWhiteSpace(question))
{
    Console.WriteLine("���₪��ł��B");
    return;
}

// ���ƃG�[�W�F���g���쐬
var contractAgent = CreateSpecialistAgent(extensionsAIChatClient, "Contract", "�_��֘A�̐���");
var spendAgent = CreateSpecialistAgent(extensionsAIChatClient, "Spend", "�x�o���͂̐���");
var negotiationAgent = CreateSpecialistAgent(extensionsAIChatClient, "Negotiation", "���헪�̐���");
var sourcingAgent = CreateSpecialistAgent(extensionsAIChatClient, "Sourcing", "���B�헪�̐���");
var knowledgeAgent = CreateSpecialistAgent(extensionsAIChatClient, "Knowledge", "�m���Ǘ��̐���");
var supplierAgent = CreateSpecialistAgent(extensionsAIChatClient, "Supplier", "�T�v���C���[�Ǘ��̐���");

// GitHub�T���v���Ɋ�Â������� Group Chat ����
// RoundRobinGroupChatManager ���g�p���āA�S�������Ԃɔ���
var workflow = AgentWorkflowBuilder
    .CreateGroupChatBuilderWith(agents => new AgentWorkflowBuilder.RoundRobinGroupChatManager(agents) 
    { 
        MaximumIterationCount = 5  // �ő�5���E���h�܂ŋc�_
    })
    .AddParticipants([contractAgent, spendAgent, negotiationAgent, sourcingAgent, knowledgeAgent, supplierAgent])
    .Build();

// ���[�N�t���[�����s
var messages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.User, question)
};

Console.WriteLine("\n����������������������������������������������������������������������������");
Console.WriteLine("Group Chat ���[�N�t���[���s�J�n");
Console.WriteLine("����������������������������������������������������������������������������\n");

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(180));

try
{
    var workflowAgent = await workflow.AsAgentAsync("group_chat", "Group Chat Workflow");
    var thread = workflowAgent.GetNewThread();

    var messageCount = 0;
    var currentAgentName = "";
    var currentMessage = new StringBuilder();
    
    await foreach (var update in workflowAgent.RunStreamingAsync(messages, thread, cancellationToken: cts.Token))
    {
        // �G�[�W�F���g���ς�����ꍇ
        if (!string.IsNullOrEmpty(update.AuthorName) && update.AuthorName != currentAgentName)
        {
            // �O�̃��b�Z�[�W���o��
            if (currentMessage.Length > 0)
            {
                Console.WriteLine("\n");
                currentMessage.Clear();
            }
            
            messageCount++;
            currentAgentName = update.AuthorName;
            
            // �V�����G�[�W�F���g�̃w�b�_�[
            Console.WriteLine($"\n���� [{messageCount}] {currentAgentName} ����������������������������������");
            Console.Write("�� ");
        }
        
        // �e�L�X�g��~�ς��ĕ\��
        if (!string.IsNullOrWhiteSpace(update.Text))
        {
            Console.Write(update.Text);
            currentMessage.Append(update.Text);
        }
    }

    // �Ō�̃��b�Z�[�W�̏I��
    if (currentMessage.Length > 0)
    {
        Console.WriteLine("\n������������������������������������������������������������������������������");
    }

    Console.WriteLine($"\n\n����������������������������������������������������������������������������");
    Console.WriteLine($"���v���b�Z�[�W��: {messageCount}");
    Console.WriteLine("����������������������������������������������������������������������������");
}
catch (OperationCanceledException)
{
    Console.WriteLine("\n?? �^�C���A�E�g: ���[�N�t���[�����ԓ��Ɋ������܂���ł����B");
}
catch (Exception ex)
{
    Console.WriteLine($"\n? �G���[: {ex.GetType().Name}");
    Console.WriteLine($"���b�Z�[�W: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"�����G���[: {ex.InnerException.Message}");
    }
}

Console.WriteLine("\nEnter �L�[�������ďI�����Ă�������...");
Console.ReadLine();

static ChatClientAgent CreateSpecialistAgent(IChatClient chatClient, string specialty, string description)
{
    var instructions = $"""
���Ȃ���{description}�Ƃ��āA�O���[�v�`���b�g�ɎQ�����Ă��܂��B
���̃G�[�W�F���g�̔�����ǂ݁A���Ȃ��̐��m�������p���ċc�_�ɍv�����Ă��������B

����:
- ��啪��̎��_����Ȍ��Ɉӌ����q�ׂ�i2-3�����x�j
- ���̃G�[�W�F���g�̈ӌ��𓥂܂��ăR�����g����
- �c�_��O�i�����鎿����Ă��s��
- ���_���o���ꍇ�́A���̃G�[�W�F���g�Ƀn���h�I�t����

�d�v:
- �Ȍ��ɗv�_�݂̂��q�ׂĂ�������
- �璷�Ȑ����͔����Ă�������
- �c�_���i�W���Ȃ��ꍇ�́A�K�؂ȃG�[�W�F���g�Ƀn���h�I�t���Ă�������
""";

    return new ChatClientAgent(
        chatClient,
        instructions,
        $"{specialty.ToLowerInvariant()}_agent",
        $"{specialty} Agent");
}
