using System;
using System.Collections.Generic;
using System.Linq;
using CloudNative.CloudEvents;
using CloudEventValidation = CloudNative.CloudEvents.Core.Validation;

namespace Motor.Extensions.Hosting.CloudEvents
{
    public static class MotorVersionExtension
    {
        public static CloudEventAttribute MotorVersionAttribute { get; } =
            CloudEventAttribute.CreateExtension("motorversion", CloudEventAttributeType.String);

        public static IEnumerable<CloudEventAttribute> AllAttributes { get; } =
            new[] { MotorVersionAttribute }.ToList().AsReadOnly();

        private static readonly string? CurrentVersion = typeof(MotorVersionExtension).Assembly.GetName().Version?.ToString();

        public static MotorCloudEvent<TData> SetMotorVersion<TData>(this MotorCloudEvent<TData> cloudEvent)
            where TData : class
        {
            CloudEventValidation.CheckNotNull(cloudEvent, nameof(cloudEvent));
            cloudEvent[MotorVersionAttribute] =
                CurrentVersion ?? throw new InvalidOperationException("Motor.NET version is undefined.");
            return cloudEvent;
        }

        public static Version? GetMotorVersion<TData>(this MotorCloudEvent<TData> cloudEvent) where TData : class
        {
            return CloudEventValidation.CheckNotNull(cloudEvent, nameof(cloudEvent))[MotorVersionAttribute]
                is not string versionString
                ? null
                : System.Version.Parse(versionString);
        }
    }
}
