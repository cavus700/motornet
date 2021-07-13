using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.Extensions;
using Microsoft.Extensions.Options;
using Moq;
using Motor.Extensions.Diagnostics.Tracing;
using Motor.Extensions.Hosting.Abstractions;
using Motor.Extensions.Hosting.RabbitMQ;
using Motor.Extensions.Hosting.RabbitMQ.Options;
using Motor.Extensions.TestUtilities;
using RabbitMQ.Client;
using Xunit;

namespace Motor.Extensions.Hosting.RabbitMQ_UnitTest
{
    public class RabbitMQMessagePublisherTests
    {
        private const string DefaultExchange = "exchange";

        [Fact]
        public async Task PublishMessageAsync_WithConfig_ConnectionFactoryIsSet()
        {
            var mock = GetDefaultConnectionFactoryMock<string>();
            var config = GetConfig();
            var publisher = GetPublisher(mock.Object, config);

            await publisher.PublishMessageAsync(MotorCloudEvent.CreateTestCloudEvent(new byte[0]));

            mock.VerifyGet(x => x.CurrentChannel, Times.Exactly(1));
        }

        [Fact]
        public async Task PublishMessageAsync_WithConfig_ConnectionEstablished()
        {
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>();
            var publisher = GetPublisher(rabbitConnectionFactoryMock.Object, GetConfig());

            await publisher.PublishMessageAsync(MotorCloudEvent.CreateTestCloudEvent(new byte[0]));

            rabbitConnectionFactoryMock.VerifyGet(x => x.CurrentChannel, Times.Exactly(1));
        }

        [Fact]
        public async Task PublishMessageAsync_WithConfig_ChannelEstablished()
        {
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>();
            var publisher = GetPublisher(rabbitConnectionFactoryMock.Object, GetConfig());

            await publisher.PublishMessageAsync(MotorCloudEvent.CreateTestCloudEvent(new byte[0]));

            rabbitConnectionFactoryMock.Verify(x => x.CurrentChannel, Times.Exactly(1));
        }

        [Fact]
        public async Task PublishMessageAsync_WithConfig_BasicPropertiesAreCreated()
        {
            var modelMock = new Mock<IModel>();
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>(modelMock: modelMock);
            var publisher = GetPublisher(rabbitConnectionFactoryMock.Object, GetConfig());

            await publisher.PublishMessageAsync(MotorCloudEvent.CreateTestCloudEvent(new byte[0]));

            modelMock.Verify(x => x.CreateBasicProperties(), Times.Exactly(1));
        }

        [Fact]
        public async Task PublishMessageAsync_WithConfig_BasicPropertiesAreSet()
        {
            var basicProperties = Mock.Of<IBasicProperties>();
            var modelMock = new Mock<IModel>();
            modelMock.Setup(x => x.CreateBasicProperties()).Returns(basicProperties);
            var rabbitConnectionFactoryMock =
                GetDefaultConnectionFactoryMock<string>(modelMock: modelMock, basicProperties: basicProperties);

            var publisher = GetPublisher(rabbitConnectionFactoryMock.Object, GetConfig());
            const byte priority = 1;

            var openTelemetryExtension = new DistributedTracingExtension();

            var activity = new Activity(nameof(RabbitMQMessagePublisherTests));
            activity.SetIdFormat(ActivityIdFormat.W3C);
            activity.Start();
            openTelemetryExtension.SetActivity(activity);

            var motorCloudEvent = MotorCloudEvent.CreateTestCloudEvent(new byte[0],
                extensions: new List<ICloudEventExtension>
                {
                    new RabbitMQPriorityExtension(priority), openTelemetryExtension
                }.ToArray());

            await publisher.PublishMessageAsync(motorCloudEvent);

            Assert.Equal(2, basicProperties.DeliveryMode);
            Assert.Equal(priority, basicProperties.Priority);
            var traceparent = Encoding.UTF8.GetString((byte[])basicProperties.Headers[
                    $"{BasicPropertiesExtensions.CloudEventPrefix}{DistributedTracingExtension.TraceParentAttributeName}"])
                .Trim('"');
            var activityContext = ActivityContext.Parse(traceparent, null);
            Assert.Equal(activity.Context.TraceId, activityContext.TraceId);
            Assert.Equal(activity.Context.SpanId, activityContext.SpanId);
            Assert.Equal(activity.Context.TraceFlags, activityContext.TraceFlags);
        }

