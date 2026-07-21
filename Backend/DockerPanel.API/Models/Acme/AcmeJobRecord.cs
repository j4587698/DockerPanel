using TinyDb.Attributes;

namespace DockerPanel.API.Models.Acme
{
    [Entity]
    public class AcmeJobRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        
        public string JobType { get; set; } = string.Empty;
        
        public string Payload { get; set; } = string.Empty;
        
        public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed
        
        public int RetryCount { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public string? ErrorMessage { get; set; }
    }
}
