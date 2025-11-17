"use client";

import { CopilotKit } from "@copilotkit/react-core";
import { CopilotChat } from "@copilotkit/react-ui";
import "@copilotkit/react-ui/styles.css";
import { useState } from "react";

/**
 * CopilotKit統合ページ
 *
 * DevUIHostのAG-UIエンドポイントに接続します。
 * API Route (/api/copilotkit) を経由してHttpAgentでAG-UIプロトコル通信を行います。
 *
 * 参考: https://docs.copilotkit.ai/microsoft-agent-framework/quickstart
 */
export default function CopilotKitPage() {
  // 利用可能なエージェント (DevUIHostで定義されているID)
  const agents = [
    { id: "contract", name: "Contract Agent" },
    { id: "spend", name: "Spend Agent" },
    { id: "negotiation", name: "Negotiation Agent" },
    { id: "sourcing", name: "Sourcing Agent" },
    { id: "knowledge", name: "Knowledge Agent" },
    { id: "supplier", name: "Supplier Agent" },
  ];
  const [selectedAgent, setSelectedAgent] = useState(agents[0]);

  return (
    <div className="h-screen flex flex-col bg-slate-50">
      {/* ヘッダー */}
      <div className="bg-white border-b border-slate-200 p-4 shadow-sm">
        <h1 className="text-2xl font-bold text-slate-900 mb-2">
          CopilotKit + AG-UI Demo
        </h1>
        <p className="text-sm text-slate-600 mb-3">
          DevUIHost に CopilotKit で接続 (AG-UIプロトコル経由)
        </p>

        {/* エージェント選択 */}
        <div className="flex items-center gap-2">
          <label className="text-sm font-medium text-slate-700">Agent:</label>
          <select
            value={selectedAgent.id}
            onChange={(e) => {
              const agent = agents.find((a) => a.id === e.target.value);
              if (agent) setSelectedAgent(agent);
            }}
            className="px-3 py-2 border border-slate-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            {agents.map((agent) => (
              <option key={agent.id} value={agent.id}>
                {agent.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* CopilotKit チャット */}
      <div className="flex-1 overflow-hidden">
        <CopilotKit
          key={selectedAgent.id}
          runtimeUrl={`/api/copilotkit?agent=${selectedAgent.id}`}
          agent={selectedAgent.id}
        >
          <CopilotChat
            labels={{
              title: selectedAgent.name,
              initial: `${selectedAgent.name}に質問してください。専門知識を活用して回答します。`,
            }}
          />
        </CopilotKit>
      </div>

      {/* フッター */}
      <div className="bg-white border-t border-slate-200 p-2 text-center">
        <p className="text-xs text-slate-500">
          🔗 AG-UI Protocol準拠 | Powered by CopilotKit + Microsoft Agent
          Framework
        </p>
      </div>
    </div>
  );
}
