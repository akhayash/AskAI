"use client";

import {
  CopilotKit,
  useCopilotReadable,
  useCopilotAction,
} from "@copilotkit/react-core";
import { CopilotChat } from "@copilotkit/react-ui";
import "@copilotkit/react-ui/styles.css";
import { useState } from "react";
import {
  FileText,
  DollarSign,
  Handshake,
  ShoppingCart,
  BookOpen,
  Building2,
  Sparkles,
  MessageSquare,
} from "lucide-react";

/**
 * CopilotKit統合ページ
 *
 * DevUIHostのAG-UIエンドポイントに接続します。
 * API Route (/api/copilotkit) を経由してHttpAgentでAG-UIプロトコル通信を行います。
 *
 * 参考: https://docs.copilotkit.ai/microsoft-agent-framework/quickstart
 */

interface Agent {
  id: string;
  name: string;
  icon: React.ElementType;
  description: string;
  color: string;
  examples: string[];
}

// CopilotKit機能を使用する内部コンポーネント
function CopilotContent({
  agents,
  selectedAgent,
  setSelectedAgent,
}: {
  agents: Agent[];
  selectedAgent: Agent;
  setSelectedAgent: (agent: Agent) => void;
}) {
  // 1. useCopilotReadable: エージェント情報をAIと共有
  useCopilotReadable({
    description: "現在選択されているエージェントの情報",
    value: {
      agentId: selectedAgent.id,
      agentName: selectedAgent.name,
      agentDescription: selectedAgent.description,
      availableAgents: agents.map((a) => ({
        id: a.id,
        name: a.name,
        description: a.description,
      })),
    },
  });

  // 2. useCopilotAction: エージェント切り替えアクション
  useCopilotAction({
    name: "switchAgent",
    description:
      "別の専門エージェントに切り替えます。ユーザーが別の分野の質問をした場合に使用します。",
    parameters: [
      {
        name: "agentId",
        type: "string",
        description:
          "切り替え先のエージェントID (contract, spend, negotiation, sourcing, knowledge, supplier)",
        required: true,
      },
    ],
    handler: async ({ agentId }) => {
      const agent = agents.find((a) => a.id === agentId);
      if (agent) {
        setSelectedAgent(agent);
        return `${agent.name}に切り替えました。${agent.description}`;
      }
      return "エージェントが見つかりませんでした。";
    },
  });

  return (
    <div className="flex justify-center items-center h-full w-full">
      <div className="h-full w-full">
        <CopilotChat
          className="h-full"
          labels={{
            title: selectedAgent.name,
            initial: `${selectedAgent.name}に質問してください。専門知識を活用して回答します。\n\n💡 他の分野について質問する場合は、自動的に適切なエージェントに切り替えます。`,
          }}
          instructions={`あなたは${selectedAgent.name}です。${selectedAgent.description}に関する質問に答えてください。`}
          suggestions={selectedAgent.examples.map((example) => ({
            title: example,
            message: example,
          }))}
        />
      </div>
    </div>
  );
}

