"""把硬编码的 GenPdfQuestions（约1290行）替换为从 data/pdf_questions.json 运行时加载。"""
SRC = r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\Application\Evaluation\GatewayVsLlmEvaluationService.cs"

NEW_METHOD = '''    // ==================== PDF 真题题目（来自2026一化儿100讲，qwen3-vl-plus 提取） ====================
    // 题目从 data/pdf_questions.json 运行时加载
    private static List<QaTestCase> GenPdfQuestions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "pdf_questions.json");
        if (!File.Exists(path))
            return new List<QaTestCase>();
        try
        {
            var json = File.ReadAllText(path);
            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var items = System.Text.Json.JsonSerializer.Deserialize<List<PdfQuestionItem>>(json, opts);
            if (items == null) return new List<QaTestCase>();
            return items.Select(x => new QaTestCase
            {
                Id = x.Id ?? string.Empty,
                Question = x.Question ?? string.Empty,
                AnswerKeywords = x.AnswerKeywords ?? new List<string>(),
                Note = x.Note ?? string.Empty
            }).ToList();
        }
        catch
        {
            return new List<QaTestCase>();
        }
    }

    private sealed class PdfQuestionItem
    {
        public string? Id { get; set; }
        public string? Question { get; set; }
        public List<string>? AnswerKeywords { get; set; }
        public string? Note { get; set; }
        public string? Type { get; set; }
    }
}
'''

with open(SRC, "r", encoding="utf-8-sig") as f:
    lines = f.readlines()

# 验证边界：保留前 1128 行（行1..1128），行1127=BuildTestCases结束'}', 行1128=空行
print(f"total lines: {len(lines)}")
print(f"line 1126 (idx1125): {lines[1125]!r}")
print(f"line 1127 (idx1126): {lines[1126]!r}")
print(f"line 1128 (idx1127): {lines[1127]!r}")
print(f"line 1129 (idx1128): {lines[1128]!r}")
print(f"line 1130 (idx1129): {lines[1129]!r}")

assert lines[1126].strip() == "}", "line 1127 should be BuildTestCases closing brace"
assert "GenPdfQuestions" in lines[1129], "line 1130 should be GenPdfQuestions signature"

keep = lines[:1128]  # 行1..1128
with open(SRC, "w", encoding="utf-8-sig") as f:
    f.writelines(keep)
    f.write(NEW_METHOD)
print(f"rewritten OK: kept {len(keep)} lines + new method")
