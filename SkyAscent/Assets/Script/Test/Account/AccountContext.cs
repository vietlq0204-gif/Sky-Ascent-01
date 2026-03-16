using Save.Abstractions;

namespace Account
{
    /// <summary>
    /// Account context đơn giản: cung cấp UserId.
    /// </summary>
    /// <remarks>
    /// - Module khác có thể set UserId sau login.
    /// - SaveSystem chỉ phụ thuộc interface IAccountContext.
    /// </remarks>
    public sealed class AccountContext : IAccountContext
    {
        public string UserId { get; private set; }

        public AccountContext(string userId)
        {
            UserId = userId;
        }

        /// <summary>
        /// Update userId khi user đổi account.
        /// </summary>
        public void SetUserId(string userId)
        {
            UserId = userId;
        }
    }
}
