// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Common;

/// <summary>
/// 共通の Agent ファクトリークラス。
/// 各ワークフローで再利用可能な専門家エージェントを生成します。
/// </summary>
public static class AgentFactory
{
        /// <summary>
        /// Contract (契約) 専門家エージェントを作成します。
        /// </summary>
        public static ChatClientAgent CreateContractAgent(IChatClient chatClient)
        {
                var instructions = """
あなたは Contract (契約) 専門家です。
契約条項、契約リスク、法的義務、契約期間、更新条件などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""";

                return new ChatClientAgent(
                    chatClient,
                    instructions,
                    "contract_agent",
                    "Contract Agent");
        }

        /// <summary>
        /// Spend (支出分析) 専門家エージェントを作成します。
        /// </summary>
        public static ChatClientAgent CreateSpendAgent(IChatClient chatClient)
        {
                var instructions = """
あなたは Spend Analysis (支出分析) 専門家です。
コスト構造、支出トレンド、予算管理、コスト削減機会などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""";

                return new ChatClientAgent(
                    chatClient,
                    instructions,
                    "spend_agent",
                    "Spend Agent");
        }

        /// <summary>
        /// Negotiation (交渉) 専門家エージェントを作成します。
        /// </summary>
        public static ChatClientAgent CreateNegotiationAgent(IChatClient chatClient)
        {
                var instructions = """
あなたは Negotiation (交渉) 専門家です。
交渉戦略、条件改善提案、価格交渉、契約条件の最適化などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""";

                return new ChatClientAgent(
                    chatClient,
                    instructions,
                    "negotiation_agent",
                    "Negotiation Agent");
        }

        /// <summary>
        /// Sourcing (調達) 専門家エージェントを作成します。
        /// </summary>
        public static ChatClientAgent CreateSourcingAgent(IChatClient chatClient)
        {
                var instructions = """
あなたは Sourcing (調達) 専門家です。
サプライヤー選定、調達戦略、品質管理、納期管理などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""";

                return new ChatClientAgent(
                    chatClient,
                    instructions,
                    "sourcing_agent",
                    "Sourcing Agent");
        }

        /// <summary>
        /// Supplier (サプライヤー評価) 専門家エージェントを作成します。
        /// </summary>
        public static ChatClientAgent CreateSupplierAgent(IChatClient chatClient)
        {
                var instructions = """
あなたは Supplier Management (サプライヤー管理) 専門家です。
サプライヤーの信頼性、パフォーマンス評価、リスク評価、関係管理などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""";

                return new ChatClientAgent(
                    chatClient,
                    instructions,
                    "supplier_agent",
                    "Supplier Agent");
        }

        /// <summary>
        /// Knowledge (ナレッジ管理) 専門家エージェントを作成します。
        /// </summary>
        public static ChatClientAgent CreateKnowledgeAgent(IChatClient chatClient)
        {
                var instructions = """
あなたは Knowledge Management (ナレッジ管理) 専門家です。
過去の事例、ベストプラクティス、組織の知見、業界標準などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""";

                return new ChatClientAgent(
                    chatClient,
                    instructions,
                    "knowledge_agent",
                    "Knowledge Agent");
        }

        /// <summary>
        /// Legal (法務) 専門家エージェントを作成します。
        /// </summary>
        public static ChatClientAgent CreateLegalAgent(IChatClient chatClient)
        {
                var instructions = """
あなたは Legal (法務) 専門家です。
法的リスク、コンプライアンス、規制要件、法的義務、知的財産権などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""";

                return new ChatClientAgent(
                    chatClient,
                    instructions,
                    "legal_agent",
                    "Legal Agent");
        }

        /// <summary>
        /// Finance (財務) 専門家エージェントを作成します。
        /// </summary>
        public static ChatClientAgent CreateFinanceAgent(IChatClient chatClient)
        {
                var instructions = """
あなたは Finance (財務) 専門家です。
財務影響、予算管理、ROI分析、キャッシュフロー、財務リスクなどの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""";

                return new ChatClientAgent(
                    chatClient,
                    instructions,
                    "finance_agent",
                    "Finance Agent");
        }

        /// <summary>
        /// Procurement (調達実務) 専門家エージェントを作成します。
        /// </summary>
        public static ChatClientAgent CreateProcurementAgent(IChatClient chatClient)
        {
                var instructions = """
あなたは Procurement (調達実務) 専門家です。
調達プロセス、購買手続き、契約管理、サプライヤー管理、調達戦略などの観点から分析を提供します。
簡潔で実用的な回答を心がけてください。
""";

                return new ChatClientAgent(
                    chatClient,
                    instructions,
                    "procurement_agent",
                    "Procurement Agent");
        }
}
