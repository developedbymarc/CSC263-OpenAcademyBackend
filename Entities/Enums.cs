namespace OpenAcademy.Entities;

public enum AccountStatus
{
    Active,
    Inactive,
    Disabled,
}

public enum CourseStatus
{
    // Draft MUST stay first: default(CourseStatus) is what a forgotten assignment produces,
    // and an accidental Draft is harmless while an accidental Published is not.
    Draft,
    Published
}

public enum EnrollmentStatus
{
    Active,
    Withdrawn
}

public enum AssetType
{
    Image,
    Video
}

public enum AssetStatus
{
    Uploading,
    Ready,
    Failed
}

public enum ReportCreationReason
{
    Profanity,
    InappropriateContent,
    Other,
}

public enum ReportClosureReason
{
    Resolved,
    Spam,
    Other,
}