namespace BuildAndBuy.Web.Models
{
    public class AiPlanRequestDto
    {
        public string Prompt { get; set; } = string.Empty;
    }

    public class AiPlanDto
    {
        public string Title { get; set; } = "DIY Plan";
        public int? Difficulty { get; set; }  // 1..5
        public int? TimeMinutes { get; set; }
        public string? BudgetNote { get; set; }
        public List<string> Steps { get; set; } = new();
        public List<MaterialItemDto> Materials { get; set; } = new();
        public List<string> Safety { get; set; } = new();

        // Used by the "Regenerate" button
        public string? OriginalPrompt { get; set; }

        // NEW: Friendly error message (null when OK)
        public string? Error { get; set; }
    }

    public class MaterialItemDto
    {
        public string Name { get; set; } = "";
        public string? Specs { get; set; }
        public string? Link { get; set; }   // optional for now
    }
}
