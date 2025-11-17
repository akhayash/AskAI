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
 * 注意: serviceAdapterは必須ですが、実際のLLM呼び出しはAG-UIエージェント側で行われます。
 * ダミーのOpenAI設定でも、エージェント経由では問題なく動作します。
 *
 * 参考: https://docs.copilotkit.ai/microsoft-agent-framework/quickstart
 */

// DevUIHostのベースURL
const DEVUI_HOST_URL = process.env.DEVUI_HOST_URL || "http://localhost:5000";

/**
 * POST /api/copilotkit
 *
 * クエリパラメータ:
 * - agent: エージェントID (例: "contract", "sourcing", "spend")
 */
export async function POST(req: NextRequest) {
  try {
    // エージェントIDを取得 (デフォルト: "contract")
    const searchParams = req.nextUrl.searchParams;
    const agentId = searchParams.get("agent") || "contract";

    // AG-UIエンドポイントURL
    const aguiEndpoint = `${DEVUI_HOST_URL}/agents/${agentId}`;

    console.log(
      `[CopilotKit API] ═══════════════════════════════════════════════`
    );
    console.log(`[CopilotKit API] Agent ID: ${agentId}`);
    console.log(`[CopilotKit API] AG-UI Endpoint: ${aguiEndpoint}`);
    console.log(`[CopilotKit API] DevUI Host URL: ${DEVUI_HOST_URL}`);
    console.log(
      `[CopilotKit API] ═══════════════════════════════════════════════`
    );

    // ダミーのOpenAI設定（エージェント経由のため実際には使用されない）
    const openai = new OpenAI({
      apiKey: "dummy-key-not-used",
    });

    const serviceAdapter = new OpenAIAdapter({
      openai,
      model: "gpt-4o", // ダミーモデル名
    });

    // CopilotRuntime を作成し、HttpAgent を使用してAG-UIに接続
    const runtime = new CopilotRuntime({
      agents: {
        [agentId]: new HttpAgent({ url: aguiEndpoint }),
      },
    });

    // CopilotKitのNext.js App Router統合を使用してリクエストを処理
    const { handleRequest } = copilotRuntimeNextJSAppRouterEndpoint({
      runtime,
      serviceAdapter,
      endpoint: "/api/copilotkit",
    });

    return handleRequest(req);
  } catch (error) {
    console.error("[CopilotKit API] Error:", error);
    console.error(
      "[CopilotKit API] Error details:",
      error instanceof Error ? error.message : String(error)
    );
    return new Response(
      JSON.stringify({
        error: "Failed to connect to AG-UI endpoint",
        details: error instanceof Error ? error.message : String(error),
      }),
      {
        status: 500,
        headers: { "Content-Type": "application/json" },
      }
    );
  }
}
