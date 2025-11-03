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
            className="mb-4 p-4 rounded-lg bg-slate-50 border border-slate-200"
          >
            <div className="flex items-start gap-3">
              <div className="flex-shrink-0 w-10 h-10 rounded-full bg-blue-500 flex items-center justify-center text-white font-semibold">
                {agentMsg.agentName.charAt(0)}
              </div>
              <div className="flex-1">
                <div className="flex items-center gap-2 mb-1">
                  <span className="font-semibold text-slate-900">
                    {agentMsg.agentName}
                  </span>
                  {agentMsg.phase && (
                    <span className="text-xs px-2 py-1 rounded bg-blue-100 text-blue-700">
                      {agentMsg.phase}
                    </span>
                  )}
                  {agentMsg.riskScore !== undefined && (
                    <span
                      className={cn(
                        "text-xs px-2 py-1 rounded font-medium",
                        agentMsg.riskScore <= 30
                          ? "bg-green-100 text-green-700"
                          : agentMsg.riskScore <= 70
                          ? "bg-yellow-100 text-yellow-700"
                          : "bg-red-100 text-red-700"
                      )}
                    >
                      Risk: {agentMsg.riskScore}/100
                    </span>
                  )}
                </div>
                <p className="text-slate-700 whitespace-pre-wrap">
                  {agentMsg.content}
                </p>
                <span className="text-xs text-slate-500 mt-1 block">
                  {new Date(message.timestamp).toLocaleTimeString()}
                </span>
              </div>
            </div>
          </div>
        );

      case "final_response":
        const finalMsg = message as FinalResponseMessage;
        const hasNegotiation =
          finalMsg.decision?.original_contract_info &&
          finalMsg.decision?.negotiation_history?.length > 0;

        return (
          <div
            key={message.messageId}
            className="mb-4 p-6 rounded-lg bg-gradient-to-r from-green-50 to-emerald-50 border-2 border-green-300"
          >
            <div className="flex items-start gap-3">
              <CheckCircle2 className="w-8 h-8 text-green-600 flex-shrink-0" />
              <div className="flex-1">
                <h3 className="text-lg font-bold text-green-900 mb-2">
                  🎉 Final Decision
                </h3>
                <p className="text-slate-700 font-medium mb-2">
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
                  <div className="mt-4 bg-white border border-slate-200 rounded-lg p-4">
                    <h4 className="text-md font-bold text-slate-900 mb-2">
                      🔄 交渉履歴 (
                      {finalMsg.decision.negotiation_history.length}回)
                    </h4>
                    <div className="space-y-2">
                      {finalMsg.decision.negotiation_history.map(
                        (negotiation: any, idx: number) => (
                          <div
                            key={idx}
                            className="p-3 bg-slate-50 rounded border border-slate-200"
                          >
                            <div className="font-semibold text-sm text-slate-700 mb-1">
                              反復 {negotiation.iteration}/3
                            </div>
                            <div className="text-sm text-slate-600">
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
                    <div className="mt-4 bg-white border border-green-200 rounded-lg p-4">
                      <h4 className="text-md font-bold text-slate-900 mb-2">
                        📋 Next Actions
                      </h4>
                      <ul className="list-disc list-inside space-y-1">
                        {finalMsg.decision.next_actions.map(
                          (action: string, idx: number) => (
                            <li key={idx} className="text-sm text-slate-700">
                              {action}
                            </li>
                          )
                        )}
                      </ul>
                    </div>
                  )}

                <span className="text-xs text-slate-500 mt-3 block">
                  {new Date(message.timestamp).toLocaleTimeString()}
                </span>
              </div>
            </div>
          </div>
        );

      case "workflow_start":
        return (
          <div
            key={message.messageId}
            className="mb-4 p-4 rounded-lg bg-blue-50 border border-blue-200"
          >
            <div className="flex items-center gap-2">
              <Loader2 className="w-5 h-5 text-blue-600 animate-spin" />
              <span className="font-semibold text-blue-900">
                Workflow Started
              </span>
              <span className="text-xs text-slate-500">
                {new Date(message.timestamp).toLocaleTimeString()}
              </span>
            </div>
          </div>
        );

      case "workflow_complete":
        return (
          <div
            key={message.messageId}
            className="mb-4 p-4 rounded-lg bg-green-50 border border-green-200"
          >
            <div className="flex items-center gap-2">
              <CheckCircle2 className="w-5 h-5 text-green-600" />
              <span className="font-semibold text-green-900">
                Workflow Complete
              </span>
              <span className="text-xs text-slate-500">
                {new Date(message.timestamp).toLocaleTimeString()}
              </span>
            </div>
          </div>
        );

      case "error":
        const errorMsg = message as ErrorMessage;
        return (
          <div
            key={message.messageId}
            className="mb-4 p-4 rounded-lg bg-red-50 border border-red-200"
          >
            <div className="flex items-start gap-2">
              <XCircle className="w-5 h-5 text-red-600 flex-shrink-0 mt-0.5" />
              <div className="flex-1">
                <span className="font-semibold text-red-900 block">Error</span>
                <p className="text-red-700">{errorMsg.error}</p>
              </div>
            </div>
          </div>
        );

      case "contract_selection_request":
        const contractMsg = message as ContractSelectionRequestMessage;
        return (
          <div
            key={message.messageId}
            className="mb-4 p-4 rounded-lg bg-purple-50 border border-purple-200"
          >
            <div className="flex items-center gap-2 mb-2">
              <AlertTriangle className="w-5 h-5 text-purple-600" />
              <span className="font-semibold text-purple-900">
                Contract Selection Request
              </span>
              <span className="text-xs text-slate-500">
                {new Date(message.timestamp).toLocaleTimeString()}
              </span>
            </div>
            <p className="text-sm text-slate-600">
              Please select one of {contractMsg.contracts?.length ?? 0}{" "}
              contracts below.
            </p>
          </div>
        );

      default:
        return null;
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 to-slate-100">
      <div className="container mx-auto p-4 max-w-5xl">
        {/* Header */}
        <div className="bg-white rounded-lg shadow-sm p-6 mb-4">
          <h1 className="text-3xl font-bold text-slate-900 mb-2">
            Advanced Conditional Workflow Demo
          </h1>
          <p className="text-slate-600">
            Contract Review → AI Negotiation → Human Approval Process
          </p>
          <div className="flex items-center gap-2 mt-3">
            <div
              className={cn(
                "w-2 h-2 rounded-full",
                connected ? "bg-green-500" : "bg-red-500"
              )}
            />
            <span className="text-sm text-slate-600">
              {connected ? "Connected to workflow" : "Disconnected"}
            </span>
          </div>
        </div>

        {/* Messages */}
        <div className="bg-white rounded-lg shadow-sm p-6 mb-4 min-h-[500px] max-h-[600px] overflow-y-auto">
          {messages.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full text-slate-400">
              <AlertTriangle className="w-12 h-12 mb-2" />
              <p>Waiting for workflow to start...</p>
              <p className="text-sm mt-2">
                Run the backend with: dotnet run -- --websocket
              </p>
            </div>
          ) : (
            messages.map((message) => renderMessage(message))
          )}
          <div ref={messagesEndRef} />
        </div>

        {/* Contract Selection Panel */}
        {pendingContractSelection && (
          <div className="bg-white rounded-lg shadow-lg p-6 border-2 border-purple-400 mb-4">
            <div className="flex items-start gap-3 mb-4">
              <AlertTriangle className="w-8 h-8 text-purple-600 flex-shrink-0" />
              <div className="flex-1">
                <h3 className="text-xl font-bold text-slate-900 mb-2">
                  📋 Select a Contract to Evaluate
                </h3>
                <p className="text-slate-700 mb-4">
                  Please choose one of the following contracts for the workflow
                  to analyze:
                </p>

                <div className="grid gap-4 md:grid-cols-3">
                  {pendingContractSelection.contracts?.map((contract) => (
                    <div
                      key={contract.index}
                      className="p-4 bg-slate-50 rounded-lg border border-slate-200 hover:border-purple-400 transition-colors"
                    >
                      <div className="mb-3">
                        <h4 className="font-bold text-lg text-slate-900 mb-1">
                          {contract.label}
                        </h4>
                        <p className="text-sm text-slate-600 font-semibold">
                          {contract.supplierName}
                        </p>
                      </div>

                      <div className="space-y-2 mb-4 text-sm">
                        <div className="flex justify-between">
                          <span className="text-slate-600">Value:</span>
                          <span className="font-semibold text-slate-900">
                            ${contract.contractValue.toLocaleString()}
                          </span>
                        </div>
                        <div className="flex justify-between">
                          <span className="text-slate-600">Term:</span>
                          <span className="font-semibold text-slate-900">
                            {contract.contractTermMonths} months
                          </span>
                        </div>
                        <div className="flex gap-2 flex-wrap">
                          <span
                            className={cn(
                              "text-xs px-2 py-1 rounded font-medium",
                              contract.hasPenaltyClause
                                ? "bg-green-100 text-green-700"
                                : "bg-red-100 text-red-700"
                            )}
                          >
                            {contract.hasPenaltyClause ? "✓" : "✗"} Penalty
                          </span>
                          <span
                            className={cn(
                              "text-xs px-2 py-1 rounded font-medium",
                              contract.hasAutoRenewal
                                ? "bg-yellow-100 text-yellow-700"
                                : "bg-slate-100 text-slate-700"
                            )}
                          >
                            {contract.hasAutoRenewal ? "✓" : "✗"} Auto-Renew
                          </span>
                        </div>
                      </div>

                      <p className="text-xs text-slate-600 mb-3 line-clamp-2">
                        {contract.description}
                      </p>

                      <button
                        onClick={() => handleContractSelection(contract.index)}
                        className="w-full bg-purple-600 hover:bg-purple-700 text-white font-semibold py-2 px-4 rounded-lg transition-colors"
                      >
                        Select Contract {contract.index + 1}
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* HITL Approval Panel */}
        {pendingHitl && (
          <div className="bg-white rounded-lg shadow-lg p-6 border-2 border-yellow-400 animate-pulse">
            <div className="flex items-start gap-3 mb-4">
              <AlertTriangle className="w-8 h-8 text-yellow-600 flex-shrink-0" />
              <div className="flex-1">
                <h3 className="text-xl font-bold text-slate-900 mb-2">
                  👤 Human Approval Required
                </h3>
                <p className="text-slate-700 whitespace-pre-wrap mb-4">
                  {pendingHitl.promptMessage}
                </p>

                {pendingHitl.contractInfo && (
                  <div className="mb-4 p-3 bg-slate-50 rounded">
                    <h4 className="font-semibold mb-2">Contract Info:</h4>
                    <pre className="text-sm text-slate-700 whitespace-pre-wrap overflow-x-auto">
                      {JSON.stringify(pendingHitl.contractInfo, null, 2)}
                    </pre>
                  </div>
                )}

                <div className="flex gap-3">
                  <button
                    onClick={() => handleHitlResponse(true)}
                    className="flex-1 bg-green-600 hover:bg-green-700 text-white font-semibold py-3 px-6 rounded-lg transition-colors flex items-center justify-center gap-2"
                  >
                    <CheckCircle2 className="w-5 h-5" />
                    Approve (Y)
                  </button>
                  <button
                    onClick={() => handleHitlResponse(false)}
                    className="flex-1 bg-red-600 hover:bg-red-700 text-white font-semibold py-3 px-6 rounded-lg transition-colors flex items-center justify-center gap-2"
                  >
                    <XCircle className="w-5 h-5" />
                    Reject (N)
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
