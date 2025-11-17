import {
  CopilotRuntime,
  OpenAIAdapter,
  copilotRuntimeNextJSAppRouterEndpoint,
} from "@copilotkit/runtime";
import { HttpAgent } from "@ag-ui/client";
import { NextRequest } from "next/server";
import OpenAI from "openai";

/**
 * CopilotKit Runtime API Route for Microsoft Agent Framework (AG-UI)
 *
 * このルートは、CopilotKitとDevUIHost (AG-UI) を接続します。
 *
 * 注意: serviceAdapterは必須です。実際のLLM呼び出しはAG-UIエージェント側で行われるため、
 * 有効なAPIキーがなくても動作しますが、CopilotKitの要件として設定が必要です。
 *
 * 参考: https://docs.copilotkit.ai/microsoft-agent-framework/quickstart
 */

const DEVUI_HOST_URL = process.env.DEVUI_HOST_URL || "http://localhost:5000";
const DEFAULT_AGENT_ID = "contract";

/**
 * POST /api/copilotkit
 *
 * クエリパラメータ:
 * - agent: エージェントID (例: "contract", "sourcing", "spend")
 */
export async function POST(req: NextRequest) {
  try {
    const searchParams = req.nextUrl.searchParams;
    const agentId = searchParams.get("agent") || DEFAULT_AGENT_ID;
    const aguiEndpoint = `${DEVUI_HOST_URL}/agents/${agentId}`;

    console.log(`[CopilotKit API] Connecting to agent: ${agentId} at ${aguiEndpoint}`);

    // OpenAI adapter (required by CopilotKit, but actual LLM calls handled by AG-UI)
    const openai = new OpenAI({
      apiKey: "dummy-key-not-used",
    });

    const serviceAdapter = new OpenAIAdapter({
      openai,
      model: "gpt-4o",
    });

    const runtime = new CopilotRuntime({
      agents: {
        [agentId]: new HttpAgent({ url: aguiEndpoint }),
      },
    });

    const { handleRequest } = copilotRuntimeNextJSAppRouterEndpoint({
      runtime,
      serviceAdapter,
      endpoint: "/api/copilotkit",
    });

    return handleRequest(req);
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : String(error);
    console.error(`[CopilotKit API] Error: ${errorMessage}`, error);
    return new Response(
      JSON.stringify({
        error: "Failed to connect to AG-UI endpoint",
        details: errorMessage,
      }),
      {
        status: 500,
        headers: { "Content-Type": "application/json" },
      }
    );
  }
}
