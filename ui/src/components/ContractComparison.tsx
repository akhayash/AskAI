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
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4 mb-4">
        <div className="flex items-center gap-2 text-blue-700">
          <AlertCircle className="w-5 h-5" />
          <span className="font-semibold">契約条件に変更はありません</span>
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
        className={`flex items-center gap-4 p-3 rounded ${
          changed ? "bg-yellow-50 border border-yellow-300" : "bg-slate-50"
        }`}
      >
        <div className="flex-1">
          <div className="text-xs font-semibold text-slate-600 mb-1">
            {label}
          </div>
          <div className="flex items-center gap-2">
            <span
              className={
                changed
                  ? "line-through text-slate-400"
                  : "text-slate-900 font-medium"
              }
            >
              {format(origValue)}
            </span>
            {changed && (
              <>
                <ArrowRight className="w-4 h-4 text-yellow-600" />
                <span className="text-slate-900 font-bold">
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
    <div className="bg-white border border-slate-200 rounded-lg p-4 mb-4">
      <h4 className="text-lg font-bold text-slate-900 mb-3">
        📊 契約条件の比較 (交渉前 → 交渉後)
      </h4>
      <div className="space-y-2">
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
