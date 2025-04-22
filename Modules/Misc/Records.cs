
using System.Text.Json.Serialization;

namespace MegaBulkUploader.Modules.Misc
{
    public static class Records
    {
        public record EmailData(
            [property: JsonPropertyName("@context")] string Context,
            [property: JsonPropertyName("@id")] string InternalId,
            [property: JsonPropertyName("@type")] string Type,
            [property: JsonPropertyName("id")] string Id,
            [property: JsonPropertyName("msgid")] string Msgid,
            [property: JsonPropertyName("from")] EmailFrom From,
            [property: JsonPropertyName("to")] IReadOnlyList<EmailTo> To,
            [property: JsonPropertyName("cc")] IReadOnlyList<object> Cc,
            [property: JsonPropertyName("bcc")] IReadOnlyList<object> Bcc,
            [property: JsonPropertyName("subject")] string Subject,
            [property: JsonPropertyName("intro")] string Intro,
            [property: JsonPropertyName("seen")] bool? Seen,
            [property: JsonPropertyName("flagged")] bool? Flagged,
            [property: JsonPropertyName("isDeleted")] bool? IsDeleted,
            [property: JsonPropertyName("verifications")] Verifications Verifications,
            [property: JsonPropertyName("retention")] bool? Retention,
            [property: JsonPropertyName("retentionDate")] DateTime? RetentionDate,
            [property: JsonPropertyName("text")] string Text,
            [property: JsonPropertyName("html")] IReadOnlyList<string> Html,
            [property: JsonPropertyName("hasAttachments")] bool? HasAttachments,
            [property: JsonPropertyName("size")] int? Size,
            [property: JsonPropertyName("downloadUrl")] string DownloadUrl,
            [property: JsonPropertyName("sourceUrl")] string SourceUrl,
            [property: JsonPropertyName("createdAt")] DateTime? CreatedAt,
            [property: JsonPropertyName("updatedAt")] DateTime? UpdatedAt,
            [property: JsonPropertyName("accountId")] string AccountId
        );

        public record Tls(
            [property: JsonPropertyName("name")] string Name,
            [property: JsonPropertyName("standardName")] string StandardName,
            [property: JsonPropertyName("version")] string Version
        );

        public record Verifications(
            [property: JsonPropertyName("tls")] Tls Tls,
            [property: JsonPropertyName("spf")] bool? Spf,
            [property: JsonPropertyName("dkim")] bool? Dkim
        );


        public record EmailFrom(
            [property: JsonPropertyName("address")] string Address,
            [property: JsonPropertyName("name")] string Name
        );

        public record EmailHydraMember(
            [property: JsonPropertyName("@id")] string InternalId,
            [property: JsonPropertyName("@type")] string Type,
            [property: JsonPropertyName("id")] string Id,
            [property: JsonPropertyName("msgid")] string Msgid,
            [property: JsonPropertyName("from")] EmailFrom From,
            [property: JsonPropertyName("to")] IReadOnlyList<EmailTo> To,
            [property: JsonPropertyName("subject")] string Subject,
            [property: JsonPropertyName("intro")] string Intro,
            [property: JsonPropertyName("seen")] bool? Seen,
            [property: JsonPropertyName("isDeleted")] bool? IsDeleted,
            [property: JsonPropertyName("hasAttachments")] bool? HasAttachments,
            [property: JsonPropertyName("size")] int? Size,
            [property: JsonPropertyName("downloadUrl")] string DownloadUrl,
            [property: JsonPropertyName("sourceUrl")] string SourceUrl,
            [property: JsonPropertyName("createdAt")] DateTime? CreatedAt,
            [property: JsonPropertyName("updatedAt")] DateTime? UpdatedAt,
            [property: JsonPropertyName("accountId")] string AccountId
        );

        public record Emails(
            [property: JsonPropertyName("@context")] string Context,
            [property: JsonPropertyName("@id")] string EmailTo,
            [property: JsonPropertyName("@type")] string Type,
            [property: JsonPropertyName("hydra:totalItems")] int? HydraTotalItems,
            [property: JsonPropertyName("hydra:member")] IReadOnlyList<EmailHydraMember> HydraMember
        );

        public record EmailTo(
            [property: JsonPropertyName("address")] string Address,
            [property: JsonPropertyName("name")] string Name
        );
        
        public record HydraMember(
            [property: JsonPropertyName("@id")] string DomainId,
            [property: JsonPropertyName("@type")] string Type,
            [property: JsonPropertyName("id")] string Id,
            [property: JsonPropertyName("domain")] string Domain,
            [property: JsonPropertyName("isActive")] bool? IsActive,
            [property: JsonPropertyName("isPrivate")] bool? IsPrivate,
            [property: JsonPropertyName("createdAt")] DateTime? CreatedAt,
            [property: JsonPropertyName("updatedAt")] DateTime? UpdatedAt
        );

        public record Domains(
            [property: JsonPropertyName("@context")] string Context,
            [property: JsonPropertyName("@id")] string Id,
            [property: JsonPropertyName("@type")] string Type,
            [property: JsonPropertyName("hydra:totalItems")] int? HydraTotalItems,
            [property: JsonPropertyName("hydra:member")] IReadOnlyList<HydraMember> HydraMember
        );

        public record SignUp(
            [property: JsonPropertyName("@context")] string Context,
            [property: JsonPropertyName("@id")] string SignId,
            [property: JsonPropertyName("@type")] string Type,
            [property: JsonPropertyName("id")] string Id,
            [property: JsonPropertyName("address")] string Address,
            [property: JsonPropertyName("quota")] int? Quota,
            [property: JsonPropertyName("used")] int? Used,
            [property: JsonPropertyName("isDisabled")] bool? IsDisabled,
            [property: JsonPropertyName("isDeleted")] bool? IsDeleted,
            [property: JsonPropertyName("createdAt")] DateTime? CreatedAt,
            [property: JsonPropertyName("updatedAt")] DateTime? UpdatedAt
        );

        public record TokenData(
            [property: JsonPropertyName("id")] string Id,
            [property: JsonPropertyName("token")] string Token
        );

    }
}
