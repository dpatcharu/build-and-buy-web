namespace BuildAndBuy.Web.Models
{
    public class AiRequestDto
    {
        public string Prompt { get; set; } = string.Empty;
    }

    public class AiResponseDto
    {
        public string Result { get; set; } = string.Empty;
    }
}
