"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";

export function Navigation() {
  const pathname = usePathname();

  const links = [
    { href: "/", label: "WebSocket Demo" },
    { href: "/copilotkit", label: "CopilotKit Demo" },
  ];

  return (
    <nav className="bg-slate-800 text-white p-3 shadow-lg">
      <div className="container mx-auto flex items-center gap-4">
        <div className="font-bold text-lg">AskAI Demos</div>
        <div className="flex gap-2">
          {links.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className={cn(
                "px-4 py-2 rounded-lg text-sm font-medium transition-colors",
                pathname === link.href
                  ? "bg-blue-600 text-white"
                  : "bg-slate-700 text-slate-200 hover:bg-slate-600"
              )}
            >
              {link.label}
            </Link>
          ))}
        </div>
      </div>
    </nav>
  );
}
