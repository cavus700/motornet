using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Motor.Extensions.Hosting.Abstractions;
using Motor.Extensions.Hosting.CloudEvents;
using Motor.Extensions.Hosting.RabbitMQ;
using Motor.Extensions.Hosting.RabbitMQ.Options;
using RabbitMQ.Client;
using Xunit;

namespace Motor.Extensions.Hosting.RabbitMQ_UnitTest
{
    public class RabbitMQMessageConsumerTests
    {
        private const int DefaultPrefetchCount = 777;


        private ILogger<RabbitMQMessageConsumer<string>> FakeLogger =>
            new Mock<ILogger<RabbitMQMessageConsumer<string>>>().Object;

        private IHostApplicationLifetime FakeApplicationLifetime => new Mock<IHostApplicationLifetime>().Object;

        [Fact]
        public async Task StartAsync_CallbackNotConfigured_Throw()
        {
            var consumer = GetRabbitMQMessageConsumer();

            await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.StartAsync());
        }

        [Fact]
        public async Task StartAsync_AlreadyStarted_Throw()
        {
            var consumer = GetRabbitMQMessageConsumer();
            SetConsumerCallback(consumer);
            await consumer.StartAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.StartAsync());
        }

        [Fact]
        public async Task StartAsync_CallbackConfigured_ReturnCompletedTask()
        {
            var consumer = GetRabbitMQMessageConsumer();
            SetConsumerCallback(consumer);

            var actual = consumer.StartAsync();
            await actual;

            Assert.Equal(Task.CompletedTask, actual);
        }

        [Fact]
        public async Task StartAsync_CallbackConfigured_ConnectionFactoryIsSet()
        {
            var cfg = GetConfig();
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>();
            var consumer = GetRabbitMQMessageConsumer(rabbitConnectionFactoryMock.Object, cfg);
            SetConsumerCallback(consumer);

            await consumer.StartAsync();

            rabbitConnectionFactoryMock.VerifyGet(x => x.CurrentChannel, Times.AtLeastOnce);
        }

        [Fact]
        public async Task StartAsync_CallbackConfigured_ChannelConfigured()
        {
            var channelMock = new Mock<IModel>();
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>(channelMock: channelMock);
            var consumer = GetRabbitMQMessageConsumer(rabbitConnectionFactoryMock.Object);
            SetConsumerCallback(consumer);

            await consumer.StartAsync();

            channelMock.Verify(x => x.BasicQos(0, DefaultPrefetchCount, false), Times.Exactly(1));
        }

        [Fact]
        public async Task StartAsync_CallbackConfigured_QueueDeclaredAndBind()
        {
            var channelMock = new Mock<IModel>();
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>(channelMock: channelMock);
            var cfg = GetConfig();
            var expectedArguments = GetExpectedArgumentsFromConfig(cfg.Queue);
            var consumer = GetRabbitMQMessageConsumer(rabbitConnectionFactoryMock.Object, cfg);
            SetConsumerCallback(consumer);

            await consumer.StartAsync();

            channelMock.Verify(x =>
                    x.QueueDeclare(cfg.Queue.Name, cfg.Queue.Durable, false, cfg.Queue.AutoDelete, expectedArguments),
                Times.Exactly(1));
            channelMock.Verify(
                x => x.QueueBind(cfg.Queue.Name, cfg.Queue.Bindings.First().Exchange,
                    cfg.Queue.Bindings.First().RoutingKey, cfg.Queue.Bindings.First().Arguments), Times.Exactly(1));
        }

        [Fact]
        public async Task StartAsync_ConsumerConfigWithHasTwoBindings_QueueIsBoundTwice()
        {
            var channelMock = new Mock<IModel>();
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>(channelMock: channelMock);
            var cfg = GetConfig();
            var consumer = GetRabbitMQMessageConsumer(rabbitConnectionFactoryMock.Object, cfg);
            SetConsumerCallback(consumer);

            await consumer.StartAsync();

            channelMock.Verify(
                x => x.QueueBind(cfg.Queue.Name, cfg.Queue.Bindings.First().Exchange,
                    cfg.Queue.Bindings.First().RoutingKey, cfg.Queue.Bindings.First().Arguments), Times.Exactly(1));
            channelMock.Verify(
                x => x.QueueBind(cfg.Queue.Name, cfg.Queue.Bindings.Last().Exchange,
                    cfg.Queue.Bindings.Last().RoutingKey, cfg.Queue.Bindings.Last().Arguments), Times.Exactly(1));
        }

        [Fact]
        public async Task StartAsync_CallbackConfigured_ChannelConsumed()
        {
            var channelMock = new Mock<IModel>();
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>(channelMock: channelMock);
            var consumer = GetRabbitMQMessageConsumer(rabbitConnectionFactoryMock.Object, GetConfig());
            SetConsumerCallback(consumer);

            await consumer.StartAsync();

            channelMock.Verify(x => x.BasicConsume(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(),
                It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IBasicConsumer>()));
        }

        [Fact]
        public async Task StartAsync_DeclareQueueWithoutMaxPriority_QueueDeclared()
        {
            var channelMock = new Mock<IModel>();
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>(channelMock: channelMock);
            var cfg = GetConfig();
            cfg.Queue.MaxPriority = null;
            var expectedArguments = GetExpectedArgumentsFromConfig(cfg.Queue);
            expectedArguments.Remove("x-max-priority");
            var consumer = GetRabbitMQMessageConsumer(rabbitConnectionFactoryMock.Object, cfg);
            SetConsumerCallback(consumer);

            await consumer.StartAsync();

            channelMock.Verify(x =>
                    x.QueueDeclare(cfg.Queue.Name, cfg.Queue.Durable, false, cfg.Queue.AutoDelete, expectedArguments),
                Times.Exactly(1));
        }

        [Fact]
        public async Task StartAsync_DeclareQueueWithoutMaxLength_QueueDeclared()
        {
            var channelMock = new Mock<IModel>();
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>(channelMock: channelMock);
            var cfg = GetConfig();
            cfg.Queue.MaxLength = null;
            var expectedArguments = GetExpectedArgumentsFromConfig(cfg.Queue);
            expectedArguments.Remove("x-max-length");
            var consumer = GetRabbitMQMessageConsumer(rabbitConnectionFactoryMock.Object, cfg);
            SetConsumerCallback(consumer);

            await consumer.StartAsync();

            channelMock.Verify(x =>
                    x.QueueDeclare(cfg.Queue.Name, cfg.Queue.Durable, false, cfg.Queue.AutoDelete, expectedArguments),
                Times.Exactly(1));
        }

        [Fact]
        public async Task StartAsync_DeclareQueueWithoutMessageTtl_QueueDeclared()
        {
            var channelMock = new Mock<IModel>();
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>(channelMock: channelMock);
            var cfg = GetConfig();
            cfg.Queue.MessageTtl = null;
            var expectedArguments = GetExpectedArgumentsFromConfig(cfg.Queue);
            expectedArguments.Remove("x-message-ttl");
            var consumer = GetRabbitMQMessageConsumer(rabbitConnectionFactoryMock.Object, cfg);
            SetConsumerCallback(consumer);

            await consumer.StartAsync();

            channelMock.Verify(x =>
                    x.QueueDeclare(cfg.Queue.Name, cfg.Queue.Durable, false, cfg.Queue.AutoDelete, expectedArguments),
                Times.Exactly(1));
        }

        [Fact]
        public async Task StartAsync_DontDeclareQueues_QueueDeclareIsNotCalled()
        {
            var channelMock = new Mock<IModel>();
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>(channelMock: channelMock);
            var cfg = GetConfig(false);
            cfg.Queue.MessageTtl = null;
            var consumer = GetRabbitMQMessageConsumer(rabbitConnectionFactoryMock.Object, cfg);
            SetConsumerCallback(consumer);

            await consumer.StartAsync();

            channelMock.Verify(x =>
                    x.QueueDeclare(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                        It.IsAny<IDictionary<string, object>>()),
                Times.Never);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(123456)]
        [InlineData(10_000_000_000)]
        public async Task StartAsync_DeclareQueueWithMaxLengthBytes_QueueDeclared(long maxLengthBytes)
        {
            var channelMock = new Mock<IModel>();
            var rabbitConnectionFactoryMock = GetDefaultConnectionFactoryMock<string>(channelMock: channelMock);
            var cfg = GetConfig();
            cfg.Queue.MaxLengthBytes = maxLengthBytes;
            var consumer = GetRabbitMQMessageConsumer(rabbitConnectionFactoryMock.Object, cfg);
            SetConsumerCallback(consumer);

            await consumer.StartAsync();

            channelMock.Verify(
                x => x.QueueDeclare(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                    It.Is<IDictionary<string, object>>(dict => (long)dict["x-max-length-bytes"] == maxLengthBytes)),
                Times.Exactly(1));
        }

        private static IDictionary<string, object> GetExpectedArgumentsFromConfig(RabbitMQQueueOptions options)
        {
            var expectedArguments = options.Arguments.ToDictionary(t => t.Key, t => t.Value);
            expectedArguments.Add("x-max-priority", options.MaxPriority);
            expectedArguments.Add("x-max-length", options.MaxLength);
            expectedArguments.Add("x-max-length-bytes", options.MaxLengthBytes);
            expectedArguments.Add("x-message-ttl", options.MessageTtl);
            return expectedArguments;
        }

        private IMessageConsumer<string> GetRabbitMQMessageConsumer(
            IRabbitMQConnectionFactory<string> rabbitMqConnectionFactory = null,
            RabbitMQConsumerOptions<string> options = null, IHostApplicationLifetime applicationLifetime = null)
        {
            rabbitMqConnectionFactory ??= GetDefaultConnectionFactoryMock<string>().Object;
            applicationLifetime ??= FakeApplicationLifetime;
            var optionsWrapper = new OptionsWrapper<RabbitMQConsumerOptions<string>>(options ?? GetConfig());

            return new RabbitMQMessageConsumer<string>(FakeLogger, rabbitMqConnectionFactory, optionsWrapper,
                applicationLifetime, GetApplicationNameService());
        }

        private IApplicationNameService GetApplicationNameService(string source = "test://non")
        {
            var mock = new Mock<IApplicationNameService>();
            mock.Setup(t => t.GetSource()).Returns(new Uri(source));
            return mock.Object;
        }

        private Mock<IRabbitMQConnectionFactory<T>> GetDefaultConnectionFactoryMock<T>(
            Mock<IConnection> connectionMock = null,
            Mock<IModel> channelMock = null)
        {
            var rabbitConnectionFactoryMock = new Mock<IRabbitMQConnectionFactory<T>>();
            connectionMock ??= new Mock<IConnection>();
            channelMock ??= new Mock<IModel>();

            rabbitConnectionFactoryMock
                .Setup(f => f.CurrentConnection)
                .Returns(connectionMock.Object);

            rabbitConnectionFactoryMock
                .Setup(f => f.CurrentChannel)
                .Returns(channelMock.Object);

            rabbitConnectionFactoryMock
                .Setup(f => f.CreateConnection())
                .Returns(connectionMock.Object);

            rabbitConnectionFactoryMock
                .Setup(f => f.CreateModel())
                .Returns(channelMock.Object);

            connectionMock
                .Setup(x => x.CreateModel())
                .Returns(channelMock.Object);

            return rabbitConnectionFactoryMock;
        }

        private RabbitMQConsumerOptions<string> GetConfig(bool declareQueue = true)
        {
            return new()
            {
                Host = "someHost",
                Port = 12345,
                User = "user",
                Password = "pass",
                VirtualHost = "vHost",
                PrefetchCount = DefaultPrefetchCount,
                DeclareQueue = declareQueue,
                RequestedHeartbeat = TimeSpan.FromSeconds(111),
                Queue = new RabbitMQQueueOptions
                {
                    Name = "qName",
                    Durable = true,
                    AutoDelete = false,
                    Bindings = new[]
                    {
                        new RabbitMQBindingOptions
                        {
                            Exchange = "someExchange",
                            RoutingKey = "routingKey",
                            Arguments = new Dictionary<string, object>()
                        },
                        new RabbitMQBindingOptions
                        {
                            Exchange = "someOtherExchange",
                            RoutingKey = "someOtherRoutingKey",
                            Arguments = new Dictionary<string, object>()
                        }
                    }
                }
            };
        }

        private void SetConsumerCallback(IMessageConsumer<string> consumer)
        {
            consumer.ConsumeCallbackAsync = (_, _) => Task.FromResult(ProcessedMessageStatus.Success);
        }
    }
}
