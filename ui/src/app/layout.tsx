import type { Metadata } from "next";
import "./globals.css";
import { Navigation } from "@/components/Navigation";

export const metadata: Metadata = {
  title: "AskAI - Agent Workflow Demos",
  description: "Contract Review → AI Negotiation → Human Approval Process",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="ja">
      <body className="antialiased">
        <Navigation />
        {children}
      </body>
    </html>
  );
}
