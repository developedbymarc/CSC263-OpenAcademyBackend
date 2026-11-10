namespace OpenAcademy.Entities;

public enum AccountStatus
{
    Active = 0,
    Inactive = 1,
    Disabled = 2,
}

public enum CourseStatus
{
    Draft = 0,
    Published = 1,
    ModerationHold = 2,
}

public enum ModuleStatus
{
    Draft = 0,
    Published = 1,
    ModerationHold = 2,
}

public enum LessonStatus
{
    Draft = 0,
    Published = 1,
    ModerationHold = 2,
}

public enum EnrollmentStatus
{
    Active = 0,
    Withdrawn = 1
}

public enum AssetType
{
    Image = 0,
    Video = 1
}

public enum AssetStatus
{
    Uploading = 0,
    Ready = 1,
    Failed = 2
}

public enum ReportCreationReason
{
    Profanity = 0,
    InappropriateContent = 1,
    Other = 2,
}

public enum ReportClosureReason
{
    Resolved = 0,
    Spam = 1,
    Other = 2,
}