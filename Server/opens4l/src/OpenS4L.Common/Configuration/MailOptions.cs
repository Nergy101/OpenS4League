namespace OpenS4L.Common.Configuration
{
    /// <summary>Mailbox / note settings (chat server).</summary>
    public class MailOptions
    {
        /// <summary>Max note title length.</summary>
        public int MaxTitleLength { get; set; } = 100;

        /// <summary>Max note message length.</summary>
        public int MaxMessageLength { get; set; } = 112;

        /// <summary>Days until a mail expires after it was sent.</summary>
        public int ExpiryDays { get; set; } = 30;
    }
}
