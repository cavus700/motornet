using System;
using System.Threading.Tasks;
using Motor.Extensions.Hosting.RabbitMQ;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using TestContainers.Container.Abstractions;
using TestContainers.Container.Abstractions.Hosting;
using Xunit;

namespace Motor.Extensions.Hosting.RabbitMQ_IntegrationTest
{
    public class RabbitMQFixture : IAsyncLifetime
    {
        public RabbitMQFixture()
        {
            Container = new ContainerBuilder<GenericContainer>()
                .ConfigureDockerImageName("rabbitmq:3.7.21")
                .ConfigureContainer((_, container) =>
                {
                    container.Env["RABBITMQ_DEFAULT_USER"] = "guest";
                    container.Env["RABBITMQ_DEFAULT_PASS"] = "guest";
                    container.ExposedPorts.Add(5672);
                })
                .Build();
        }

        public IConnection Connection => CreateConnection();

        public IRabbitMQConnectionFactory<T> ConnectionFactory<T>() =>
            new RabbitMQConnectionFactory<T>(CreateConnectionFactory());

        public int Port => Container.GetMappedPort(5672);
        public string Hostname => Container.GetDockerHostIpAddress();

        private GenericContainer Container { get; }

        public async Task InitializeAsync()
        {
            await Container.StartAsync();
            Policy
                .Handle<BrokerUnreachableException>()
                .WaitAndRetry(10, i => TimeSpan.FromSeconds(Math.Pow(2, i)))
                .Execute(CreateConnection);
        }

        public Task DisposeAsync()
        {
            return Container.StopAsync();
        }

        private IConnectionFactory CreateConnectionFactory() => new ConnectionFactory
        {
            Uri = new Uri($"amqp://guest:guest@{Hostname}:{Port}")
        };

        private IConnection CreateConnection() => CreateConnectionFactory().CreateConnection();
    }
}
