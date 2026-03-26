using ViT.SaveKit.Abstractions;
    /// <summary>
    /// Account context that exposes the current save user id.
    /// </summary>
    public sealed class AccountContext : IAccountContext
    {
        public string UserId { get; private set; }

        public AccountContext(string userId)
        {
            UserId = userId;
        }

        public void SetUserId(string userId)
        {
            UserId = userId;
        }
    }
