# Advanced Conditional Workflow UI

Next.js-based UI for the AdvancedConditionalWorkflow demo with real-time WebSocket communication.

## Features

- ✨ Real-time workflow visualization via WebSocket
- 🔄 Intermediate agent utterances displayed separately from final responses
- 👤 Interactive HITL (Human-in-the-Loop) approval UI
- 🎨 Modern UI with Tailwind CSS
- 📊 Risk score visualization with color coding
- ⚡ Built with Next.js 16 and TypeScript

## Prerequisites

- Node.js 20.x or later
- npm 10.x or later
- .NET 8 SDK (for running the backend)

## Installation

```bash
npm install
```

## Development

1. Start the backend workflow in WebSocket mode:

```bash
cd ../src/AdvancedConditionalWorkflow
dotnet run -- --websocket
```

2. In a separate terminal, start the Next.js development server:

```bash
npm run dev
```

3. Open [http://localhost:3000](http://localhost:3000) in your browser

## Production Build

```bash
npm run build
npm run start
```

## How It Works

### WebSocket Communication

The UI connects to the backend WebSocket server (running on `ws://localhost:8080`) and receives real-time messages:

- **agent_utterance**: Intermediate messages from specialist agents (Legal, Finance, Procurement, etc.)
- **final_response**: Final decision after workflow completion
- **hitl_request**: Request for human approval
- **workflow_start**: Workflow execution started
- **workflow_complete**: Workflow execution completed
- **error**: Error messages

### Message Types

#### Agent Utterance
Shows agent name, phase, risk score, and content. Color-coded by risk level:
- 🟢 Green: Low risk (≤30)
- 🟡 Yellow: Medium risk (31-70)
- 🔴 Red: High risk (>70)

#### Final Response
Displayed with a special highlighted card showing the final decision and complete JSON data.

#### HITL Approval
Interactive approval panel with Approve/Reject buttons. Sends response back to backend via WebSocket.

## Architecture

```
ui/
├── src/
│   ├── app/
│   │   ├── layout.tsx       # Root layout
│   │   ├── page.tsx         # Main chat interface
│   │   └── globals.css      # Global styles
│   ├── lib/
│   │   └── utils.ts         # Utility functions (cn helper)
│   └── types/
│       └── workflow.ts      # TypeScript types for messages
├── package.json
└── README.md
```

## Technologies

- **Next.js 16**: React framework with App Router
- **TypeScript**: Type-safe development
- **Tailwind CSS**: Utility-first CSS framework
- **lucide-react**: Icon library
- **WebSocket API**: Real-time bidirectional communication

## Clean Architecture

The UI follows clean architecture principles:

- **Separation of Concerns**: UI logic separated from business logic (in backend)
- **Type Safety**: Strong typing with TypeScript interfaces
- **Reusable Components**: Modular message rendering
- **Real-time Communication**: WebSocket abstraction layer

## Related Documentation

- [AdvancedConditionalWorkflow Backend](../src/AdvancedConditionalWorkflow/README.md)
- [Common WebSocket Infrastructure](../src/Common/WebSocket/)
- [Main Project README](../README.md)
