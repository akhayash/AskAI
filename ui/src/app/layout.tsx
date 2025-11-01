import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Advanced Conditional Workflow Demo",
  description: "Contract Review → AI Negotiation → Human Approval Process",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="antialiased">
        {children}
      </body>
    </html>
  );
}
