import React from "react";
import { ContractInfo } from "@/types/workflow";
import { ArrowRight, AlertCircle } from "lucide-react";

interface ContractComparisonProps {
  original: ContractInfo;
  final: ContractInfo;
}

export function ContractComparison({
  original,
  final,
}: ContractComparisonProps) {
  const hasChanges = JSON.stringify(original) !== JSON.stringify(final);

  if (!hasChanges) {
    return (
      <div className="bg-gradient-to-br from-blue-50 to-indigo-50 border-2 border-blue-300 rounded-xl p-5 mb-5 shadow-sm">
        <div className="flex items-center gap-3 text-blue-700">
          <div className="bg-blue-600 p-2 rounded-lg">
            <AlertCircle className="w-6 h-6 text-white" />
          </div>
          <span className="font-bold text-lg">契約条件に変更はありません</span>
        </div>
      </div>
    );
  }

  const compareField = (
    field: keyof ContractInfo,
    label: string,
    formatter?: (val: any) => string
  ) => {
    const origValue = original[field];
    const finalValue = final[field];
    const changed = origValue !== finalValue;

    const format = formatter || ((val: any) => String(val));

    return (
      <div
        className={`flex items-center gap-4 p-4 rounded-xl shadow-sm ${
          changed 
            ? "bg-gradient-to-br from-yellow-50 to-amber-50 border-2 border-yellow-400" 
            : "bg-slate-50 border border-slate-200"
        }`}
      >
        <div className="flex-1">
          <div className="text-xs font-bold text-slate-600 mb-2 uppercase tracking-wide">
            {label}
          </div>
          <div className="flex items-center gap-3">
            <span
              className={
                changed
                  ? "line-through text-slate-400 font-medium"
                  : "text-slate-900 font-bold text-lg"
              }
            >
              {format(origValue)}
            </span>
            {changed && (
              <>
                <div className="bg-yellow-600 p-1 rounded">
                  <ArrowRight className="w-5 h-5 text-white" />
                </div>
                <span className="text-slate-900 font-bold text-lg bg-white px-3 py-1 rounded-lg border-2 border-yellow-500">
                  {format(finalValue)}
                </span>
              </>
            )}
          </div>
        </div>
      </div>
    );
  };

  return (
    <div className="bg-white border-2 border-slate-300 rounded-xl p-6 mb-5 shadow-md">
      <h4 className="text-xl font-bold text-slate-900 mb-4 flex items-center gap-2">
        <span className="bg-indigo-100 text-indigo-700 px-3 py-1 rounded-full text-sm">
          📊 契約条件の比較 (交渉前 → 交渉後)
        </span>
      </h4>
      <div className="space-y-3">
        {compareField("supplier_name", "サプライヤー")}
        {compareField(
          "contract_value",
          "契約金額",
          (val) => `$${val.toLocaleString()}`
        )}
        {compareField(
          "contract_term_months",
          "契約期間",
          (val) => `${val}ヶ月`
        )}
        {compareField("payment_terms", "支払条件")}
        {compareField("delivery_terms", "納品条件")}
        {compareField(
          "warranty_period_months",
          "保証期間",
          (val) => `${val}ヶ月`
        )}
        {compareField("penalty_clause", "ペナルティ条項", (val) =>
          val ? "あり" : "なし"
        )}
        {compareField("auto_renewal", "自動更新", (val) =>
          val ? "あり" : "なし"
        )}
      </div>
    </div>
  );
}
