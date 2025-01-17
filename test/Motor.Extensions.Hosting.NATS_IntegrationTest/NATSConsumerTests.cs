using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Motor.Extensions.Hosting.Abstractions;
using Motor.Extensions.Hosting.CloudEvents;
using Motor.Extensions.Hosting.NATS;
using Motor.Extensions.Hosting.NATS.Options;
using NATS.Client;
using RandomDataGenerator.FieldOptions;
using RandomDataGenerator.Randomizers;
using Xunit;

namespace Motor.Extensions.Hosting.NATS_IntegrationTest
{
    [Collection("NATSMessage")]
    public class NATSConsumerTests : IClassFixture<NATSFixture>
    {
        private readonly IRandomizerString _randomizerString;
        private readonly string _natsUrl;

        public NATSConsumerTests(NATSFixture fixture)
        {
            _randomizerString = RandomizerFactory.GetRandomizer(new FieldOptionsTextRegex { Pattern = @"^[A-Z]{10}" });
            _natsUrl = $"{fixture.Hostname}:{fixture.Port}";
        }

        [Fact(Timeout = 50000)]
        public void Consume_RawPublishIntoNATSAndConsumeCreateCloudEvent_ConsumedEqualsPublished()
        {
            const string expectedMessage = "testMessage";
            var topicName = _randomizerString.Generate();
            var queueName = _randomizerString.Generate();
            var clientOptions = GetNATSClientOptions(topicName, queueName);

            var nats = new NATSClientFactory().From(clientOptions);

            var consumer = GetConsumer<string>(new OptionsWrapper<NATSClientOptions>(clientOptions), queueName);
            var rawConsumedNatsMessage = RawConsumedNatsMessage(consumer);
            PublishMessage(nats, topicName, expectedMessage);
            Assert.Equal(expectedMessage, Encoding.UTF8.GetString(rawConsumedNatsMessage.GetAwaiter().GetResult()));
        }

        private static async Task<byte[]> RawConsumedNatsMessage(NATSConsumer<string> consumer)
        {
            var rawConsumedNatsMessage = (byte[])null;
            var taskCompletionSource = new TaskCompletionSource();
            consumer.ConsumeCallbackAsync = async (dataEvent, _) =>
            {
                rawConsumedNatsMessage = dataEvent.TypedData;
                taskCompletionSource.TrySetResult();
                return await Task.FromResult(ProcessedMessageStatus.Success);
            };

            await consumer.StartAsync();
            var consumerStartTask = consumer.ExecuteAsync();

            await Task.WhenAny(consumerStartTask, taskCompletionSource.Task);
            return rawConsumedNatsMessage;
        }

        private NATSClientOptions GetNATSClientOptions(string topicName, string queueName)
        {
            var clientOptions = new NATSClientOptions
            {
                Url = _natsUrl,
                Topic = topicName,
                Queue = queueName
            };
            return clientOptions;
        }

        private static void PublishMessage(IConnection natsClient, string topic, string message)
        {
            natsClient.Publish(topic, Encoding.UTF8.GetBytes(message));
        }

        private NATSConsumer<T> GetConsumer<T>(IOptions<NATSClientOptions> clientOptions, string queueName)
        {
            var fakeLoggerMock = Mock.Of<ILogger<NATSConsumer<T>>>();
            return new NATSConsumer<T>(clientOptions, fakeLoggerMock, GetApplicationNameService(),
                new NATSClientFactory());
        }

        private IApplicationNameService GetApplicationNameService(string source = "test://non")
        {
            var mock = new Mock<IApplicationNameService>();
            mock.Setup(t => t.GetSource()).Returns(new Uri(source));
            return mock.Object;
        }
    }
}
