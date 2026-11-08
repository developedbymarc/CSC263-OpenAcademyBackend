using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using OpenAcademy.Configuration.Options;
using OpenAcademy.Configuration.Validators;
using OpenAcademy.Services.ExternalServices.MediaService;

namespace OpenAcademy.Configuration;

public static class MediaServiceCollectionExtensions
{
    public const string PublicS3Key = "public";

    public static IServiceCollection AddMedia(this IServiceCollection services)
    {
        services.AddOptions<MediaOptions>()
            .BindConfiguration(MediaOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Internal client: uploads/deletes over the private network.
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<S3Options>>().Value;
            return CreateClient(o, o.Endpoint);
        });

        // Signing client: presigned URLs must be signed for the PUBLIC host the browser will call
        // (the signature covers the Host header). GetPreSignedURL does no network I/O.
        services.AddKeyedSingleton<IAmazonS3>(PublicS3Key, (sp, _) =>
        {
            var o = sp.GetRequiredService<IOptions<S3Options>>().Value;
            return CreateClient(o, o.PublicEndpoint ?? o.Endpoint);
        });

        services.AddSingleton<IValidateOptions<S3Options>, S3OptionsValidator>();

        services.AddScoped<IMediaService, MediaService>();
        return services;
    }

    private static AmazonS3Client CreateClient(S3Options o, string endpoint) =>
        new(new BasicAWSCredentials(o.AccessKey, o.SecretAccessKey), new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = o.Region,
            ForcePathStyle = o.ForcePathStyle,
            // Recent SDKs add checksum trailers by default, which many S3-compatible stores reject.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        });
}