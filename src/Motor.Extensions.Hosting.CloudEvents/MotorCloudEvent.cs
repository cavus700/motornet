using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using CloudNative.CloudEvents;

namespace Motor.Extensions.Hosting.CloudEvents
{
    public static class MotorCloudEventInfo
    {
        public static CloudEventsSpecVersion SpecVersion => CloudEventsSpecVersion.V1_0;

        public static readonly IEnumerable<CloudEventAttribute> RequiredAttributes = SpecVersion.RequiredAttributes.Concat(new[]{
            SpecVersion.TimeAttribute, SpecVersion.DataContentTypeAttribute
        });
    }

    public class MotorCloudEvent<TData> where TData : class
    {
        private readonly IApplicationNameService _applicationNameService;

        public MotorCloudEvent(
            IApplicationNameService applicationNameService,
            TData data,
            Uri source,
            string? id = null,
            DateTimeOffset? time = null,
            params KeyValuePair<CloudEventAttribute, object>[] extensions)
        {
            BaseEvent = new CloudEvent(CloudEventsSpecVersion.Default);
            foreach (var (key, value) in extensions)
            {
                BaseEvent[key] = value;
            }
            BaseEvent.Id = id ?? Guid.NewGuid().ToString();
            BaseEvent.Type = typeof(TData).Name;
            BaseEvent.Source = source;
            BaseEvent.Time = time ?? DateTimeOffset.UtcNow;
            BaseEvent.DataContentType = new ContentType().ToString();

            _applicationNameService = applicationNameService;
            TypedData = data;
        }

        public TData TypedData
        {
            get => (TData?)BaseEvent.Data!;
            set => BaseEvent.Data = value;
        }

        public object Data => BaseEvent.Data!;
        public string Id => BaseEvent.Id!;
        public string Type => BaseEvent.Type!;
        public DateTimeOffset Time => BaseEvent.Time!.Value;
        public Uri Source => BaseEvent.Source!;

        public object? this[CloudEventAttribute attribute]
        {
            get => BaseEvent[attribute];
            set => BaseEvent[attribute] = value;
        }

        public object? this[string attributeName]
        {
            get => BaseEvent[attributeName];
            set => BaseEvent[attributeName] = value;
        }

        private CloudEvent BaseEvent { get; }

        public CloudEvent ConvertToCloudEvent()
        {
            CloudEvent clone = new CloudEvent(BaseEvent.SpecVersion, BaseEvent.ExtensionAttributes)
            {
                Data = Data
            };
            foreach (var (key, value) in GetPopulatedAttributes())
            {
                clone[key] = value;
            }

            return clone;
        }

        public CloudEventAttribute? GetAttribute(string name) => BaseEvent.GetAttribute(name);

        public IEnumerable<KeyValuePair<CloudEventAttribute, object>> GetPopulatedAttributes() => BaseEvent.GetPopulatedAttributes();

        public void SetAttributeFromString(string key, string value) => BaseEvent.SetAttributeFromString(key, value);

        private static MotorCloudEvent<T> CreateCloudEvent<T>(IApplicationNameService applicationNameService, T data,
            IEnumerable<KeyValuePair<CloudEventAttribute, object>>? extensions = null)
            where T : class
        {
            return new MotorCloudEvent<T>(applicationNameService, data, applicationNameService.GetSource(),
                extensions: extensions?.ToArray() ?? Array.Empty<KeyValuePair<CloudEventAttribute, object>>());
        }

        public MotorCloudEvent<T> CreateNew<T>(T data, bool useOldIdentifier = false)
            where T : class
        {
            return useOldIdentifier
                ? new MotorCloudEvent<T>(_applicationNameService, data, BaseEvent.Source!, BaseEvent.Id, BaseEvent.Time,
                    BaseEvent.GetPopulatedAttributes().ToArray())
                : CreateCloudEvent(_applicationNameService, data, BaseEvent.GetPopulatedAttributes());
        }
    }
}