        [Fact]
        public async Task PublishMessageAsync_WithConfig_MessagePublished()
        {
            var basicProperties = Mock.Of<IBasicProperties>();
            var modelMock = new Mock<IModel>();
            modelMock.Setup(x => x.CreateBasicProperties()).Returns(basicProperties);
            var rabbitConnectionFactoryMock =
                GetDefaultConnectionFactoryMock<string>(modelMock: modelMock, basicProperties: basicProperties);
            var config = GetConfig();
            var publisher = GetPublisher(rabbitConnectionFactoryMock.Object, config);
            var message = new byte[0];

            await publisher.PublishMessageAsync(MotorCloudEvent.CreateTestCloudEvent(message));

            modelMock.Verify(x => x.BasicPublish(config.PublishingTarget.Exchange,
                config.PublishingTarget.RoutingKey, true, basicProperties, message));
        }

        [Fact]
        public async Task PublishMessageAsync_CloudEventWithRoutingKeyExtension_MessagePublished()
        {
            const string customExchange = "cloud-event-exchange";
            const string customRoutingKey = "cloud-event-routing-key";

            var modelMock = new Mock<IModel>();
            modelMock.Setup(x => x.CreateBasicProperties()).Returns(Mock.Of<IBasicProperties>());
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>(modelMock: modelMock);
            var publisher = GetPublisher(rabbitConnectionFactoryMock.Object);
            var extensions = new List<ICloudEventExtension>
            {
                new RabbitMQBindingExtension(customExchange, customRoutingKey)
            };

            await publisher.PublishMessageAsync(
                MotorCloudEvent.CreateTestCloudEvent(new byte[0], extensions: extensions));

            modelMock.Verify(x => x.BasicPublish(customExchange,
                customRoutingKey, true, It.IsAny<IBasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()));
        }

        [Fact]
        public async Task PublishMessageAsync_CloudEventWithRoutingKeyExtensionAndOverwriteExchange_MessagePublished()
        {
            const string customExchange = "cloud-event-exchange";
            const string customRoutingKey = "cloud-event-routing-key";

            var modelMock = new Mock<IModel>();
            modelMock.Setup(x => x.CreateBasicProperties()).Returns(Mock.Of<IBasicProperties>());
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>(modelMock: modelMock);
            var publisher = GetPublisher(rabbitConnectionFactoryMock.Object, overwriteExchange: true);
            var extensions = new List<ICloudEventExtension>
            {
                new RabbitMQBindingExtension(customExchange, customRoutingKey)
            };

            await publisher.PublishMessageAsync(
                MotorCloudEvent.CreateTestCloudEvent(new byte[0], extensions: extensions));

            modelMock.Verify(x => x.BasicPublish(DefaultExchange,
                customRoutingKey, true, It.IsAny<IBasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>()));
        }

        private ITypedMessagePublisher<byte[]> GetPublisher(
            IRabbitMQConnectionFactory<string> factory = null,
            RabbitMQPublisherOptions<string> options = null,
            bool overwriteExchange = false)
        {
            options ??= GetConfig(overwriteExchange);
            factory ??= GetDefaultConnectionFactoryMock<string>().Object;

            var configMock = new Mock<IOptions<RabbitMQPublisherOptions<string>>>();
            configMock.Setup(x => x.Value).Returns(options);
            return new RabbitMQMessagePublisher<string>(
                factory,
                configMock.Object,
                new JsonEventFormatter()
            );
        }

        private RabbitMQPublisherOptions<string> GetConfig(bool overwriteExchange = false)
        {
            return new()
            {
                Host = "host",
                VirtualHost = "vHost",
                User = "user",
                Password = "pw",
                OverwriteExchange = overwriteExchange,
                PublishingTarget = new RabbitMQBindingOptions
                {
                    Exchange = DefaultExchange,
                    RoutingKey = "routingKey"
                }
            };
        }

        private Mock<IRabbitMQConnectionFactory<T>> GetDefaultConnectionFactoryMock<T>(
            Mock<IConnection> connectionMock = null,
            Mock<IModel> modelMock = null,
            IBasicProperties basicProperties = null
        )
        {
            var rabbitConnectionFactoryMock = new Mock<IRabbitMQConnectionFactory<T>>();
            connectionMock ??= new Mock<IConnection>();
            modelMock ??= new Mock<IModel>();

            connectionMock
                .Setup(x => x.CreateModel())
                .Returns(modelMock.Object);

            modelMock
                .Setup(x => x.CreateBasicProperties())
                .Returns(basicProperties ?? new Mock<IBasicProperties>().Object);

            rabbitConnectionFactoryMock.Setup(f => f.CurrentConnection).Returns(connectionMock.Object);
            rabbitConnectionFactoryMock.Setup(f => f.CurrentChannel).Returns(modelMock.Object);
            rabbitConnectionFactoryMock.Setup(f => f.CreateConnection()).Returns(connectionMock.Object);
            rabbitConnectionFactoryMock.Setup(f => f.CreateModel()).Returns(modelMock.Object);

            return rabbitConnectionFactoryMock;
        }
    }
}
