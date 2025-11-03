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

export interface ContractInfo {
  supplier_name: string;
  contract_value: number;
  contract_term_months: number;
  payment_terms: string;
  delivery_terms: string;
  warranty_period_months: number;
  penalty_clause: boolean;
  auto_renewal: boolean;
  description?: string;
}

export interface FinalDecision {
  decision: string;
  contract_info: ContractInfo;
  original_contract_info?: ContractInfo;
  final_risk_score: number;
  original_risk_score?: number;
  decision_summary: string;
  next_actions?: string[];
  negotiation_history?: any[];
  evaluation_history?: any[];
}

export interface FinalResponseMessage extends WorkflowMessage {
  type: "final_response";
  decision: FinalDecision;
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
  finalDecision: FinalDecision;
}

export interface ErrorMessage extends WorkflowMessage {
  type: "error";
  error: string;
  details?: string;
}

export interface ContractOption {
  index: number;
  label: string;
  supplierName: string;
  contractValue: number;
  contractTermMonths: number;
  hasPenaltyClause: boolean;
  hasAutoRenewal: boolean;
  description: string;
}

export interface ContractSelectionRequestMessage extends WorkflowMessage {
  type: "contract_selection_request";
  contracts: ContractOption[];
}

export type Message =
  | AgentUtteranceMessage
  | FinalResponseMessage
  | HITLRequestMessage
  | WorkflowStartMessage
  | WorkflowCompleteMessage
  | ErrorMessage
  | ContractSelectionRequestMessage;
