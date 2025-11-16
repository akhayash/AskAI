using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using AdvancedConditionalWorkflow.Models;
using System.Text.Json;

namespace DevUIHost.Executors;

/// <summary>
/// ChatMessageからContractInfoを抽出するExecutor
/// JSON形式またはテキスト形式の入力を受け付ける
/// </summary>
public class ChatMessageToContractExecutor : Executor<List<ChatMessage>, ContractInfo>
{
    private readonly ILogger? _logger;
    
    public ChatMessageToContractExecutor(string id, ILogger? logger) : base(id)
    {
        _logger = logger;
    }
    
    public override ValueTask<ContractInfo> HandleAsync(
        List<ChatMessage> messages, 
        IWorkflowContext context, 
        CancellationToken cancellationToken)
    {
        var userMessage = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
        
        _logger?.LogInformation("📝 契約情報をパース中: {MessageLength}文字", userMessage.Length);
        
        // JSONパースを試行 (二重エンコードされたJSONも処理)
        if (!string.IsNullOrWhiteSpace(userMessage) && userMessage.TrimStart().StartsWith("{"))
        {
            try
            {
                string jsonToParse = userMessage;
                
                // まず {"input": "..."} 形式かチェック
                using (var doc = JsonDocument.Parse(userMessage))
                {
                    if (doc.RootElement.TryGetProperty("input", out var inputElement))
                    {
                        // input フィールドの中身（エスケープされたJSON文字列）を取得
                        var innerJson = inputElement.GetString();
                        if (!string.IsNullOrEmpty(innerJson))
                        {
                            _logger?.LogDebug("二重エンコードされたJSONを検出、内部JSONをパース");
                            jsonToParse = innerJson;
                        }
                    }
                }
                
                // PascalCase のJSONを手動でパース (JsonPropertyName属性を無視)
                using (var parsedDoc = JsonDocument.Parse(jsonToParse))
                {
                    var root = parsedDoc.RootElement;
                    
                    // PascalCase プロパティを読み取る
                    var contract = new ContractInfo
                    {
                        SupplierName = root.TryGetProperty("SupplierName", out var sn) ? sn.GetString() ?? "" : 
                                       root.TryGetProperty("supplier_name", out var sn2) ? sn2.GetString() ?? "" : "",
                        ContractValue = root.TryGetProperty("ContractValue", out var cv) ? cv.GetDecimal() :
                                       root.TryGetProperty("contract_value", out var cv2) ? cv2.GetDecimal() : 0,
                        ContractTermMonths = root.TryGetProperty("ContractTermMonths", out var ctm) ? ctm.GetInt32() :
                                            root.TryGetProperty("contract_term_months", out var ctm2) ? ctm2.GetInt32() : 0,
                        PaymentTerms = root.TryGetProperty("PaymentTerms", out var pt) ? pt.GetString() ?? "" :
                                      root.TryGetProperty("payment_terms", out var pt2) ? pt2.GetString() ?? "" : "",
                        DeliveryTerms = root.TryGetProperty("DeliveryTerms", out var dt) ? dt.GetString() ?? "" :
                                       root.TryGetProperty("delivery_terms", out var dt2) ? dt2.GetString() ?? "" : "",
                        WarrantyPeriodMonths = root.TryGetProperty("WarrantyPeriodMonths", out var wpm) ? wpm.GetInt32() :
                                              root.TryGetProperty("warranty_period_months", out var wpm2) ? wpm2.GetInt32() : 0,
                        HasPenaltyClause = root.TryGetProperty("HasPenaltyClause", out var hpc) ? hpc.GetBoolean() :
                                          root.TryGetProperty("penalty_clause", out var hpc2) ? hpc2.GetBoolean() : false,
                        HasAutoRenewal = root.TryGetProperty("HasAutoRenewal", out var har) ? har.GetBoolean() :
                                        root.TryGetProperty("auto_renewal", out var har2) ? har2.GetBoolean() : false,
                        Description = root.TryGetProperty("Description", out var desc) ? desc.GetString() :
                                     root.TryGetProperty("description", out var desc2) ? desc2.GetString() : null
                    };
                    
                    if (!string.IsNullOrEmpty(contract.SupplierName))
                    {
                        _logger?.LogInformation("✅ JSON形式の契約情報をパースしました: {Supplier}", contract.SupplierName);
                        return new ValueTask<ContractInfo>(contract);
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning("⚠️ JSON パースエラー: {Error}", ex.Message);
                // デフォルト契約にフォールバック
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("⚠️ 予期しないエラー: {Error}", ex.Message);
                // デフォルト契約にフォールバック
            }
        }
        
        // JSON形式でない場合はデフォルト契約を作成
        var defaultContract = new ContractInfo
        {
            SupplierName = "Sample Supplier",
            ContractValue = 100000m,
            ContractTermMonths = 12,
            PaymentTerms = "Net 30",
            DeliveryTerms = "FOB Destination",
            WarrantyPeriodMonths = 12,
            HasPenaltyClause = true,
            HasAutoRenewal = false,
            Description = userMessage
        };
        
        _logger?.LogInformation("✅ デフォルト契約情報を作成しました (テキスト入力)");
        return new ValueTask<ContractInfo>(defaultContract);
    }
}
