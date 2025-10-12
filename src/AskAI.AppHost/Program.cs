var builder = DistributedApplication.CreateBuilder(args);

// Add the workflow projects as executable resources
var selectiveGroupChat = builder.AddProject<Projects.SelectiveGroupChatWorkflow>("selectivegroupchat")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

var handoffWorkflow = builder.AddProject<Projects.HandoffWorkflow>("handoffworkflow")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

var taskBasedWorkflow = builder.AddProject<Projects.TaskBasedWorkflow>("taskbasedworkflow")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

var groupChatWorkflow = builder.AddProject<Projects.GroupChatWorkflow>("groupchatworkflow")
    .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "http://localhost:4317");

builder.Build().Run();
