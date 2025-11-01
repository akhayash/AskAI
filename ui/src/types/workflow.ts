export interface WorkflowMessage {
  type: string;
  timestamp: string;
  messageId: string;
}

export interface AgentUtteranceMessage extends WorkflowMessage {
  type: "agent_utterance";
  agentName: string;
  content: string;
  phase?: string;
  riskScore?: number;
}

export interface FinalResponseMessage extends WorkflowMessage {
  type: "final_response";
  decision: any;
  summary: string;
}

export interface HITLRequestMessage extends WorkflowMessage {
  type: "hitl_request";
  approvalType: string;
  contractInfo: any;
  riskAssessment: any;
  promptMessage: string;
}

export interface WorkflowStartMessage extends WorkflowMessage {
  type: "workflow_start";
  contractInfo: any;
}

export interface WorkflowCompleteMessage extends WorkflowMessage {
  type: "workflow_complete";
  finalDecision: any;
}

export interface ErrorMessage extends WorkflowMessage {
  type: "error";
  error: string;
  details?: string;
}

export type Message = 
  | AgentUtteranceMessage
  | FinalResponseMessage
  | HITLRequestMessage
  | WorkflowStartMessage
  | WorkflowCompleteMessage
  | ErrorMessage;