export default function CopilotKitPage() {
  // 利用可能なエージェント (DevUIHostで定義されているID)
  const agents: Agent[] = [
    {
      id: "contract",
      name: "Contract Agent",
      icon: FileText,
      description: "契約書の分析、リスク評価、条項のレビューを支援",
      color: "blue",
      examples: [
        "この契約のリスクを評価してください",
        "自動更新条項について説明してください",
        "ペナルティ条項の有無を確認してください",
      ],
    },
    {
      id: "spend",
      name: "Spend Agent",
      icon: DollarSign,
      description: "支出分析、コスト最適化、予算管理をサポート",
      color: "green",
      examples: [
        "今月の支出トレンドを教えてください",
        "コスト削減の機会を特定してください",
        "予算超過のリスクを分析してください",
      ],
    },
    {
      id: "negotiation",
      name: "Negotiation Agent",
      icon: Handshake,
      description: "交渉戦略の提案、条件改善の支援",
      color: "purple",
      examples: [
        "より良い契約条件を提案してください",
        "交渉のポイントを教えてください",
        "代替案を検討してください",
      ],
    },
    {
      id: "sourcing",
      name: "Sourcing Agent",
      icon: ShoppingCart,
      description: "調達戦略、サプライヤー選定、購買最適化",
      color: "orange",
      examples: [
        "最適なサプライヤーを提案してください",
        "調達プロセスを改善してください",
        "リスク分散の方法を教えてください",
      ],
    },
    {
      id: "knowledge",
      name: "Knowledge Agent",
      icon: BookOpen,
      description: "社内知識ベース、ベストプラクティス、ポリシー参照",
      color: "indigo",
      examples: [
        "社内の調達ポリシーを教えてください",
        "過去の類似案件を探してください",
        "ベストプラクティスを参照してください",
      ],
    },
    {
      id: "supplier",
      name: "Supplier Agent",
      icon: Building2,
      description: "サプライヤー情報管理、パフォーマンス評価",
      color: "cyan",
      examples: [
        "このサプライヤーの評価を教えてください",
        "納期実績を確認してください",
        "代替サプライヤーを提案してください",
      ],
    },
  ];
  const [selectedAgent, setSelectedAgent] = useState<Agent>(agents[0]);

  const getColorClasses = (color: string) => {
    const colors: Record<
      string,
      { bg: string; text: string; border: string; hover: string }
    > = {
      blue: {
        bg: "bg-blue-50",
        text: "text-blue-600",
        border: "border-blue-200",
        hover: "hover:bg-blue-100",
      },
      green: {
        bg: "bg-green-50",
        text: "text-green-600",
        border: "border-green-200",
        hover: "hover:bg-green-100",
      },
      purple: {
        bg: "bg-purple-50",
        text: "text-purple-600",
        border: "border-purple-200",
        hover: "hover:bg-purple-100",
      },
      orange: {
        bg: "bg-orange-50",
        text: "text-orange-600",
        border: "border-orange-200",
        hover: "hover:bg-orange-100",
      },
      indigo: {
        bg: "bg-indigo-50",
        text: "text-indigo-600",
        border: "border-indigo-200",
        hover: "hover:bg-indigo-100",
      },
      cyan: {
        bg: "bg-cyan-50",
        text: "text-cyan-600",
        border: "border-cyan-200",
        hover: "hover:bg-cyan-100",
      },
    };
    return colors[color] || colors.blue;
  };

  return (
    <div className="h-screen flex bg-gradient-to-br from-slate-50 via-blue-50 to-slate-50">
      {/* サイドバー - エージェント選択 */}
      <div className="w-80 bg-white border-r border-slate-200 shadow-lg flex flex-col">
        {/* ヘッダー */}
        <div className="p-6 border-b border-slate-200 bg-gradient-to-r from-blue-600 to-purple-600">
          <div className="flex items-center gap-2 mb-2">
            <Sparkles className="w-6 h-6 text-white" />
            <h1 className="text-xl font-bold text-white">
              AI Procurement Copilot
            </h1>
          </div>
          <p className="text-sm text-blue-100">
            調達業務を支援する専門AIエージェント
          </p>
        </div>

        {/* エージェントリスト */}
        <div className="flex-1 overflow-y-auto p-4 space-y-2">
          <h2 className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-3 px-2">
            エージェントを選択
          </h2>
          {agents.map((agent) => {
            const AgentIcon = agent.icon;
            const colors = getColorClasses(agent.color);
            const isSelected = selectedAgent.id === agent.id;

            return (
              <button
                key={agent.id}
                onClick={() => setSelectedAgent(agent)}
                className={`w-full text-left p-3 rounded-lg transition-all ${
                  isSelected
                    ? `${colors.bg} ${colors.border} border-2 shadow-md`
                    : `bg-slate-50 border border-slate-200 ${colors.hover}`
                }`}
              >
                <div className="flex items-start gap-3">
                  <div className={`p-2 rounded-lg ${colors.bg} ${colors.text}`}>
                    <AgentIcon className="w-5 h-5" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="font-semibold text-sm text-slate-900 mb-1">
                      {agent.name}
                    </div>
                    <div className="text-xs text-slate-600 line-clamp-2">
                      {agent.description}
                    </div>
                  </div>
                </div>
              </button>
            );
          })}
        </div>

        {/* フッター */}
        <div className="p-4 border-t border-slate-200 bg-slate-50">
          <div className="flex items-center gap-2 text-xs text-slate-500">
            <MessageSquare className="w-4 h-4" />
            <span>Powered by CopilotKit + AG-UI</span>
          </div>
        </div>
      </div>

      {/* メインコンテンツ */}
      <div className="flex-1 flex flex-col">
        {/* エージェント情報ヘッダー */}
        <div className="bg-white border-b border-slate-200 shadow-sm p-6">
          <div className="flex items-start gap-4">
            <div
              className={`p-3 rounded-xl ${
                getColorClasses(selectedAgent.color).bg
              } ${getColorClasses(selectedAgent.color).text}`}
            >
              {(() => {
                const AgentIcon = selectedAgent.icon;
                return <AgentIcon className="w-8 h-8" />;
              })()}
            </div>
            <div className="flex-1">
              <h2 className="text-2xl font-bold text-slate-900 mb-1">
                {selectedAgent.name}
              </h2>
              <p className="text-slate-600">{selectedAgent.description}</p>
            </div>
          </div>
        </div>

        {/* CopilotKit チャット */}
        <div className="flex-1 flex flex-col overflow-hidden bg-slate-50">
          <CopilotKit
            key={selectedAgent.id}
            runtimeUrl={`/api/copilotkit?agent=${selectedAgent.id}`}
            agent={selectedAgent.id}
          >
            <CopilotContent
              agents={agents}
              selectedAgent={selectedAgent}
              setSelectedAgent={setSelectedAgent}
            />
          </CopilotKit>
        </div>
      </div>
    </div>
  );
}
