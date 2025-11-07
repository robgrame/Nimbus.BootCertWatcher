using System;

namespace SecureBootDashboard.Api.Data
{
    /// <summary>
    /// Entity for storing certificate compliance policies in the database.
    /// </summary>
    public sealed class PolicyEntity
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsEnabled { get; set; }

        public int Priority { get; set; }

        public string? FleetId { get; set; }

        /// <summary>
        /// JSON-serialized policy rules.
        /// </summary>
        public string RulesJson { get; set; } = string.Empty;

        public DateTimeOffset CreatedAtUtc { get; set; }

        public DateTimeOffset? ModifiedAtUtc { get; set; }
    }
}
