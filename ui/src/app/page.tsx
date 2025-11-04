"use client";

import { useState, useEffect, useRef } from "react";
import {
  Send,
  CheckCircle2,
  XCircle,
  Loader2,
  AlertTriangle,
} from "lucide-react";
import { cn } from "@/lib/utils";
import type {
  Message,
  AgentUtteranceMessage,
  FinalResponseMessage,
  HITLRequestMessage,
  ErrorMessage,
  ContractSelectionRequestMessage,
} from "@/types/workflow";
import { ContractComparison } from "@/components/ContractComparison";

export default function Home() {
  const [messages, setMessages] = useState<Message[]>([]);
  const [ws, setWs] = useState<WebSocket | null>(null);
  const [connected, setConnected] = useState(false);
  const [pendingHitl, setPendingHitl] = useState<HITLRequestMessage | null>(
    null
  );
  const [pendingContractSelection, setPendingContractSelection] =
    useState<ContractSelectionRequestMessage | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    // WebSocket接続 (環境変数で設定可能)
    const wsUrl = process.env.NEXT_PUBLIC_WS_URL || "ws://localhost:8080";
    const websocket = new WebSocket(wsUrl);

    websocket.onopen = () => {
      console.log("WebSocket connected");
      setConnected(true);
    };

    websocket.onmessage = (event) => {
      const message = JSON.parse(event.data) as Message;
      console.log("Received message:", message);

      if (message.type === "hitl_request") {
        setPendingHitl(message as HITLRequestMessage);
      }

      if (message.type === "contract_selection_request") {
        setPendingContractSelection(message as ContractSelectionRequestMessage);
      }

      // 重複メッセージを防ぐ: messageIdで既存チェック
      setMessages((prev) => {
        const exists = prev.some((m) => m.messageId === message.messageId);
        if (exists) {
          console.warn(
            "Duplicate message detected, skipping:",
            message.messageId
          );
          return prev;
        }
        return [...prev, message];
      });
    };

    websocket.onerror = (error) => {
      console.error("WebSocket error:", error);
    };

    websocket.onclose = () => {
      console.log("WebSocket disconnected");
      setConnected(false);
    };

    setWs(websocket);

    return () => {
      websocket.close();
    };
  }, []);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  const handleHitlResponse = (approved: boolean) => {
    if (!ws || !pendingHitl) return;

    const response = {
      type: "hitl_response",
      approved,
      comment: "",
      timestamp: new Date().toISOString(),
      messageId: crypto.randomUUID(),
    };

    ws.send(JSON.stringify(response));
    setPendingHitl(null);
  };

  const handleContractSelection = (selectedIndex: number) => {
    if (!ws || !pendingContractSelection) return;

    const response = {
      type: "contract_selection",
      selectedIndex,
      timestamp: new Date().toISOString(),
      messageId: crypto.randomUUID(),
    };

    console.log("Sending contract selection:", response);
    ws.send(JSON.stringify(response));
    setPendingContractSelection(null);
  };

  const renderMessage = (message: Message) => {
    switch (message.type) {
      case "agent_utterance":
        const agentMsg = message as AgentUtteranceMessage;
        return (
          <div
            key={message.messageId}
            className="mb-5 p-5 rounded-xl bg-gradient-to-br from-white to-slate-50 border border-slate-200 shadow-sm hover:shadow-md transition-shadow"
          >
            <div className="flex items-start gap-4">
              <div className="flex-shrink-0 w-12 h-12 rounded-xl bg-gradient-to-br from-blue-500 to-indigo-600 flex items-center justify-center text-white font-bold text-lg shadow-md">
                {agentMsg.agentName.charAt(0)}
              </div>
              <div className="flex-1">
                <div className="flex items-center gap-2 mb-2 flex-wrap">
                  <span className="font-bold text-slate-900 text-lg">
                    {agentMsg.agentName}
                  </span>
                  {agentMsg.phase && (
                    <span className="text-xs px-3 py-1 rounded-full bg-blue-100 text-blue-700 font-semibold border border-blue-200">
                      📋 {agentMsg.phase}
                    </span>
                  )}
                  {agentMsg.riskScore !== undefined && (
                    <span
                      className={cn(
                        "text-xs px-3 py-1 rounded-full font-bold shadow-sm",
                        agentMsg.riskScore <= 30
                          ? "bg-green-100 text-green-700 border border-green-300"
                          : agentMsg.riskScore <= 70
                          ? "bg-yellow-100 text-yellow-700 border border-yellow-300"
                          : "bg-red-100 text-red-700 border border-red-300"
                      )}
                    >
                      ⚠️ Risk: {agentMsg.riskScore}/100
                    </span>
                  )}
                </div>
                <p className="text-slate-700 whitespace-pre-wrap leading-relaxed">
                  {agentMsg.content}
                </p>
                <span className="text-xs text-slate-500 mt-2 block font-medium">
                  🕒 {new Date(message.timestamp).toLocaleTimeString()}
                </span>
              </div>
            </div>
          </div>
        );

      case "final_response":
        const finalMsg = message as FinalResponseMessage;
        const hasNegotiation =
          finalMsg.decision?.original_contract_info &&
          (finalMsg.decision?.negotiation_history?.length ?? 0) > 0;

        return (
          <div
            key={message.messageId}
            className="mb-5 p-6 rounded-xl bg-gradient-to-br from-green-50 via-emerald-50 to-green-50 border-2 border-green-400 shadow-lg"
          >
            <div className="flex items-start gap-4">
              <div className="flex-shrink-0 w-12 h-12 bg-green-600 rounded-xl flex items-center justify-center shadow-md">
                <CheckCircle2 className="w-8 h-8 text-white" />
              </div>
              <div className="flex-1">
                <h3 className="text-2xl font-bold text-green-900 mb-3 flex items-center gap-2">
                  <span>✅ Final Decision</span>
                </h3>
                <p className="text-slate-800 font-semibold mb-4 text-lg bg-white/60 p-3 rounded-lg border border-green-200">
                  {finalMsg.summary}
                </p>

                {/* 交渉前後の差分表示 */}
                {finalMsg.decision?.original_contract_info && (
                  <div className="mt-4">
                    <ContractComparison
                      original={finalMsg.decision.original_contract_info}
                      final={finalMsg.decision.contract_info}
                    />
                  </div>
                )}

                {/* 交渉履歴 */}
                {hasNegotiation && finalMsg.decision.negotiation_history && (
                  <div className="mt-4 bg-white border-2 border-blue-200 rounded-xl p-5 shadow-sm">
                    <h4 className="text-lg font-bold text-slate-900 mb-3 flex items-center gap-2">
                      <span className="bg-blue-100 text-blue-700 px-3 py-1 rounded-full text-sm">
                        🔄 交渉履歴 ({finalMsg.decision.negotiation_history.length}回)
                      </span>
                    </h4>
                    <div className="space-y-3">
                      {finalMsg.decision.negotiation_history.map(
                        (negotiation: any, idx: number) => (
                          <div
                            key={idx}
                            className="p-4 bg-gradient-to-br from-slate-50 to-blue-50 rounded-lg border border-slate-300 shadow-sm"
                          >
                            <div className="font-bold text-sm text-blue-700 mb-2">
                              📌 反復 {negotiation.iteration}/3
                            </div>
                            <div className="text-sm text-slate-700 leading-relaxed">
                              {negotiation.rationale}
                            </div>
                          </div>
                        )
                      )}
                    </div>
                  </div>
                )}

                {/* Next Actions */}
                {finalMsg.decision?.next_actions &&
                  finalMsg.decision.next_actions.length > 0 && (
                    <div className="mt-4 bg-white border-2 border-green-300 rounded-xl p-5 shadow-sm">
                      <h4 className="text-lg font-bold text-slate-900 mb-3 flex items-center gap-2">
                        <span className="bg-green-100 text-green-700 px-3 py-1 rounded-full text-sm">
                          📋 Next Actions
                        </span>
                      </h4>
                      <ul className="space-y-2">
                        {finalMsg.decision.next_actions.map(
                          (action: string, idx: number) => (
                            <li key={idx} className="flex items-start gap-2 text-slate-700">
                              <span className="text-green-600 font-bold mt-0.5">✓</span>
                              <span className="text-sm leading-relaxed">{action}</span>
                            </li>
                          )
                        )}
                      </ul>
                    </div>
                  )}

                <span className="text-xs text-slate-500 mt-4 block font-medium">
                  🕒 {new Date(message.timestamp).toLocaleTimeString()}
                </span>
              </div>
            </div>
          </div>
        );

      case "workflow_start":
        return (
          <div
            key={message.messageId}
            className="mb-5 p-5 rounded-xl bg-gradient-to-br from-blue-50 to-indigo-50 border-2 border-blue-300 shadow-sm"
          >
            <div className="flex items-center gap-3">
              <div className="bg-blue-600 p-2 rounded-lg">
                <Loader2 className="w-6 h-6 text-white animate-spin" />
              </div>
              <div className="flex-1">
                <span className="font-bold text-blue-900 text-lg block">
                  🚀 Workflow Started
                </span>
                <span className="text-xs text-blue-700 font-medium mt-1 block">
                  🕒 {new Date(message.timestamp).toLocaleTimeString()}
                </span>
              </div>
            </div>
          </div>
        );

      case "workflow_complete":
        return (
          <div
            key={message.messageId}
            className="mb-5 p-5 rounded-xl bg-gradient-to-br from-green-50 to-emerald-50 border-2 border-green-300 shadow-sm"
          >
            <div className="flex items-center gap-3">
              <div className="bg-green-600 p-2 rounded-lg">
                <CheckCircle2 className="w-6 h-6 text-white" />
              </div>
              <div className="flex-1">
                <span className="font-bold text-green-900 text-lg block">
                  ✅ Workflow Complete
                </span>
                <span className="text-xs text-green-700 font-medium mt-1 block">
                  🕒 {new Date(message.timestamp).toLocaleTimeString()}
                </span>
              </div>
            </div>
          </div>
        );

      case "error":
        const errorMsg = message as ErrorMessage;
        return (
          <div
            key={message.messageId}
            className="mb-5 p-5 rounded-xl bg-gradient-to-br from-red-50 to-rose-50 border-2 border-red-300 shadow-sm"
          >
            <div className="flex items-start gap-3">
              <div className="bg-red-600 p-2 rounded-lg flex-shrink-0">
                <XCircle className="w-6 h-6 text-white" />
              </div>
              <div className="flex-1">
                <span className="font-bold text-red-900 text-lg block mb-1">❌ Error</span>
                <p className="text-red-800 leading-relaxed">{errorMsg.error}</p>
              </div>
            </div>
          </div>
        );

      case "contract_selection_request":
        const contractMsg = message as ContractSelectionRequestMessage;
        return (
          <div
            key={message.messageId}
            className="mb-5 p-5 rounded-xl bg-gradient-to-br from-purple-50 to-violet-50 border-2 border-purple-300 shadow-sm"
          >
            <div className="flex items-start gap-3">
              <div className="bg-purple-600 p-2 rounded-lg flex-shrink-0">
                <AlertTriangle className="w-6 h-6 text-white" />
              </div>
              <div className="flex-1">
                <span className="font-bold text-purple-900 text-lg block mb-1">
                  📋 Contract Selection Request
                </span>
                <span className="text-xs text-purple-700 font-medium">
                  🕒 {new Date(message.timestamp).toLocaleTimeString()}
                </span>
                <p className="text-sm text-slate-700 mt-2">
                  Please select one of {contractMsg.contracts?.length ?? 0} contracts below.
                </p>
              </div>
            </div>
          </div>
        );

      default:
        return null;
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-100 via-blue-50 to-slate-100">
      <div className="container mx-auto p-6 max-w-6xl">
        {/* Header */}
        <div className="bg-white rounded-xl shadow-lg border border-slate-200 p-8 mb-6">
          <div className="flex items-start justify-between">
            <div className="flex-1">
              <div className="flex items-center gap-3 mb-3">
                <div className="bg-gradient-to-br from-blue-600 to-indigo-600 text-white p-3 rounded-lg shadow-md">
                  <svg xmlns="http://www.w3.org/2000/svg" className="h-8 w-8" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </div>
                <div>
                  <h1 className="text-3xl font-bold text-slate-900 leading-tight">
                    Advanced Workflow System
                  </h1>
                  <p className="text-lg text-slate-600 mt-1">
                    AI-Powered Contract Review & Negotiation Platform
                  </p>
                </div>
              </div>
              <div className="flex items-center gap-3 mt-4 text-sm">
                <div className="flex items-center gap-2 px-3 py-1.5 bg-slate-50 rounded-lg border border-slate-200">
                  <span className="text-slate-600 font-medium">Status:</span>
                  <div className="flex items-center gap-1.5">
                    <div
                      className={cn(
                        "w-2.5 h-2.5 rounded-full animate-pulse",
                        connected ? "bg-green-500 shadow-lg shadow-green-500/50" : "bg-red-500 shadow-lg shadow-red-500/50"
                      )}
                    />
                    <span className={cn("font-semibold", connected ? "text-green-700" : "text-red-700")}>
                      {connected ? "Connected" : "Disconnected"}
                    </span>
                  </div>
                </div>
                <div className="flex items-center gap-2 px-3 py-1.5 bg-blue-50 rounded-lg border border-blue-200">
                  <span className="text-blue-700 font-semibold">💼 Contract Review</span>
                  <span className="text-slate-400">→</span>
                  <span className="text-blue-700 font-semibold">🤖 AI Negotiation</span>
                  <span className="text-slate-400">→</span>
                  <span className="text-blue-700 font-semibold">👤 Human Approval</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Messages */}
        <div className="bg-white rounded-xl shadow-lg border border-slate-200 p-6 mb-6 min-h-[500px] max-h-[650px] overflow-y-auto">
          {messages.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full text-slate-400">
              <div className="bg-slate-100 p-6 rounded-full mb-4">
                <AlertTriangle className="w-16 h-16 text-slate-400" />
              </div>
              <p className="text-xl font-semibold text-slate-600 mb-2">Waiting for Workflow</p>
              <p className="text-sm text-slate-500 bg-slate-50 px-4 py-2 rounded-lg border border-slate-200">
                Start backend: <code className="font-mono text-blue-600">dotnet run -- --websocket</code>
              </p>
            </div>
          ) : (
            messages.map((message) => renderMessage(message))
          )}
          <div ref={messagesEndRef} />
        </div>

        {/* Contract Selection Panel */}
        {pendingContractSelection && (
          <div className="bg-gradient-to-br from-white to-purple-50 rounded-xl shadow-2xl p-8 border-2 border-purple-400 mb-6">
            <div className="flex items-start gap-4 mb-6">
              <div className="bg-purple-600 p-3 rounded-xl shadow-lg flex-shrink-0">
                <AlertTriangle className="w-8 h-8 text-white" />
              </div>
              <div className="flex-1">
                <h3 className="text-2xl font-bold text-slate-900 mb-2">
                  📋 Select a Contract to Evaluate
                </h3>
                <p className="text-slate-700 text-lg">
                  Please choose one of the following contracts for the workflow to analyze:
                </p>
              </div>
            </div>

            <div className="grid gap-5 md:grid-cols-3">
              {pendingContractSelection.contracts?.map((contract) => (
                <div
                  key={contract.index}
                  className="p-5 bg-white rounded-xl border-2 border-slate-200 hover:border-purple-500 hover:shadow-xl transition-all transform hover:-translate-y-1"
                >
                  <div className="mb-4">
                    <h4 className="font-bold text-xl text-slate-900 mb-2">
                      {contract.label}
                    </h4>
                    <p className="text-sm text-purple-700 font-bold bg-purple-50 px-3 py-1 rounded-full inline-block">
                      {contract.supplierName}
                    </p>
                  </div>

                  <div className="space-y-3 mb-4 text-sm">
                    <div className="flex justify-between items-center bg-slate-50 p-2 rounded-lg">
                      <span className="text-slate-600 font-medium">Value:</span>
                      <span className="font-bold text-slate-900 text-lg">
                        ${contract.contractValue.toLocaleString()}
                      </span>
                    </div>
                    <div className="flex justify-between items-center bg-slate-50 p-2 rounded-lg">
                      <span className="text-slate-600 font-medium">Term:</span>
                      <span className="font-bold text-slate-900">
                        {contract.contractTermMonths} months
                      </span>
                    </div>
                    <div className="flex gap-2 flex-wrap">
                      <span
                        className={cn(
                          "text-xs px-3 py-1.5 rounded-full font-bold border-2",
                          contract.hasPenaltyClause
                            ? "bg-green-100 text-green-700 border-green-300"
                            : "bg-red-100 text-red-700 border-red-300"
                        )}
                      >
                        {contract.hasPenaltyClause ? "✓" : "✗"} Penalty
                      </span>
                      <span
                        className={cn(
                          "text-xs px-3 py-1.5 rounded-full font-bold border-2",
                          contract.hasAutoRenewal
                            ? "bg-yellow-100 text-yellow-700 border-yellow-300"
                            : "bg-slate-100 text-slate-700 border-slate-300"
                        )}
                      >
                        {contract.hasAutoRenewal ? "✓" : "✗"} Auto-Renew
                      </span>
                    </div>
                  </div>

                  <p className="text-xs text-slate-600 mb-4 line-clamp-2 leading-relaxed">
                    {contract.description}
                  </p>

                  <button
                    onClick={() => handleContractSelection(contract.index)}
                    className="w-full bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-700 hover:to-indigo-700 text-white font-bold py-3 px-4 rounded-xl transition-all shadow-md hover:shadow-lg transform hover:scale-105"
                  >
                    Select Contract {contract.index + 1}
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* HITL Approval Panel */}
        {pendingHitl && (
          <div className="bg-gradient-to-br from-yellow-50 to-amber-50 rounded-xl shadow-2xl p-8 border-4 border-yellow-400 animate-pulse mb-6">
            <div className="flex items-start gap-4 mb-6">
              <div className="bg-yellow-600 p-4 rounded-xl shadow-lg flex-shrink-0">
                <AlertTriangle className="w-10 h-10 text-white" />
              </div>
              <div className="flex-1">
                <h3 className="text-3xl font-bold text-slate-900 mb-3 flex items-center gap-2">
                  👤 Human Approval Required
                </h3>
                <div className="bg-white/80 backdrop-blur-sm p-5 rounded-xl border border-yellow-300 mb-5 shadow-sm">
                  <p className="text-slate-800 whitespace-pre-wrap leading-relaxed text-lg">
                    {pendingHitl.promptMessage}
                  </p>
                </div>

                {pendingHitl.contractInfo && (
                  <div className="mb-5 p-5 bg-white rounded-xl border-2 border-slate-200 shadow-sm">
                    <h4 className="font-bold text-lg mb-3 text-slate-900">📄 Contract Information:</h4>
                    <pre className="text-sm text-slate-700 whitespace-pre-wrap overflow-x-auto bg-slate-50 p-4 rounded-lg border border-slate-200">
                      {JSON.stringify(pendingHitl.contractInfo, null, 2)}
                    </pre>
                  </div>
                )}

                <div className="grid grid-cols-2 gap-4">
                  <button
                    onClick={() => handleHitlResponse(true)}
                    className="bg-gradient-to-r from-green-600 to-emerald-600 hover:from-green-700 hover:to-emerald-700 text-white font-bold py-4 px-8 rounded-xl transition-all shadow-lg hover:shadow-xl flex items-center justify-center gap-3 transform hover:scale-105"
                  >
                    <CheckCircle2 className="w-6 h-6" />
                    <span className="text-xl">Approve (Y)</span>
                  </button>
                  <button
                    onClick={() => handleHitlResponse(false)}
                    className="bg-gradient-to-r from-red-600 to-rose-600 hover:from-red-700 hover:to-rose-700 text-white font-bold py-4 px-8 rounded-xl transition-all shadow-lg hover:shadow-xl flex items-center justify-center gap-3 transform hover:scale-105"
                  >
                    <XCircle className="w-6 h-6" />
                    <span className="text-xl">Reject (N)</span>
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
