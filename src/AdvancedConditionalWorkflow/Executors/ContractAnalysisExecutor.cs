// Copyright (c) Microsoft. All rights reserved.

using AdvancedConditionalWorkflow.Models;
using Microsoft.Agents.AI.Workflows;

namespace AdvancedConditionalWorkflow.Executors;

/// <summary>
/// 契約情報を分析し、レビューが必要な専門分野を特定する Executor
/// State に ReviewResult リストを初期化
/// </summary>
public class ContractAnalysisExecutor(string id = "contract_analysis")
    : Executor<ContractInfo, (ContractInfo Contract, List<ReviewResult> Reviews)>(id)
{
    public override async ValueTask<(ContractInfo Contract, List<ReviewResult> Reviews)> HandleAsync(
        ContractInfo input,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // 非同期処理のプレースホルダー

        // 契約情報と空のレビューリストを返す
        return (input, new List<ReviewResult>());
    }
}
