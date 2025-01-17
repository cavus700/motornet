using Motor.Extensions.Hosting.RabbitMQ.Validation;

namespace Motor.Extensions.Hosting.RabbitMQ.Options
{
    public record RabbitMQConsumerOptions<T> : RabbitMQBaseOptions
    {
        [RequireValid]
        public RabbitMQQueueOptions Queue { get; set; } = new();

        public ushort PrefetchCount { get; set; } = 10;
        public bool DeclareQueue { get; set; } = true;
        public bool ExtractBindingKey { get; set; }
    }
}
