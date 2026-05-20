namespace GameAuth.Models;

// this is the main reason i have this table. can expand it for other change operations
public enum TokenPurpose{
    EmailVerification,
    PasswordReset
}

public class Token
{
    public int ID { get; set; }
    public int UserID { get; set; }
    public User User { get; set; } = null!;
    public TokenPurpose Purpose { get; set; }
    public string TokenHash { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }

    public bool IsActive => ConsumedAt == null && ExpiresAt>DateTime.UtcNow;
}
